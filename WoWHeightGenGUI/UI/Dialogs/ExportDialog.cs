using System.Numerics;
using System.Text.RegularExpressions;
using ImGuiNET;
using SereniaBLPLib;
using Silk.NET.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using WoWHeightGenGUI.Models;
using WoWHeightGenGUI.Rendering;
using WoWHeightGenGUI.Services;
using WoWHeightGenLib.Models;
using WoWHeightGenLib.Services;

namespace WoWHeightGenGUI.UI.Dialogs;

/// <summary>
/// Export dialog for saving map images with various options.
/// </summary>
public class ExportDialog
{
    private readonly GL _gl;

    // Dialog state
    private bool _isOpen;
    private int _exportTypeIndex;      // 0=Composed, 1=Minimap, 2=Height, 3=Area, 4=All
    private bool _cropToData = true;   // true=Cropped to visible data
    private int _scalePercent = 100;   // 10-100
    private int _formatIndex;          // 0=PNG, 1=JPEG, 2=BMP, 3=WebP
    private int _jpegQuality = 90;     // 1-100
    private string _outputFolder = "";
    private string _fileName = "";
    private string? _errorMessage;
    private string? _successMessage;
    private bool _isExporting;
    private float _exportProgress;

    // Context from MapViewportPanel
    private MapViewState? _viewState;
    private MapLoadingService? _loadingService;
    private MapGenerationContext? _mapContext;
    private LayerCompositor? _compositor;
    private QuadCache? _quadCache;
    private QuadStreamingService? _streamingService;

    // Cached images assembled from quads (built on main thread, used by background export)
    private Image<Rgba32>? _cachedMinimapImage;
    private Image<Rgba32>? _cachedHeightImage;
    private Image<Rgba32>? _cachedAreaImage;

    private static readonly string[] ExportTypeOptions = { "Composed Image", "Minimap Only", "Height Only", "Area Only", "All Layers" };
    private static readonly string[] FormatOptions = { "PNG", "JPEG", "BMP", "WebP" };
    private static readonly string[] FormatExtensions = { ".png", ".jpg", ".bmp", ".webp" };

    public bool IsOpen => _isOpen;

    public ExportDialog(GL gl)
    {
        _gl = gl;
        _outputFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
    }

    /// <summary>
    /// Open the export dialog with the current map context (quad-based)
    /// </summary>
    public void Open(
        MapViewState viewState,
        MapLoadingService? loadingService,
        MapGenerationContext? mapContext,
        LayerCompositor? compositor,
        QuadCache? quadCache,
        QuadStreamingService? streamingService)
    {
        _viewState = viewState;
        _loadingService = loadingService;
        _mapContext = mapContext;
        _compositor = compositor;
        _quadCache = quadCache;
        _streamingService = streamingService;

        _errorMessage = null;
        _successMessage = null;
        _isExporting = false;
        _exportProgress = 0;

        // Generate initial filename
        UpdateFileName();

        _isOpen = true;
    }

    /// <summary>
    /// Close the export dialog
    /// </summary>
    public void Close()
    {
        _isOpen = false;
    }

    /// <summary>
    /// Render the export dialog as a modal popup
    /// </summary>
    public void Render()
    {
        if (!_isOpen) return;

        // Open the modal popup if not already open
        if (!ImGui.IsPopupOpen("Export Map"))
        {
            ImGui.OpenPopup("Export Map");
        }

        var viewport = ImGui.GetMainViewport();
        var windowSize = new Vector2(450, 480);
        var windowPos = viewport.WorkPos + (viewport.WorkSize - windowSize) / 2;

        ImGui.SetNextWindowPos(windowPos, ImGuiCond.Appearing);
        ImGui.SetNextWindowSize(windowSize, ImGuiCond.Always);

        var windowFlags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse;

        if (ImGui.BeginPopupModal("Export Map", ref _isOpen, windowFlags))
        {
            RenderExportOptions();
            ImGui.Separator();
            RenderOutputSettings();
            ImGui.Separator();
            RenderResolutionInfo();
            ImGui.Separator();
            RenderButtons();
            ImGui.EndPopup();
        }
    }

    private void RenderExportOptions()
    {
        ImGui.Text("Export Type:");
        ImGui.SetNextItemWidth(-1);
        if (ImGui.Combo("##ExportType", ref _exportTypeIndex, ExportTypeOptions, ExportTypeOptions.Length))
        {
            UpdateFileName();
        }

        ImGui.Spacing();
        ImGui.Text("Region:");

        bool fullMap = !_cropToData;
        if (ImGui.RadioButton("Full Map (64x64 tiles)", fullMap))
        {
            _cropToData = false;
            UpdateFileName();
        }

        if (ImGui.RadioButton("Visible Data Only (cropped)", _cropToData))
        {
            _cropToData = true;
            UpdateFileName();
        }

        ImGui.Spacing();
        ImGui.Text("Scale:");
        ImGui.SetNextItemWidth(-1);
        if (ImGui.SliderInt("##Scale", ref _scalePercent, 10, 100, "%d%%"))
        {
            UpdateFileName();
        }
    }

    private void RenderOutputSettings()
    {
        ImGui.Text("Format:");
        ImGui.SetNextItemWidth(150);
        if (ImGui.Combo("##Format", ref _formatIndex, FormatOptions, FormatOptions.Length))
        {
            UpdateFileName();
        }

        // JPEG quality slider (only visible for JPEG format)
        if (_formatIndex == 1)
        {
            ImGui.SameLine();
            ImGui.Text("Quality:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(100);
            ImGui.SliderInt("##JpegQuality", ref _jpegQuality, 1, 100);
        }

        ImGui.Spacing();
        ImGui.Text("Filename:");
        ImGui.SetNextItemWidth(-1);
        ImGui.InputText("##Filename", ref _fileName, 256);

        ImGui.Spacing();
        ImGui.Text("Output Folder:");
        ImGui.SetNextItemWidth(-80);
        ImGui.InputText("##OutputFolder", ref _outputFolder, 512);
        ImGui.SameLine();
        if (ImGui.Button("Browse"))
        {
            // Note: ImGui doesn't have a native folder picker
            // For now, users can type the path manually
            // In a full implementation, we'd use a native dialog
        }
    }

    private void RenderResolutionInfo()
    {
        var (width, height) = CalculateOutputResolution();

        ImGui.Text("Output Resolution:");
        ImGui.SameLine();
        ImGui.TextColored(new Vector4(0.5f, 0.8f, 1.0f, 1.0f), $"{width} x {height} px");

        if (_exportTypeIndex == 4) // All Layers
        {
            ImGui.TextDisabled("Will export 3 separate files");
        }

        // Show error or success message
        if (!string.IsNullOrEmpty(_errorMessage))
        {
            ImGui.Spacing();
            ImGui.TextColored(new Vector4(1, 0.4f, 0.4f, 1), _errorMessage);
        }

        if (!string.IsNullOrEmpty(_successMessage))
        {
            ImGui.Spacing();
            ImGui.TextColored(new Vector4(0.4f, 1, 0.4f, 1), _successMessage);
        }

        // Progress bar during export
        if (_isExporting)
        {
            ImGui.Spacing();
            ImGui.ProgressBar(_exportProgress, new Vector2(-1, 0), $"Exporting... {_exportProgress:P0}");
        }
    }

    private void RenderButtons()
    {
        ImGui.Spacing();

        var buttonWidth = 100;
        var totalWidth = buttonWidth * 2 + 10;
        var startX = (ImGui.GetContentRegionAvail().X - totalWidth) / 2;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + startX);

        ImGui.BeginDisabled(_isExporting);

        if (ImGui.Button("Cancel", new Vector2(buttonWidth, 30)))
        {
            Close();
        }

        ImGui.SameLine();

        // Orange export button
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1.0f, 0.55f, 0.0f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1.0f, 0.65f, 0.1f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.9f, 0.5f, 0.0f, 1.0f));

        if (ImGui.Button("Export", new Vector2(buttonWidth, 30)))
        {
            StartExport();
        }

        ImGui.PopStyleColor(3);
        ImGui.EndDisabled();
    }

    /// <summary>
    /// Calculate the output resolution based on current settings
    /// </summary>
    private (int width, int height) CalculateOutputResolution()
    {
        if (_viewState == null) return (0, 0);

        // Determine tile size based on export type
        // Use the actual texture tile size from ViewState (may be capped for large minimaps)
        int tileSize;
        switch (_exportTypeIndex)
        {
            case 0: // Composed - use actual minimap texture resolution
            case 1: // Minimap - use actual minimap texture resolution
                tileSize = _viewState.MinimapTileSizePixels > 0
                    ? _viewState.MinimapTileSizePixels
                    : (_loadingService?.MinimapTileSize ?? MapLoadingService.DefaultMinimapTileSize);
                break;
            case 2: // Height - fixed 128px
            case 3: // Area - fixed 128px
                tileSize = MapLoadingService.HeightTileSize;
                break;
            case 4: // All - show minimap resolution (largest)
                tileSize = _viewState.MinimapTileSizePixels > 0
                    ? _viewState.MinimapTileSizePixels
                    : (_loadingService?.MinimapTileSize ?? MapLoadingService.DefaultMinimapTileSize);
                break;
            default:
                tileSize = MapLoadingService.HeightTileSize;
                break;
        }

        int tilesX, tilesY;

        if (_cropToData && _viewState.HasTileBounds)
        {
            tilesX = _viewState.MaxTileX - _viewState.MinTileX + 1;
            tilesY = _viewState.MaxTileY - _viewState.MinTileY + 1;
        }
        else
        {
            tilesX = _viewState.TileCount;
            tilesY = _viewState.TileCount;
        }

        int baseWidth = tilesX * tileSize;
        int baseHeight = tilesY * tileSize;

        // Apply scale
        int scaledWidth = (int)(baseWidth * _scalePercent / 100.0);
        int scaledHeight = (int)(baseHeight * _scalePercent / 100.0);

        return (scaledWidth, scaledHeight);
    }

    /// <summary>
    /// Generate a filename based on current settings
    /// </summary>
    private void UpdateFileName()
    {
        if (_viewState?.CurrentMap == null) return;

        var mapName = SanitizeFileName(_viewState.CurrentMap.Name);
        var (width, height) = CalculateOutputResolution();

        string typeSuffix = _exportTypeIndex switch
        {
            0 => "composed",
            1 => "minimap",
            2 => "height",
            3 => "area",
            4 => "all",
            _ => "export"
        };

        string extension = FormatExtensions[_formatIndex];

        if (_exportTypeIndex == 4)
        {
            // For "All Layers", show base name (files will be _minimap, _height, _area)
            _fileName = $"{mapName}_{width}x{height}";
        }
        else
        {
            _fileName = $"{mapName}_{typeSuffix}_{width}x{height}{extension}";
        }
    }

    private static string SanitizeFileName(string name)
    {
        // Remove or replace invalid filename characters
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());
        // Replace spaces with underscores
        sanitized = sanitized.Replace(' ', '_');
        // Remove consecutive underscores
        sanitized = Regex.Replace(sanitized, "_+", "_");
        return sanitized.Trim('_');
    }

    /// <summary>
    /// Start the export operation
    /// </summary>
    private void StartExport()
    {
        _errorMessage = null;
        _successMessage = null;

        // Validate output path
        if (string.IsNullOrWhiteSpace(_outputFolder))
        {
            _errorMessage = "Please specify an output folder.";
            return;
        }

        if (!Directory.Exists(_outputFolder))
        {
            try
            {
                Directory.CreateDirectory(_outputFolder);
            }
            catch (Exception ex)
            {
                _errorMessage = $"Could not create folder: {ex.Message}";
                return;
            }
        }

        if (string.IsNullOrWhiteSpace(_fileName))
        {
            _errorMessage = "Please specify a filename.";
            return;
        }

        // Read texture data on main thread (OpenGL context requirement)
        try
        {
            CacheTextureData();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Export] Failed to read texture data: {ex}");
            _errorMessage = $"Failed to read texture data: {ex.Message}";
            return;
        }

        _isExporting = true;
        _exportProgress = 0;

        // Capture settings for background thread
        var exportType = _exportTypeIndex;
        var cropToData = _cropToData;
        var scalePercent = _scalePercent;
        var formatIndex = _formatIndex;
        var jpegQuality = _jpegQuality;
        var outputFolder = _outputFolder;
        var fileName = _fileName;
        var viewState = _viewState;
        var loadingService = _loadingService;
        var mapContext = _mapContext;

        // Run export on background thread
        Task.Run(() =>
        {
            try
            {
                if (exportType == 4) // All Layers
                {
                    ExportAllLayersBackground(viewState, loadingService, mapContext, cropToData, scalePercent, formatIndex, jpegQuality, outputFolder, fileName);
                }
                else
                {
                    ExportSingleImageBackground(viewState, loadingService, mapContext, exportType, cropToData, scalePercent, formatIndex, jpegQuality, outputFolder, fileName);
                }

                _successMessage = "Export completed successfully!";
            }
            catch (Exception ex)
            {
                _errorMessage = $"Export failed: {ex.Message}";
            }
            finally
            {
                _isExporting = false;
                _exportProgress = 1.0f;
                // Dispose cached images
                _cachedMinimapImage?.Dispose();
                _cachedMinimapImage = null;
                _cachedHeightImage?.Dispose();
                _cachedHeightImage = null;
                _cachedAreaImage?.Dispose();
                _cachedAreaImage = null;
            }
        });
    }

    /// <summary>
    /// Build cached images from quad data on the main thread (must be called before background export).
    /// Uses ImageSharp's DrawImage to composite tiles, avoiding manual byte array management.
    /// </summary>
    private void CacheTextureData()
    {
        if (_quadCache == null || _viewState == null) return;

        // Determine texture sizes
        int heightAreaTileSize = QuadCoord.HeightPixelsPerQuad; // 128 pixels per tile
        int minimapTileSize = _streamingService?.MinimapTileSize ?? 256;

        // Calculate output image dimensions
        int heightAreaSize = QuadCoord.GridSize * heightAreaTileSize; // 64 * 128 = 8192
        int minimapSize = QuadCoord.GridSize * minimapTileSize;       // 64 * tileSize

        Console.WriteLine($"[Export] Building images - HeightArea: {heightAreaSize}x{heightAreaSize}, Minimap: {minimapSize}x{minimapSize}");

        // Create output images
        _cachedHeightImage = new Image<Rgba32>(heightAreaSize, heightAreaSize);
        _cachedAreaImage = new Image<Rgba32>(heightAreaSize, heightAreaSize);
        _cachedMinimapImage = new Image<Rgba32>(minimapSize, minimapSize);

        // Composite tiles from quads
        for (int quadY = 0; quadY < QuadCoord.GridSize; quadY++)
        {
            for (int quadX = 0; quadX < QuadCoord.GridSize; quadX++)
            {
                var coord = new QuadCoord(quadX, quadY);
                var quadData = _quadCache.GetCpuQuad(coord);

                if (quadData == null) continue;

                // Draw height tile
                if (quadData.HeightPixels != null)
                {
                    using var tileImage = CreateImageFromPixels(quadData.HeightPixels, heightAreaTileSize, heightAreaTileSize);
                    var destPoint = new Point(quadX * heightAreaTileSize, quadY * heightAreaTileSize);
                    _cachedHeightImage.Mutate(ctx => ctx.DrawImage(tileImage, destPoint, 1f));
                }

                // Draw area tile
                if (quadData.AreaPixels != null)
                {
                    using var tileImage = CreateImageFromPixels(quadData.AreaPixels, heightAreaTileSize, heightAreaTileSize);
                    var destPoint = new Point(quadX * heightAreaTileSize, quadY * heightAreaTileSize);
                    _cachedAreaImage.Mutate(ctx => ctx.DrawImage(tileImage, destPoint, 1f));
                }

                // Draw minimap tile
                if (quadData.MinimapPixels != null && quadData.MinimapSize > 0)
                {
                    using var tileImage = CreateImageFromPixels(quadData.MinimapPixels, quadData.MinimapSize, quadData.MinimapSize);
                    var destPoint = new Point(quadX * minimapTileSize, quadY * minimapTileSize);

                    // Resize if tile size doesn't match expected minimap tile size
                    if (quadData.MinimapSize != minimapTileSize)
                    {
                        using var resized = tileImage.Clone(ctx => ctx.Resize(minimapTileSize, minimapTileSize));
                        _cachedMinimapImage.Mutate(ctx => ctx.DrawImage(resized, destPoint, 1f));
                    }
                    else
                    {
                        _cachedMinimapImage.Mutate(ctx => ctx.DrawImage(tileImage, destPoint, 1f));
                    }
                }
            }
        }

        Console.WriteLine("[Export] Image caching complete");
    }

    /// <summary>
    /// Create an Image from RGBA pixel data
    /// </summary>
    private static Image<Rgba32> CreateImageFromPixels(byte[] pixels, int width, int height)
    {
        var image = new Image<Rgba32>(width, height);
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < height; y++)
            {
                var rowSpan = accessor.GetRowSpan(y);
                int rowOffset = y * width * 4;
                for (int x = 0; x < width; x++)
                {
                    int pixelOffset = rowOffset + x * 4;
                    if (pixelOffset + 3 < pixels.Length)
                    {
                        rowSpan[x] = new Rgba32(
                            pixels[pixelOffset],
                            pixels[pixelOffset + 1],
                            pixels[pixelOffset + 2],
                            pixels[pixelOffset + 3]);
                    }
                }
            }
        });
        return image;
    }

    /// <summary>
    /// Export a single image (composed or individual layer) - called from background thread
    /// </summary>
    private void ExportSingleImageBackground(
        MapViewState? viewState,
        MapLoadingService? loadingService,
        MapGenerationContext? mapContext,
        int exportType,
        bool cropToData,
        int scalePercent,
        int formatIndex,
        int jpegQuality,
        string outputFolder,
        string fileName)
    {
        if (viewState == null) return;

        var (width, height) = CalculateOutputResolutionFor(viewState, loadingService, exportType, cropToData, scalePercent);

        _exportProgress = 0.1f;

        Image<Rgba32>? image = null;
        switch (exportType)
        {
            case 0: // Composed
                image = RenderComposedImageFromCache(viewState, width, height, cropToData);
                break;
            case 1: // Minimap
                image = RenderLayerImageFromCache(viewState, loadingService, LayerType.Minimap, width, height, cropToData);
                break;
            case 2: // Height
                image = RenderLayerImageFromCache(viewState, loadingService, LayerType.Height, width, height, cropToData);
                break;
            case 3: // Area
                image = RenderLayerImageFromCache(viewState, loadingService, LayerType.Area, width, height, cropToData);
                break;
        }

        _exportProgress = 0.6f;
        Console.WriteLine($"[Export] Rendered image: {image?.Width}x{image?.Height}");

        if (image == null)
        {
            throw new Exception("Failed to render image data.");
        }

        // Save image
        var filePath = Path.Combine(outputFolder, fileName);
        Console.WriteLine($"[Export] Saving to: {filePath}");
        _exportProgress = 0.7f;

        using (image)
        {
            SaveImage(image, filePath, formatIndex, jpegQuality);
        }

        Console.WriteLine("[Export] Save complete");
        _exportProgress = 1.0f;
    }

    /// <summary>
    /// Export all layers as separate files - called from background thread
    /// </summary>
    private void ExportAllLayersBackground(
        MapViewState? viewState,
        MapLoadingService? loadingService,
        MapGenerationContext? mapContext,
        bool cropToData,
        int scalePercent,
        int formatIndex,
        int jpegQuality,
        string outputFolder,
        string fileName)
    {
        if (viewState == null) return;

        string extension = FormatExtensions[formatIndex];
        var baseName = Path.GetFileNameWithoutExtension(fileName);

        // Export minimap
        _exportProgress = 0.1f;
        var minimapSize = CalculateLayerResolutionFor(viewState, loadingService, LayerType.Minimap, cropToData, scalePercent);
        using (var minimapImage = RenderLayerImageFromCache(viewState, loadingService, LayerType.Minimap, minimapSize.width, minimapSize.height, cropToData))
        {
            if (minimapImage != null)
            {
                SaveImage(minimapImage, Path.Combine(outputFolder, $"{baseName}_minimap{extension}"), formatIndex, jpegQuality);
            }
        }

        // Export height
        _exportProgress = 0.4f;
        var heightSize = CalculateLayerResolutionFor(viewState, loadingService, LayerType.Height, cropToData, scalePercent);
        using (var heightImage = RenderLayerImageFromCache(viewState, loadingService, LayerType.Height, heightSize.width, heightSize.height, cropToData))
        {
            if (heightImage != null)
            {
                SaveImage(heightImage, Path.Combine(outputFolder, $"{baseName}_height{extension}"), formatIndex, jpegQuality);
            }
        }

        // Export area
        _exportProgress = 0.7f;
        var areaSize = CalculateLayerResolutionFor(viewState, loadingService, LayerType.Area, cropToData, scalePercent);
        using (var areaImage = RenderLayerImageFromCache(viewState, loadingService, LayerType.Area, areaSize.width, areaSize.height, cropToData))
        {
            if (areaImage != null)
            {
                SaveImage(areaImage, Path.Combine(outputFolder, $"{baseName}_area{extension}"), formatIndex, jpegQuality);
            }
        }

        _exportProgress = 1.0f;
    }

    /// <summary>
    /// Calculate output resolution for given parameters
    /// </summary>
    private (int width, int height) CalculateOutputResolutionFor(
        MapViewState viewState,
        MapLoadingService? loadingService,
        int exportType,
        bool cropToData,
        int scalePercent)
    {
        // Determine tile size based on export type
        // Use actual texture size from ViewState (may be capped)
        int tileSize;
        switch (exportType)
        {
            case 0: // Composed - use actual minimap texture resolution
            case 1: // Minimap - use actual minimap texture resolution
                tileSize = viewState.MinimapTileSizePixels > 0
                    ? viewState.MinimapTileSizePixels
                    : (loadingService?.MinimapTileSize ?? MapLoadingService.DefaultMinimapTileSize);
                break;
            case 2: // Height - fixed 128px
            case 3: // Area - fixed 128px
                tileSize = MapLoadingService.HeightTileSize;
                break;
            default:
                tileSize = MapLoadingService.HeightTileSize;
                break;
        }

        int tilesX, tilesY;

        if (cropToData && viewState.HasTileBounds)
        {
            tilesX = viewState.MaxTileX - viewState.MinTileX + 1;
            tilesY = viewState.MaxTileY - viewState.MinTileY + 1;
        }
        else
        {
            tilesX = viewState.TileCount;
            tilesY = viewState.TileCount;
        }

        int baseWidth = tilesX * tileSize;
        int baseHeight = tilesY * tileSize;

        int scaledWidth = (int)(baseWidth * scalePercent / 100.0);
        int scaledHeight = (int)(baseHeight * scalePercent / 100.0);

        return (scaledWidth, scaledHeight);
    }

    /// <summary>
    /// Calculate layer resolution for given parameters
    /// </summary>
    private (int width, int height) CalculateLayerResolutionFor(
        MapViewState viewState,
        MapLoadingService? loadingService,
        LayerType layer,
        bool cropToData,
        int scalePercent)
    {
        // Minimap uses actual texture resolution (may be capped), Height/Area use 128px
        int tileSize = layer == LayerType.Minimap
            ? (viewState.MinimapTileSizePixels > 0
                ? viewState.MinimapTileSizePixels
                : (loadingService?.MinimapTileSize ?? MapLoadingService.DefaultMinimapTileSize))
            : MapLoadingService.HeightTileSize;

        int tilesX, tilesY;
        if (cropToData && viewState.HasTileBounds)
        {
            tilesX = viewState.MaxTileX - viewState.MinTileX + 1;
            tilesY = viewState.MaxTileY - viewState.MinTileY + 1;
        }
        else
        {
            tilesX = viewState.TileCount;
            tilesY = viewState.TileCount;
        }

        int baseWidth = tilesX * tileSize;
        int baseHeight = tilesY * tileSize;

        int scaledWidth = (int)(baseWidth * scalePercent / 100.0);
        int scaledHeight = (int)(baseHeight * scalePercent / 100.0);

        return (scaledWidth, scaledHeight);
    }

    /// <summary>
    /// Render the composed image from cached images (background thread safe)
    /// </summary>
    private Image<Rgba32>? RenderComposedImageFromCache(MapViewState viewState, int width, int height, bool cropToData)
    {
        if (_cachedMinimapImage == null && _cachedHeightImage == null && _cachedAreaImage == null)
            return null;

        // Use minimap resolution as base (highest resolution), fall back to height/area
        int sourceWidth = _cachedMinimapImage?.Width ?? _cachedHeightImage?.Width ?? 0;
        int sourceHeight = _cachedMinimapImage?.Height ?? _cachedHeightImage?.Height ?? 0;

        if (sourceWidth == 0 || sourceHeight == 0) return null;

        // Calculate crop region if cropping
        Rectangle? cropRegion = null;
        int resultWidth = sourceWidth;
        int resultHeight = sourceHeight;

        if (cropToData && viewState.HasTileBounds)
        {
            int texTileSize = sourceWidth / viewState.TileCount;
            int srcX = viewState.MinTileX * texTileSize;
            int srcY = viewState.MinTileY * texTileSize;
            int srcW = (viewState.MaxTileX - viewState.MinTileX + 1) * texTileSize;
            int srcH = (viewState.MaxTileY - viewState.MinTileY + 1) * texTileSize;
            cropRegion = new Rectangle(srcX, srcY, srcW, srcH);
            resultWidth = srcW;
            resultHeight = srcH;
        }

        Console.WriteLine($"[Export] Composing image: {resultWidth}x{resultHeight} (crop: {cropRegion != null})");

        // Create result image at cropped size (or full size if not cropping)
        var result = new Image<Rgba32>(resultWidth, resultHeight);

        // Composite each visible layer
        foreach (var layerType in new[] { LayerType.Minimap, LayerType.Height, LayerType.Area })
        {
            var state = viewState.GetLayer(layerType);
            if (!state.IsVisible || state.Opacity <= 0) continue;

            Image<Rgba32>? layerImage = layerType switch
            {
                LayerType.Minimap => _cachedMinimapImage,
                LayerType.Height => _cachedHeightImage,
                LayerType.Area => _cachedAreaImage,
                _ => null
            };

            if (layerImage == null) continue;

            Console.WriteLine($"[Export] Processing layer: {layerType}");

            // Clone the layer (or just the crop region if cropping)
            Image<Rgba32> processedLayer;
            if (cropRegion != null)
            {
                // For different resolution layers, calculate the equivalent crop region
                int layerTileSize = layerImage.Width / viewState.TileCount;
                var layerCrop = new Rectangle(
                    viewState.MinTileX * layerTileSize,
                    viewState.MinTileY * layerTileSize,
                    (viewState.MaxTileX - viewState.MinTileX + 1) * layerTileSize,
                    (viewState.MaxTileY - viewState.MinTileY + 1) * layerTileSize);
                processedLayer = layerImage.Clone(ctx => ctx.Crop(layerCrop));
            }
            else
            {
                processedLayer = layerImage.Clone();
            }

            using (processedLayer)
            {
                // Apply colormap for height layer
                if (layerType == LayerType.Height)
                {
                    ApplyColormapToImage(processedLayer, viewState.GetLayer(LayerType.Height).HeightColormap);
                }

                // Scale layer to match result size if different
                if (processedLayer.Width != resultWidth || processedLayer.Height != resultHeight)
                {
                    processedLayer.Mutate(ctx => ctx.Resize(resultWidth, resultHeight));
                }

                // Apply opacity and blend
                if (state.Opacity < 1.0f)
                {
                    processedLayer.Mutate(ctx => ctx.Opacity(state.Opacity));
                }

                result.Mutate(ctx => ctx.DrawImage(processedLayer, new Point(0, 0), 1f));
            }
        }

        // Scale to output size if needed (crop was already applied during layer extraction)
        if (result.Width != width || result.Height != height)
        {
            Console.WriteLine($"[Export] Final resize: {result.Width}x{result.Height} -> {width}x{height}");
            result.Mutate(ctx => ctx.Resize(width, height));
        }

        Console.WriteLine("[Export] Composition complete");
        return result;
    }

    /// <summary>
    /// Render a single layer from cached images (background thread safe)
    /// </summary>
    private Image<Rgba32>? RenderLayerImageFromCache(MapViewState viewState, MapLoadingService? loadingService, LayerType layer, int width, int height, bool cropToData)
    {
        Image<Rgba32>? sourceImage = layer switch
        {
            LayerType.Minimap => _cachedMinimapImage,
            LayerType.Height => _cachedHeightImage,
            LayerType.Area => _cachedAreaImage,
            _ => null
        };

        if (sourceImage == null) return null;

        Console.WriteLine($"[Export] Processing {layer} layer: {sourceImage.Width}x{sourceImage.Height} -> {width}x{height}");

        Image<Rgba32> result;

        // If cropping, extract just the region we need instead of cloning the whole image
        if (cropToData && viewState.HasTileBounds)
        {
            int texTileSize = sourceImage.Width / viewState.TileCount;
            int srcX = viewState.MinTileX * texTileSize;
            int srcY = viewState.MinTileY * texTileSize;
            int srcW = (viewState.MaxTileX - viewState.MinTileX + 1) * texTileSize;
            int srcH = (viewState.MaxTileY - viewState.MinTileY + 1) * texTileSize;

            Console.WriteLine($"[Export] Cropping to region: ({srcX},{srcY}) {srcW}x{srcH}");

            // Clone only the cropped region
            result = sourceImage.Clone(ctx => ctx.Crop(new Rectangle(srcX, srcY, srcW, srcH)));
        }
        else
        {
            // Clone the full image
            result = sourceImage.Clone();
        }

        // Apply colormap for height layer
        if (layer == LayerType.Height)
        {
            Console.WriteLine("[Export] Applying colormap...");
            ApplyColormapToImage(result, viewState.GetLayer(LayerType.Height).HeightColormap);
        }

        // Scale to output size if needed (crop was already applied above)
        if (result.Width != width || result.Height != height)
        {
            Console.WriteLine($"[Export] Resizing from {result.Width}x{result.Height} to {width}x{height}");
            result.Mutate(ctx => ctx.Resize(width, height));
        }

        Console.WriteLine("[Export] Layer processing complete");
        return result;
    }

    /// <summary>
    /// Apply colormap to an image in-place
    /// </summary>
    private void ApplyColormapToImage(Image<Rgba32> image, ColormapType colormapType)
    {
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                var rowSpan = accessor.GetRowSpan(y);
                for (int x = 0; x < rowSpan.Length; x++)
                {
                    var pixel = rowSpan[x];
                    float normalized = pixel.R / 255.0f; // Height is in R channel
                    var color = GetColormapColor(colormapType, normalized);
                    rowSpan[x] = new Rgba32(color.r, color.g, color.b, pixel.A);
                }
            }
        });
    }

    /// <summary>
    /// Get color from colormap at normalized position
    /// </summary>
    private (byte r, byte g, byte b) GetColormapColor(ColormapType type, float t)
    {
        t = Math.Clamp(t, 0, 1);

        return type switch
        {
            ColormapType.Grayscale => GrayscaleColormap(t),
            ColormapType.Terrain => TerrainColormap(t),
            ColormapType.Viridis => ViridisColormap(t),
            ColormapType.Heatmap => HeatmapColormap(t),
            _ => GrayscaleColormap(t)
        };
    }

    private static (byte r, byte g, byte b) GrayscaleColormap(float t)
    {
        byte v = (byte)(t * 255);
        return (v, v, v);
    }

    private static (byte r, byte g, byte b) TerrainColormap(float t)
    {
        // Blue -> Green -> Brown -> White
        if (t < 0.25f)
        {
            float lt = t / 0.25f;
            return (
                (byte)(0 + lt * 34),
                (byte)(0 + lt * 139),
                (byte)(139 + lt * (34 - 139))
            );
        }
        else if (t < 0.5f)
        {
            float lt = (t - 0.25f) / 0.25f;
            return (
                (byte)(34 + lt * (139 - 34)),
                (byte)(139 + lt * (69 - 139)),
                (byte)(34 + lt * (19 - 34))
            );
        }
        else if (t < 0.75f)
        {
            float lt = (t - 0.5f) / 0.25f;
            return (
                (byte)(139 + lt * (210 - 139)),
                (byte)(69 + lt * (180 - 69)),
                (byte)(19 + lt * (140 - 19))
            );
        }
        else
        {
            float lt = (t - 0.75f) / 0.25f;
            return (
                (byte)(210 + lt * (255 - 210)),
                (byte)(180 + lt * (250 - 180)),
                (byte)(140 + lt * (250 - 140))
            );
        }
    }

    private static (byte r, byte g, byte b) ViridisColormap(float t)
    {
        // Simplified Viridis approximation
        float r = Math.Clamp(0.267004f + t * (0.993248f - 0.267004f), 0, 1);
        float g = Math.Clamp(0.004874f + t * (0.906157f - 0.004874f), 0, 1);
        float b = Math.Clamp(0.329415f + (1 - t) * (0.329415f - 0.143936f), 0, 1);

        if (t < 0.5f)
        {
            r = 0.267004f * (1 - t * 2) + 0.127568f * t * 2;
            g = 0.004874f * (1 - t * 2) + 0.566949f * t * 2;
            b = 0.329415f * (1 - t * 2) + 0.550556f * t * 2;
        }
        else
        {
            float lt = (t - 0.5f) * 2;
            r = 0.127568f * (1 - lt) + 0.993248f * lt;
            g = 0.566949f * (1 - lt) + 0.906157f * lt;
            b = 0.550556f * (1 - lt) + 0.143936f * lt;
        }

        return ((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
    }

    private static (byte r, byte g, byte b) HeatmapColormap(float t)
    {
        // Blue -> Cyan -> Green -> Yellow -> Red
        if (t < 0.25f)
        {
            float lt = t / 0.25f;
            return ((byte)0, (byte)(lt * 255), (byte)255);
        }
        else if (t < 0.5f)
        {
            float lt = (t - 0.25f) / 0.25f;
            return ((byte)0, (byte)255, (byte)((1 - lt) * 255));
        }
        else if (t < 0.75f)
        {
            float lt = (t - 0.5f) / 0.25f;
            return ((byte)(lt * 255), (byte)255, (byte)0);
        }
        else
        {
            float lt = (t - 0.75f) / 0.25f;
            return ((byte)255, (byte)((1 - lt) * 255), (byte)0);
        }
    }

    /// <summary>
    /// Save image to file in the specified format (background thread safe)
    /// </summary>
    private static void SaveImage(Image<Rgba32> image, string filePath, int formatIndex, int jpegQuality)
    {
        switch (formatIndex)
        {
            case 0: // PNG
                image.SaveAsPng(filePath, new PngEncoder());
                break;
            case 1: // JPEG
                image.SaveAsJpeg(filePath, new JpegEncoder { Quality = jpegQuality });
                break;
            case 2: // BMP
                image.SaveAsBmp(filePath, new BmpEncoder());
                break;
            case 3: // WebP
                image.SaveAsWebp(filePath, new WebpEncoder());
                break;
            default:
                image.SaveAsPng(filePath);
                break;
        }
    }
}
