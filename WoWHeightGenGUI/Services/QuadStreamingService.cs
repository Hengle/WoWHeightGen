using WoWHeightGenGUI.Models;
using WoWHeightGenGUI.Rendering;

namespace WoWHeightGenGUI.Services;

/// <summary>
/// Manages quad streaming based on viewport visibility using Camera2D.
/// Bridges tile loading from MapLoadingService to the quad cache system.
/// With the 64x64 grid, each quad corresponds exactly to one ADT tile.
/// </summary>
public class QuadStreamingService
{
    private readonly QuadCache _cache;
    private HashSet<QuadCoord> _visibleQuads = new();
    private int _bufferTiles;

    /// <summary>
    /// Detected minimap tile size in pixels (per tile)
    /// </summary>
    public int MinimapTileSize { get; set; } = 256;

    /// <summary>
    /// Number of buffer tiles around the visible area
    /// </summary>
    public int BufferTiles
    {
        get => _bufferTiles;
        set => _bufferTiles = Math.Max(0, Math.Min(8, value));
    }

    /// <summary>
    /// The quad cache being managed
    /// </summary>
    public QuadCache Cache => _cache;

    /// <summary>
    /// Currently visible quads (including buffer)
    /// </summary>
    public IReadOnlySet<QuadCoord> VisibleQuads => _visibleQuads;

    public QuadStreamingService(QuadCache cache, int bufferTiles = 2)
    {
        _cache = cache;
        _bufferTiles = bufferTiles;
    }

    /// <summary>
    /// Process a tile update from MapLoadingService.
    /// With 64x64 grid, tile coordinates map directly to quad coordinates.
    /// </summary>
    public void ProcessTileUpdate(TileUpdate update)
    {
        // Direct 1:1 mapping from tile coords to quad coords
        var quadCoord = QuadCoord.FromTileCoord(update.TileX, update.TileY);

        // Get or create the quad data
        var quadData = _cache.GetOrCreateCpuQuad(quadCoord);

        // Set the tile data directly (no assembly needed)
        switch (update.Layer)
        {
            case LayerType.Minimap:
                quadData.SetMinimap(update.PixelData, update.Width);
                // Track minimap tile size from first minimap update
                if (MinimapTileSize == 256)
                {
                    MinimapTileSize = update.Width;
                    _cache.MinimapTileSize = update.Width;
                }
                break;

            case LayerType.Height:
                quadData.SetHeight(update.PixelData, update.RawHeights);
                break;

            case LayerType.Area:
                quadData.SetArea(update.PixelData);
                break;
        }
    }

    /// <summary>
    /// Update visibility - returns all loaded quads (no culling).
    /// </summary>
    /// <param name="camera">The Camera2D (unused, kept for API compatibility)</param>
    /// <returns>Set of all loaded quad coordinates</returns>
    public HashSet<QuadCoord> UpdateVisibility(Camera2D camera)
    {
        // Return all loaded quads - no visibility culling
        _visibleQuads = _cache.GetLoadedQuadCoords().ToHashSet();

        // Tick frame counter for LRU tracking
        _cache.TickFrame();

        // Update dirty quads already in GPU
        _cache.UpdateDirtyGpuQuads();

        // Ensure all loaded quads are in GPU
        _cache.EnsureQuadsLoaded(_visibleQuads);

        return _visibleQuads;
    }

    /// <summary>
    /// Get quads that have CPU data (for export or other purposes)
    /// </summary>
    public IEnumerable<QuadCoord> GetLoadedQuads()
    {
        return _cache.GetLoadedQuadCoords();
    }

    /// <summary>
    /// Get the bounds (min/max quad coords) of loaded data
    /// </summary>
    public (int minX, int maxX, int minY, int maxY)? GetLoadedBounds()
    {
        var loaded = GetLoadedQuads().ToList();
        if (loaded.Count == 0)
            return null;

        int minX = loaded.Min(q => q.X);
        int maxX = loaded.Max(q => q.X);
        int minY = loaded.Min(q => q.Y);
        int maxY = loaded.Max(q => q.Y);

        return (minX, maxX, minY, maxY);
    }

    /// <summary>
    /// Clear all cached data
    /// </summary>
    public void Clear()
    {
        _cache.Clear();
        _visibleQuads.Clear();
        MinimapTileSize = 256;
    }
}
