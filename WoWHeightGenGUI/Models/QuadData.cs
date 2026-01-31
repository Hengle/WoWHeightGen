namespace WoWHeightGenGUI.Models;

/// <summary>
/// CPU-side storage for a single quad's layer data.
/// Each quad corresponds to exactly one ADT tile (1:1 mapping).
/// </summary>
public class QuadData
{
    /// <summary>
    /// The coordinate of this quad in the grid
    /// </summary>
    public QuadCoord Coord { get; }

    /// <summary>
    /// Minimap pixel data (RGBA, variable size based on native resolution)
    /// </summary>
    public byte[]? MinimapPixels { get; private set; }

    /// <summary>
    /// Height layer pixel data (RGBA, 128x128)
    /// </summary>
    public byte[]? HeightPixels { get; private set; }

    /// <summary>
    /// Raw height values for 16-bit export (128x128 floats)
    /// </summary>
    public float[]? HeightValues { get; private set; }

    /// <summary>
    /// Area layer pixel data (RGBA, 128x128)
    /// </summary>
    public byte[]? AreaPixels { get; private set; }

    /// <summary>
    /// Size of the minimap texture (width = height)
    /// </summary>
    public int MinimapSize { get; private set; }

    /// <summary>
    /// Size of height/area textures (always 128)
    /// </summary>
    public int HeightAreaSize => QuadCoord.HeightPixelsPerQuad;

    /// <summary>
    /// Whether the minimap layer is dirty and needs GPU upload
    /// </summary>
    public bool MinimapDirty { get; set; }

    /// <summary>
    /// Whether the height layer is dirty and needs GPU upload
    /// </summary>
    public bool HeightDirty { get; set; }

    /// <summary>
    /// Whether the area layer is dirty and needs GPU upload
    /// </summary>
    public bool AreaDirty { get; set; }

    /// <summary>
    /// Whether this quad has minimap data
    /// </summary>
    public bool HasMinimap => MinimapPixels != null;

    /// <summary>
    /// Whether this quad has height data
    /// </summary>
    public bool HasHeight => HeightPixels != null;

    /// <summary>
    /// Whether this quad has area data
    /// </summary>
    public bool HasArea => AreaPixels != null;

    /// <summary>
    /// Whether this quad has any data at all
    /// </summary>
    public bool HasAnyData => MinimapPixels != null || HeightPixels != null || AreaPixels != null;

    /// <summary>
    /// Whether any layer is dirty and needs GPU upload
    /// </summary>
    public bool IsDirty => MinimapDirty || HeightDirty || AreaDirty;

    public QuadData(QuadCoord coord)
    {
        Coord = coord;
    }

    /// <summary>
    /// Set minimap data directly (single tile, no assembly needed)
    /// </summary>
    /// <param name="pixels">RGBA pixel data</param>
    /// <param name="size">Width/height in pixels</param>
    public void SetMinimap(byte[] pixels, int size)
    {
        MinimapPixels = pixels;
        MinimapSize = size;
        MinimapDirty = true;
    }

    /// <summary>
    /// Set height data directly (single tile, always 128x128)
    /// </summary>
    /// <param name="pixels">RGBA pixel data (128x128x4 bytes)</param>
    /// <param name="rawHeights">Optional raw height values for 16-bit export</param>
    public void SetHeight(byte[] pixels, float[]? rawHeights = null)
    {
        HeightPixels = pixels;
        HeightValues = rawHeights;
        HeightDirty = true;
    }

    /// <summary>
    /// Set area data directly (single tile, always 128x128)
    /// </summary>
    /// <param name="pixels">RGBA pixel data (128x128x4 bytes)</param>
    public void SetArea(byte[] pixels)
    {
        AreaPixels = pixels;
        AreaDirty = true;
    }

    /// <summary>
    /// Mark all layers as clean (uploaded to GPU)
    /// </summary>
    public void MarkClean()
    {
        MinimapDirty = false;
        HeightDirty = false;
        AreaDirty = false;
    }

    /// <summary>
    /// Clear all data
    /// </summary>
    public void Clear()
    {
        MinimapPixels = null;
        HeightPixels = null;
        HeightValues = null;
        AreaPixels = null;
        MinimapSize = 0;
        MinimapDirty = false;
        HeightDirty = false;
        AreaDirty = false;
    }
}
