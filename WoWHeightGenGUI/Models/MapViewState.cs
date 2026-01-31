using System.Numerics;
using WoWHeightGenGUI.Rendering;
using WoWHeightGenGUI.Services;

namespace WoWHeightGenGUI.Models;

// Forward reference for HeightStats (defined in TileUpdate.cs)

/// <summary>
/// Represents the complete state of the map viewport including the current map,
/// all layer states, selected layer, and camera transform.
/// </summary>
public class MapViewState
{
    /// <summary>
    /// Number of layers in the system
    /// </summary>
    public const int LayerCount = 3;

    /// <summary>
    /// The WDT FileDataID of the currently loaded map, or 0 if none
    /// </summary>
    public uint CurrentWdtId { get; set; }

    /// <summary>
    /// The currently loaded map entry, or null if none
    /// </summary>
    public MapEntry? CurrentMap { get; set; }

    /// <summary>
    /// State for each layer, indexed by LayerType
    /// </summary>
    public LayerState[] Layers { get; } = new LayerState[LayerCount]
    {
        new(LayerType.Minimap),
        new(LayerType.Height),
        new(LayerType.Area)
    };

    /// <summary>
    /// Layer rendering order (indices into Layers array, bottom to top).
    /// Default: [0, 1, 2] meaning Minimap -> Height -> Area
    /// </summary>
    public int[] LayerOrder { get; private set; } = { 0, 1, 2 };

    /// <summary>
    /// Index of the currently selected layer (for properties panel)
    /// </summary>
    public int SelectedLayerIndex { get; set; } = (int)LayerType.Height;

    /// <summary>
    /// The currently selected layer
    /// </summary>
    public LayerState SelectedLayer => Layers[SelectedLayerIndex];

    /// <summary>
    /// Camera for viewport navigation (pan/zoom in world space)
    /// </summary>
    public Camera2D Camera { get; } = new();

    /// <summary>
    /// Whether a map is currently being loaded
    /// </summary>
    public bool IsLoading { get; set; }

    /// <summary>
    /// Loading progress from 0 to 1
    /// </summary>
    public float LoadingProgress { get; set; }

    /// <summary>
    /// Number of tiles in each dimension (always 64)
    /// </summary>
    public int TileCount { get; set; } = QuadCoord.GridSize;

    /// <summary>
    /// Size of each tile in pixels for height/area (always 128)
    /// </summary>
    public int TileSizePixels { get; set; } = QuadCoord.HeightPixelsPerQuad;

    /// <summary>
    /// Total size of the map in pixels for height/area layers (64 * 128 = 8192)
    /// </summary>
    public int MapSizePixels => TileCount * TileSizePixels;

    /// <summary>
    /// Size of each minimap tile in pixels (typically 256-512, detected from BLP files)
    /// </summary>
    public int MinimapTileSizePixels { get; set; } = 256;

    /// <summary>
    /// Total size of the minimap in pixels (64 * minimap_tile_size)
    /// </summary>
    public int MinimapSizePixels => TileCount * MinimapTileSizePixels;

    /// <summary>
    /// Global height statistics for the current map (min/max height values)
    /// </summary>
    public HeightStats? HeightStats { get; set; }

    // Tile bounds tracking - actual extent of loaded tiles
    /// <summary>
    /// Minimum X coordinate of loaded tiles
    /// </summary>
    public int MinTileX { get; set; } = int.MaxValue;

    /// <summary>
    /// Maximum X coordinate of loaded tiles
    /// </summary>
    public int MaxTileX { get; set; } = int.MinValue;

    /// <summary>
    /// Minimum Y coordinate of loaded tiles
    /// </summary>
    public int MinTileY { get; set; } = int.MaxValue;

    /// <summary>
    /// Maximum Y coordinate of loaded tiles
    /// </summary>
    public int MaxTileY { get; set; } = int.MinValue;

    /// <summary>
    /// Whether any tiles have been loaded and bounds are valid
    /// </summary>
    public bool HasTileBounds => MinTileX <= MaxTileX && MinTileY <= MaxTileY;

    /// <summary>
    /// Updates tile bounds when a tile is loaded
    /// </summary>
    public void UpdateTileBounds(int tileX, int tileY)
    {
        MinTileX = Math.Min(MinTileX, tileX);
        MaxTileX = Math.Max(MaxTileX, tileX);
        MinTileY = Math.Min(MinTileY, tileY);
        MaxTileY = Math.Max(MaxTileY, tileY);
    }

    /// <summary>
    /// Resets tile bounds
    /// </summary>
    public void ResetTileBounds()
    {
        MinTileX = int.MaxValue;
        MaxTileX = int.MinValue;
        MinTileY = int.MaxValue;
        MaxTileY = int.MinValue;
    }

    /// <summary>
    /// Gets the layer state for a specific layer type
    /// </summary>
    public LayerState GetLayer(LayerType type) => Layers[(int)type];

    /// <summary>
    /// Selects a layer by type
    /// </summary>
    public void SelectLayer(LayerType type)
    {
        SelectedLayerIndex = (int)type;
    }

    /// <summary>
    /// Toggles visibility of a layer
    /// </summary>
    public void ToggleLayerVisibility(LayerType type)
    {
        var layer = GetLayer(type);
        layer.IsVisible = !layer.IsVisible;
    }

    /// <summary>
    /// Move a layer from one position to another in the rendering order
    /// </summary>
    /// <param name="fromOrderIndex">Source position in LayerOrder (0=bottom, 2=top)</param>
    /// <param name="toOrderIndex">Destination position in LayerOrder (0=bottom, 2=top)</param>
    public void MoveLayer(int fromOrderIndex, int toOrderIndex)
    {
        if (fromOrderIndex == toOrderIndex) return;
        if (fromOrderIndex < 0 || fromOrderIndex >= LayerCount) return;
        if (toOrderIndex < 0 || toOrderIndex >= LayerCount) return;

        int layerIndex = LayerOrder[fromOrderIndex];
        if (fromOrderIndex < toOrderIndex)
        {
            for (int i = fromOrderIndex; i < toOrderIndex; i++)
                LayerOrder[i] = LayerOrder[i + 1];
        }
        else
        {
            for (int i = fromOrderIndex; i > toOrderIndex; i--)
                LayerOrder[i] = LayerOrder[i - 1];
        }
        LayerOrder[toOrderIndex] = layerIndex;
    }

    /// <summary>
    /// Reset layer order to default (Minimap -> Height -> Area)
    /// </summary>
    public void ResetLayerOrder()
    {
        LayerOrder = new[] { 0, 1, 2 };
    }

    /// <summary>
    /// Fits the camera to show the current map bounds (or actual tile bounds if available)
    /// </summary>
    public void FitToMap()
    {
        if (HasTileBounds)
        {
            // Fit to actual tile bounds (add 1 to max because tiles are 1 unit each)
            Camera.FitToTileRange(MinTileX, MaxTileX, MinTileY, MaxTileY);
        }
        else
        {
            // Fit to full map
            Camera.FitToMap();
        }
    }

    /// <summary>
    /// Resets camera to default state (centered on map, zoom = 1)
    /// </summary>
    public void ResetCamera()
    {
        Camera.Reset();
    }

    /// <summary>
    /// Clears the current map and resets all state
    /// </summary>
    public void Clear()
    {
        CurrentWdtId = 0;
        CurrentMap = null;
        IsLoading = false;
        LoadingProgress = 0;
        MinimapTileSizePixels = 256;
        HeightStats = null;

        foreach (var layer in Layers)
        {
            layer.Reset();
        }

        ResetLayerOrder();
        ResetTileBounds();
        ResetCamera();
    }

    /// <summary>
    /// Checks if any layer is currently visible
    /// </summary>
    public bool HasVisibleLayers()
    {
        foreach (var layer in Layers)
        {
            if (layer.IsVisible)
                return true;
        }
        return false;
    }
}
