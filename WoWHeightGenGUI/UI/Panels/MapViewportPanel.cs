using System.Numerics;
using ImGuiNET;
using Silk.NET.OpenGL;
using WoWHeightGenGUI.App;
using WoWHeightGenGUI.Models;
using WoWHeightGenGUI.Rendering;
using WoWHeightGenGUI.Services;
using WoWHeightGenGUI.UI.Dialogs;

namespace WoWHeightGenGUI.UI.Panels;

/// <summary>
/// Unified map viewport panel that displays all layers composited together.
/// Uses quad-based streaming for efficient GPU memory usage.
/// Supports pan/zoom navigation via Camera2D and keyboard shortcuts.
/// </summary>
public class MapViewportPanel : IPanel, IConnectionAwarePanel, IDisposable
{
    private readonly Application _app;
    private readonly PanelManager _panelManager;

    private LayerCompositor? _compositor;
    private QuadCache? _quadCache;
    private QuadStreamingService? _streamingService;
    private MapLoadingService? _loadingService;

    // Viewport-sized framebuffer for compositing visible quads
    private uint _viewportFramebuffer;
    private uint _viewportFramebufferTexture;
    private int _viewportFboWidth;
    private int _viewportFboHeight;

    private bool _isVisible = true;
    private bool _initialized;
    private bool _disposed;

    // Viewport state
    private Vector2 _lastMousePos;
    private bool _isDragging;
    private bool _isHovering;

    // Export dialog
    private ExportDialog? _exportDialog;

    public string Name => "Map Viewport";
    public bool IsVisible { get => _isVisible; set => _isVisible = value; }

    /// <summary>
    /// The current map view state (shared with LayersPropertiesPanel)
    /// </summary>
    public MapViewState ViewState { get; } = new();

    /// <summary>
    /// The quad cache for accessing quad data
    /// </summary>
    public QuadCache? QuadCache => _quadCache;

    /// <summary>
    /// The streaming service for accessing visible quads
    /// </summary>
    public QuadStreamingService? StreamingService => _streamingService;

    public MapViewportPanel(Application app, PanelManager panelManager)
    {
        _app = app;
        _panelManager = panelManager;
    }

    /// <summary>
    /// Initialize OpenGL resources
    /// </summary>
    public void Initialize()
    {
        if (_initialized) return;

        var gl = _app.GL;

        // Create compositor
        _compositor = new LayerCompositor(gl);
        _compositor.Initialize();

        // Create quad cache with max 4096 GPU quads (full 64x64 grid)
        _quadCache = new QuadCache(gl, maxGpuQuads: 4096);

        // Create streaming service with 2-tile buffer
        _streamingService = new QuadStreamingService(_quadCache, bufferTiles: 2);

        // Create export dialog
        _exportDialog = new ExportDialog(gl);

        _initialized = true;
    }

    /// <summary>
    /// Load a map into the viewport
    /// </summary>
    public void LoadMap(MapEntry map)
    {
        if (!_initialized)
            Initialize();

        if (_app.Context == null) return;

        // Cancel any existing loading
        _loadingService?.Cancel();
        _loadingService?.Dispose();

        // Clear existing quad data
        _quadCache?.Clear();
        _streamingService?.Clear();

        // Clear existing state
        ViewState.Clear();
        ViewState.CurrentMap = map;
        ViewState.CurrentWdtId = map.WdtFileDataId;

        // Create loading service
        _loadingService = new MapLoadingService(_app.Context);

        _loadingService.OnProgressChanged += progress =>
        {
            ViewState.LoadingProgress = progress;
        };
        _loadingService.OnLoadingComplete += () =>
        {
            ViewState.IsLoading = false;
        };
        _loadingService.OnLoadingError += ex =>
        {
            Console.WriteLine($"Loading error: {ex.Message}");
            ViewState.IsLoading = false;
        };

        // Get tile bounds from WDT immediately (fast - just reads WDT header)
        var bounds = _loadingService.GetTileBoundsFromWdt(map.WdtFileDataId);
        if (bounds.HasValue)
        {
            ViewState.MinTileX = bounds.Value.minX;
            ViewState.MaxTileX = bounds.Value.maxX;
            ViewState.MinTileY = bounds.Value.minY;
            ViewState.MaxTileY = bounds.Value.maxY;
        }

        ViewState.IsLoading = true;
        ViewState.LoadingProgress = 0;

        // Start loading all layers
        _loadingService.StartLoading(map.WdtFileDataId);

        // Fit to map bounds immediately (using WDT-derived bounds)
        ViewState.FitToMap();
    }

    public void OnConnectionChanged()
    {
        // Clear state when disconnected
        ViewState.Clear();
        _loadingService?.Cancel();
        _loadingService?.Dispose();
        _loadingService = null;
        _quadCache?.Clear();
        _streamingService?.Clear();
    }

    public void Update(float deltaTime)
    {
        // Process pending tile updates from background loading
        ProcessPendingUpdates();
    }

    private void ProcessPendingUpdates()
    {
        if (_loadingService == null || _streamingService == null) return;

        // Process up to 10 updates per frame to avoid stalling
        int updatesProcessed = 0;
        while (updatesProcessed < 10 && _loadingService.TryGetPendingUpdate(out var update))
        {
            if (update != null)
            {
                // Route tile update to quad streaming service
                _streamingService.ProcessTileUpdate(update);

                // Track tile bounds
                ViewState.UpdateTileBounds(update.TileX, update.TileY);

                // Update minimap tile size if first minimap tile
                if (update.Layer == LayerType.Minimap && ViewState.MinimapTileSizePixels == 256)
                {
                    ViewState.MinimapTileSizePixels = update.Width;
                }
            }
            updatesProcessed++;
        }
    }

    public void Render()
    {
        if (!_initialized)
            Initialize();

        // Process updates before rendering
        ProcessPendingUpdates();

        var windowFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;

        if (ImGui.Begin(Name, ref _isVisible, windowFlags))
        {
            _isHovering = ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows);

            // Render toolbar
            RenderToolbar();

            ImGui.Separator();

            // Get content region for viewport
            var contentPos = ImGui.GetCursorScreenPos();
            var contentSize = ImGui.GetContentRegionAvail();

            if (contentSize.X > 0 && contentSize.Y > 0)
            {
                // Update camera viewport size
                ViewState.Camera.ViewportSize = contentSize;

                // Handle input
                HandleInput(contentPos, contentSize);

                // Render the composited layers using quad system
                RenderViewport(contentPos, contentSize);

                // Render map info tooltip
                RenderMapTooltip(contentPos, contentSize);
            }

            // Handle keyboard shortcuts
            if (_isHovering)
            {
                HandleKeyboardInput();
            }
        }
        ImGui.End();

        // Render export dialog (outside of main window)
        _exportDialog?.Render();
    }

    private void RenderToolbar()
    {
        // Map name
        if (ViewState.CurrentMap != null)
        {
            ImGui.Text(ViewState.CurrentMap.Name);
            ImGui.SameLine();
            ImGui.TextDisabled($"(ID: {ViewState.CurrentMap.Id})");

            // Export button (only when map is loaded and not loading)
            if (!ViewState.IsLoading)
            {
                ImGui.SameLine();
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1.0f, 0.55f, 0.0f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1.0f, 0.65f, 0.1f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.9f, 0.5f, 0.0f, 1.0f));
                if (ImGui.Button("Export"))
                {
                    _exportDialog?.Open(ViewState, _loadingService, _app.Context, _compositor,
                        _quadCache, _streamingService);
                }
                ImGui.PopStyleColor(3);
            }
        }
        else
        {
            ImGui.TextDisabled("No map loaded");
        }

        ImGui.SameLine(ImGui.GetWindowWidth() - 350);

        // Zoom controls - show zoom as percentage (at zoom=128, we're at 100% native resolution)
        float zoomPercent = ViewState.Camera.Zoom / Camera2D.PixelsPerTile * 100f;
        if (ImGui.Button("-"))
        {
            ViewState.Camera.Zoom *= 0.9f;  // Setter auto-clamps to min/max
        }
        ImGui.SameLine();
        ImGui.Text($"{zoomPercent:F0}%");
        ImGui.SameLine();
        if (ImGui.Button("+"))
        {
            ViewState.Camera.Zoom *= 1.1f;  // Setter auto-clamps to min/max
        }

        ImGui.SameLine();

        // Fit button
        if (ImGui.Button("Fit (F)"))
        {
            ViewState.FitToMap();
        }

        ImGui.SameLine();

        // Reset button
        if (ImGui.Button("Reset (R)"))
        {
            ViewState.ResetCamera();
        }

        // Loading indicator and GPU stats
        if (ViewState.IsLoading)
        {
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(0.5f, 0.8f, 1.0f, 1.0f),
                $"Loading... {ViewState.LoadingProgress:P0}");
        }

        // Show GPU quad count
        if (_quadCache != null && _quadCache.GpuQuadCount > 0)
        {
            ImGui.SameLine();
            ImGui.TextDisabled($"[GPU: {_quadCache.GpuQuadCount}/{_quadCache.MaxGpuQuads}]");
        }
    }

    private void HandleInput(Vector2 contentPos, Vector2 contentSize)
    {
        var io = ImGui.GetIO();
        var mousePos = io.MousePos;

        // Don't process input if any popup/modal is open
        if (ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopup))
            return;

        // Check if mouse is in content area
        bool inContent = mousePos.X >= contentPos.X && mousePos.X < contentPos.X + contentSize.X &&
                         mousePos.Y >= contentPos.Y && mousePos.Y < contentPos.Y + contentSize.Y;

        // Pan with mouse drag (middle mouse or left mouse while holding)
        if (inContent && _isHovering)
        {
            // Mouse wheel zoom - pass position relative to viewport top-left
            if (io.MouseWheel != 0)
            {
                var localMouse = mousePos - contentPos;
                ViewState.Camera.ZoomTowards(io.MouseWheel, localMouse);
            }

            // Start dragging
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) || ImGui.IsMouseClicked(ImGuiMouseButton.Middle))
            {
                _isDragging = true;
                _lastMousePos = mousePos;
            }
        }

        // Continue drag - pan accounts for zoom automatically via Camera2D.Pan()
        if (_isDragging)
        {
            if (ImGui.IsMouseDown(ImGuiMouseButton.Left) || ImGui.IsMouseDown(ImGuiMouseButton.Middle))
            {
                var delta = mousePos - _lastMousePos;
                ViewState.Camera.Pan(delta);  // Pan divides by zoom internally
                _lastMousePos = mousePos;
            }
            else
            {
                _isDragging = false;
            }
        }
    }

    private void HandleKeyboardInput()
    {
        var io = ImGui.GetIO();

        // Don't process if typing in a text field
        if (io.WantTextInput) return;

        // View controls
        if (ImGui.IsKeyPressed(ImGuiKey.F))
        {
            ViewState.FitToMap();
        }
        if (ImGui.IsKeyPressed(ImGuiKey.R))
        {
            ViewState.ResetCamera();
        }

        // Layer toggles (number keys)
        if (ImGui.IsKeyPressed(ImGuiKey._1))
        {
            ViewState.ToggleLayerVisibility(LayerType.Minimap);
        }
        if (ImGui.IsKeyPressed(ImGuiKey._2))
        {
            ViewState.ToggleLayerVisibility(LayerType.Height);
        }
        if (ImGui.IsKeyPressed(ImGuiKey._3))
        {
            ViewState.ToggleLayerVisibility(LayerType.Area);
        }

        // Layer toggles (letter keys)
        if (ImGui.IsKeyPressed(ImGuiKey.M))
        {
            ViewState.ToggleLayerVisibility(LayerType.Minimap);
        }
        if (ImGui.IsKeyPressed(ImGuiKey.H))
        {
            ViewState.ToggleLayerVisibility(LayerType.Height);
        }
        if (ImGui.IsKeyPressed(ImGuiKey.A))
        {
            ViewState.ToggleLayerVisibility(LayerType.Area);
        }

        // Zoom with + and -
        if (ImGui.IsKeyPressed(ImGuiKey.Equal) || ImGui.IsKeyPressed(ImGuiKey.KeypadAdd))
        {
            ViewState.Camera.Zoom *= 1.1f;  // Setter auto-clamps to min/max
        }
        if (ImGui.IsKeyPressed(ImGuiKey.Minus) || ImGui.IsKeyPressed(ImGuiKey.KeypadSubtract))
        {
            ViewState.Camera.Zoom *= 0.9f;  // Setter auto-clamps to min/max
        }

        // Pan with arrow keys - move in world units scaled by zoom for consistent feel
        float panSpeed = 5.0f;  // World units per key press
        if (ImGui.IsKeyDown(ImGuiKey.LeftArrow))
        {
            ViewState.Camera.Position -= new Vector2(panSpeed * io.DeltaTime * 60, 0);
        }
        if (ImGui.IsKeyDown(ImGuiKey.RightArrow))
        {
            ViewState.Camera.Position += new Vector2(panSpeed * io.DeltaTime * 60, 0);
        }
        if (ImGui.IsKeyDown(ImGuiKey.UpArrow))
        {
            ViewState.Camera.Position -= new Vector2(0, panSpeed * io.DeltaTime * 60);
        }
        if (ImGui.IsKeyDown(ImGuiKey.DownArrow))
        {
            ViewState.Camera.Position += new Vector2(0, panSpeed * io.DeltaTime * 60);
        }
    }

    private unsafe void RenderViewport(Vector2 contentPos, Vector2 contentSize)
    {
        if (_compositor == null || _streamingService == null || _quadCache == null) return;

        var gl = _app.GL;
        var drawList = ImGui.GetWindowDrawList();

        // Draw background
        drawList.AddRectFilled(contentPos, contentPos + contentSize, ImGui.ColorConvertFloat4ToU32(new Vector4(0.05f, 0.05f, 0.05f, 1.0f)));

        // Only render if we have a map loaded
        if (ViewState.CurrentWdtId == 0)
        {
            // Draw placeholder text
            var text = "Select a map from the browser";
            var textSize = ImGui.CalcTextSize(text);
            var textPos = contentPos + (contentSize - textSize) / 2;
            drawList.AddText(textPos, ImGui.ColorConvertFloat4ToU32(new Vector4(0.5f, 0.5f, 0.5f, 1.0f)), text);
            return;
        }

        // Update visibility using Camera2D and ensure quads are loaded
        var visibleQuads = _streamingService.UpdateVisibility(ViewState.Camera);

        // Use scissor rect to clip rendering to content area
        drawList.PushClipRect(contentPos, contentPos + contentSize, true);

        // Ensure viewport framebuffer exists
        int fboWidth = (int)contentSize.X;
        int fboHeight = (int)contentSize.Y;
        EnsureViewportFramebuffer(fboWidth, fboHeight);

        if (_viewportFramebuffer != 0)
        {
            // Save GL state
            Span<int> savedViewport = stackalloc int[4];
            gl.GetInteger(GLEnum.Viewport, savedViewport);
            Span<int> savedFb = stackalloc int[1];
            gl.GetInteger(GLEnum.DrawFramebufferBinding, savedFb);

            // Render to viewport framebuffer
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, _viewportFramebuffer);
            gl.Viewport(0, 0, (uint)_viewportFboWidth, (uint)_viewportFboHeight);
            gl.ClearColor(0.05f, 0.05f, 0.05f, 1.0f);
            gl.Clear(ClearBufferMask.ColorBufferBit);

            // Enable blending for proper layer compositing
            gl.Enable(EnableCap.Blend);
            gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            // Render each visible quad
            RenderQuads(visibleQuads);

            // Restore GL state
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, (uint)savedFb[0]);
            gl.Viewport(savedViewport[0], savedViewport[1], (uint)savedViewport[2], (uint)savedViewport[3]);

            // Draw composited result to ImGui
            drawList.AddImage(
                (nint)_viewportFramebufferTexture,
                contentPos,
                contentPos + contentSize,
                new Vector2(0, 1),  // Flip Y for OpenGL framebuffer
                new Vector2(1, 0),
                0xFFFFFFFF);
        }

        drawList.PopClipRect();
    }

    private void RenderQuads(HashSet<QuadCoord> visibleQuads)
    {
        if (_compositor == null || _quadCache == null) return;

        // Get view-projection matrix ONCE per frame from Camera2D
        var viewProjection = ViewState.Camera.GetViewProjectionMatrix();

        // Render quads in order (back to front, row by row)
        foreach (var coord in visibleQuads.OrderBy(q => q.Y).ThenBy(q => q.X))
        {
            var gpuQuad = _quadCache.GetGpuQuad(coord);
            if (gpuQuad == null || !gpuQuad.IsInitialized)
                continue;

            // Quad position in world space is simply its (x, y) coordinates
            // since each quad is 1x1 world units
            var quadWorldPos = new Vector2(coord.X, coord.Y);

            // Render the quad with world-space positioning
            _compositor.RenderQuad(
                gpuQuad,
                ViewState.Layers,
                viewProjection,
                quadWorldPos,
                QuadCoord.QuadWorldSize);
        }
    }

    private unsafe void EnsureViewportFramebuffer(int width, int height)
    {
        var gl = _app.GL;

        // Don't recreate if same size
        if (_viewportFramebuffer != 0 && _viewportFboWidth == width && _viewportFboHeight == height)
            return;

        // Delete existing
        if (_viewportFramebuffer != 0)
        {
            gl.DeleteFramebuffer(_viewportFramebuffer);
            gl.DeleteTexture(_viewportFramebufferTexture);
            _viewportFramebuffer = 0;
        }

        _viewportFboWidth = width;
        _viewportFboHeight = height;

        // Create texture for framebuffer
        _viewportFramebufferTexture = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, _viewportFramebufferTexture);
        gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8,
            (uint)width, (uint)height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, null);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

        // Create framebuffer
        _viewportFramebuffer = gl.GenFramebuffer();
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, _viewportFramebuffer);
        gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
            TextureTarget.Texture2D, _viewportFramebufferTexture, 0);

        // Check framebuffer completeness
        var status = gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (status != GLEnum.FramebufferComplete)
        {
            Console.WriteLine($"Viewport framebuffer incomplete: {status}");
            gl.DeleteFramebuffer(_viewportFramebuffer);
            gl.DeleteTexture(_viewportFramebufferTexture);
            _viewportFramebuffer = 0;
        }

        gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        gl.BindTexture(TextureTarget.Texture2D, 0);
    }

    private void RenderMapTooltip(Vector2 contentPos, Vector2 contentSize)
    {
        if (_loadingService == null) return;
        if (ViewState.CurrentWdtId == 0) return;

        // Don't show tooltips when modal popups are open
        if (ImGui.IsPopupOpen("Export Map") || ImGui.IsPopupOpen("Switch WoW Installation"))
            return;

        var io = ImGui.GetIO();
        var mousePos = io.MousePos;

        // Check if mouse is in content area
        bool inContent = mousePos.X >= contentPos.X && mousePos.X < contentPos.X + contentSize.X &&
                         mousePos.Y >= contentPos.Y && mousePos.Y < contentPos.Y + contentSize.Y;

        if (!inContent) return;

        // Convert mouse position to world coordinates using Camera2D
        var localMouse = mousePos - contentPos;
        var worldPos = ViewState.Camera.ScreenToWorld(localMouse);

        // Convert to tile coordinates
        int tileX = (int)Math.Floor(worldPos.X);
        int tileY = (int)Math.Floor(worldPos.Y);

        // Bounds check
        if (tileX < 0 || tileX >= QuadCoord.GridSize ||
            tileY < 0 || tileY >= QuadCoord.GridSize)
            return;

        // Convert to pixel coordinates
        int pixelX = (int)(worldPos.X * Camera2D.PixelsPerTile);
        int pixelY = (int)(worldPos.Y * Camera2D.PixelsPerTile);

        pixelX = Math.Clamp(pixelX, 0, ViewState.MapSizePixels - 1);
        pixelY = Math.Clamp(pixelY, 0, ViewState.MapSizePixels - 1);

        // Check if we have any data for this tile
        bool hasAreaData = _loadingService.AreaIdMap.ContainsKey((tileX, tileY));
        bool hasHeightData = _loadingService.HeightMap.ContainsKey((tileX, tileY));

        if (!hasAreaData && !hasHeightData)
            return;

        // Get area info
        uint areaId = _loadingService.GetAreaIdAtPixel(pixelX, pixelY);
        string? areaName = areaId > 0 ? _app.Db2Service?.GetAreaName(areaId) : null;

        // Get height info
        _loadingService.TryGetHeightAtPixel(pixelX, pixelY, out float height);

        // Get cell (chunk) coordinates within the ADT
        var (cellX, cellY) = MapLoadingService.GetCellCoords(pixelX, pixelY);

        // Get world coordinates
        var (gameWorldX, gameWorldY) = MapLoadingService.PixelToWorldCoords(pixelX, pixelY);

        // Show tooltip with combined info
        ImGui.BeginTooltip();

        // Area name (prominent)
        if (!string.IsNullOrEmpty(areaName))
        {
            ImGui.Text(areaName);
        }
        else if (areaId > 0)
        {
            ImGui.Text("Unknown Area");
        }

        // Area ID
        if (areaId > 0)
        {
            ImGui.TextDisabled($"Area ID: {areaId}");
        }

        ImGui.Separator();

        // Coordinates section
        ImGui.TextDisabled($"ADT: [{tileX}, {tileY}]  Cell: [{cellX}, {cellY}]");
        ImGui.TextDisabled($"World: ({gameWorldX:F1}, {gameWorldY:F1})");

        // Height
        if (hasHeightData)
        {
            ImGui.TextDisabled($"Height: {height:F2}");
        }

        ImGui.EndTooltip();
    }

    public void Dispose()
    {
        if (_disposed) return;

        _loadingService?.Dispose();
        _compositor?.Dispose();
        _quadCache?.Dispose();

        // Clean up viewport framebuffer
        if (_viewportFramebuffer != 0)
        {
            try
            {
                _app.GL?.DeleteFramebuffer(_viewportFramebuffer);
                _app.GL?.DeleteTexture(_viewportFramebufferTexture);
            }
            catch
            {
                // GL context may be gone
            }
            _viewportFramebuffer = 0;
        }

        _disposed = true;
    }
}
