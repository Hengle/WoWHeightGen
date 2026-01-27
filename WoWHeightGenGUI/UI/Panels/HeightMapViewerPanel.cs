using ImGuiNET;
using Silk.NET.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using WoWHeightGenGUI.App;
using WoWHeightGenGUI.Rendering;
using WoWHeightGenLib.Models;
using WoWHeightGenLib.Configuration;

namespace WoWHeightGenGUI.UI.Panels;

public class HeightMapViewerPanel : IPanel
{
    private readonly Application _app;

    public string Name => "Height Map Viewer";
    private bool _isVisible = true;
    public bool IsVisible { get => _isVisible; set => _isVisible = value; }

    private uint _currentWdtId;
    private uint _glTextureId;
    private int _textureWidth;
    private int _textureHeight;
    private string? _errorMessage;
    private bool _isLoading;

    private bool _clampAboveSea;
    private bool _clampBelowSea;
    private float _minHeight;
    private float _maxHeight;

    private ImageViewport _viewport = new();
    private string _wdtIdInput = "";

    public HeightMapViewerPanel(Application app)
    {
        _app = app;
    }

    public void LoadWdt(uint wdtFileId)
    {
        _wdtIdInput = wdtFileId.ToString();
        GenerateHeightMap();
    }

    public void Update(float deltaTime)
    {
    }

    public void Render()
    {
        if (ImGui.Begin(Name, ref _isVisible))
        {
            if (_app.Context == null)
            {
                ImGui.TextColored(new System.Numerics.Vector4(1, 0.5f, 0.5f, 1),
                    "Not connected to WoW installation.");
            }
            else
            {
                RenderToolbar();
                ImGui.Separator();

                if (_isLoading)
                {
                    ImGui.Text("Generating height map...");
                }
                else if (!string.IsNullOrEmpty(_errorMessage))
                {
                    ImGui.TextColored(new System.Numerics.Vector4(1, 0.5f, 0.5f, 1), _errorMessage);
                }
                else if (_glTextureId != 0)
                {
                    RenderHeightMapView();
                }
                else
                {
                    ImGui.TextDisabled("Enter a WDT FileDataID and click Generate.");
                }
            }
        }
        ImGui.End();
    }

    private void RenderToolbar()
    {
        ImGui.Text("WDT FileDataID:");
        ImGui.SameLine();

        ImGui.SetNextItemWidth(120);
        ImGui.InputText("##wdtId", ref _wdtIdInput, 20, ImGuiInputTextFlags.CharsDecimal);

        ImGui.SameLine();
        if (ImGui.Button("Generate"))
        {
            GenerateHeightMap();
        }

        // Options
        ImGui.SameLine();
        ImGui.Spacing();
        ImGui.SameLine();

        if (ImGui.Checkbox("Above Sea", ref _clampAboveSea))
        {
            if (_clampAboveSea) _clampBelowSea = false;
            if (_currentWdtId != 0) GenerateHeightMap();
        }

        ImGui.SameLine();
        if (ImGui.Checkbox("Below Sea", ref _clampBelowSea))
        {
            if (_clampBelowSea) _clampAboveSea = false;
            if (_currentWdtId != 0) GenerateHeightMap();
        }

        // Zoom controls
        ImGui.SameLine();
        ImGui.Spacing();
        ImGui.SameLine();

        if (ImGui.Button("Fit"))
        {
            var contentRegion = ImGui.GetContentRegionAvail();
            _viewport.ViewportSize = contentRegion;
            _viewport.FitToViewport(new System.Numerics.Vector2(_textureWidth, _textureHeight));
        }

        ImGui.SameLine();
        if (ImGui.Button("1:1"))
        {
            _viewport.Zoom = 1.0f;
            _viewport.Pan = System.Numerics.Vector2.Zero;
        }

        ImGui.SameLine();
        ImGui.Text($"Zoom: {_viewport.Zoom:P0}");

        if (_currentWdtId != 0)
        {
            ImGui.SameLine();
            ImGui.TextDisabled($"| Height: {_minHeight:F0} to {_maxHeight:F0}");
        }
    }

    private void GenerateHeightMap()
    {
        _errorMessage = null;

        if (!uint.TryParse(_wdtIdInput, out var wdtId))
        {
            _errorMessage = "Invalid WDT FileDataID";
            return;
        }

        if (_app.Context == null)
        {
            _errorMessage = "Not connected";
            return;
        }

        if (!_app.Context.FileExists(wdtId))
        {
            _errorMessage = $"WDT file {wdtId} not found";
            return;
        }

        _isLoading = true;

        try
        {
            // Clean up old texture
            if (_glTextureId != 0)
            {
                _app.GL.DeleteTexture(_glTextureId);
                _glTextureId = 0;
            }

            // Load WDT
            using var wdtStream = _app.Context.OpenFile(wdtId);
            var wdt = new Wdt(wdtStream);

            var config = _app.Context.Config;
            var mapSize = config.MapSize;
            var chunkRes = config.HeightChunkResolution;

            // Load all ADTs
            var adts = new Adt?[mapSize, mapSize];
            _minHeight = float.MaxValue;
            _maxHeight = float.MinValue;

            for (int y = 0; y < mapSize; y++)
            {
                for (int x = 0; x < mapSize; x++)
                {
                    var adtFileId = wdt.fileInfo[x, y].rootADT;
                    if (adtFileId == 0 || !_app.Context.FileExists(adtFileId))
                        continue;

                    try
                    {
                        using var adtStream = _app.Context.OpenFile(adtFileId);
                        var adt = new Adt(adtStream);
                        adts[x, y] = adt;

                        if (adt.minHeight < _minHeight) _minHeight = adt.minHeight;
                        if (adt.maxHeight > _maxHeight) _maxHeight = adt.maxHeight;
                    }
                    catch
                    {
                        // Skip failed ADTs
                    }
                }
            }

            // Apply sea level clamping
            float minH = _minHeight;
            float maxH = _maxHeight;

            if (_clampAboveSea)
            {
                minH = Math.Max(minH, 0);
            }
            else if (_clampBelowSea)
            {
                maxH = Math.Min(maxH, 0);
            }

            if (maxH <= minH)
            {
                maxH = minH + 1;
            }

            // Generate height map image
            _textureWidth = chunkRes * mapSize;
            _textureHeight = chunkRes * mapSize;

            byte[] pixels = new byte[_textureWidth * _textureHeight * 4];

            for (int y = 0; y < mapSize; y++)
            {
                for (int x = 0; x < mapSize; x++)
                {
                    var adt = adts[x, y];
                    if (adt == null) continue;

                    for (int cy = 0; cy < chunkRes; cy++)
                    {
                        for (int cx = 0; cx < chunkRes; cx++)
                        {
                            float height = adt.heightmap[cx, cy];

                            if (_clampAboveSea && height < 0) height = 0;
                            if (_clampBelowSea && height > 0) height = 0;

                            float normalized = (height - minH) / (maxH - minH);
                            normalized = Math.Clamp(normalized, 0, 1);
                            byte value = (byte)(normalized * 255);

                            int px = x * chunkRes + cx;
                            int py = y * chunkRes + cy;
                            int idx = (py * _textureWidth + px) * 4;

                            pixels[idx] = value;     // R
                            pixels[idx + 1] = value; // G
                            pixels[idx + 2] = value; // B
                            pixels[idx + 3] = 255;   // A
                        }
                    }
                }
            }

            // Upload to OpenGL
            _glTextureId = _app.GL.GenTexture();
            _app.GL.BindTexture(TextureTarget.Texture2D, _glTextureId);

            unsafe
            {
                fixed (byte* ptr = pixels)
                {
                    _app.GL.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8,
                        (uint)_textureWidth, (uint)_textureHeight, 0,
                        PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
                }
            }

            _app.GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            _app.GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            _app.GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            _app.GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            _app.GL.BindTexture(TextureTarget.Texture2D, 0);

            _currentWdtId = wdtId;
            _viewport.FitToViewport(new System.Numerics.Vector2(_textureWidth, _textureHeight));

            // Add to recent
            _app.RecentFiles.AddEntry(wdtId, Configuration.FileType.HeightMap,
                _app.Settings.Settings.WowProduct ?? "unknown", $"Height Map: {wdtId}");
        }
        catch (Exception ex)
        {
            _errorMessage = $"Error: {ex.Message}";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void RenderHeightMapView()
    {
        var contentRegion = ImGui.GetContentRegionAvail();
        _viewport.ViewportSize = contentRegion;

        ImGui.BeginChild("HeightMapViewport", contentRegion, ImGuiChildFlags.None, ImGuiWindowFlags.NoScrollbar);

        var displaySize = new System.Numerics.Vector2(_textureWidth, _textureHeight) * _viewport.Zoom;
        var center = contentRegion / 2 + _viewport.Pan;
        var imagePos = center - displaySize / 2;

        var drawList = ImGui.GetWindowDrawList();
        var screenPos = ImGui.GetCursorScreenPos();

        drawList.AddImage(
            (nint)_glTextureId,
            screenPos + imagePos,
            screenPos + imagePos + displaySize,
            new System.Numerics.Vector2(0, 0),
            new System.Numerics.Vector2(1, 1));

        // Handle input
        if (ImGui.IsWindowHovered())
        {
            var io = ImGui.GetIO();

            if (io.MouseWheel != 0)
            {
                var mousePos = io.MousePos - screenPos;
                _viewport.HandleMouseWheel(io.MouseWheel, mousePos);
            }

            if (ImGui.IsMouseDragging(ImGuiMouseButton.Middle) ||
                (ImGui.IsMouseDragging(ImGuiMouseButton.Left) && io.KeyAlt))
            {
                _viewport.HandleMouseDrag(io.MouseDelta);
            }
        }

        ImGui.EndChild();
    }

    public void Dispose()
    {
        if (_glTextureId != 0)
        {
            try
            {
                _app.GL.DeleteTexture(_glTextureId);
            }
            catch
            {
                // GL context may already be destroyed during shutdown
            }
            _glTextureId = 0;
        }
    }
}
