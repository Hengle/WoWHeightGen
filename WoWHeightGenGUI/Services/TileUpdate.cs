using WoWHeightGenGUI.Models;

namespace WoWHeightGenGUI.Services;

/// <summary>
/// Represents a tile update from background loading.
/// Contains the pixel data for a single tile of a single layer.
/// </summary>
public record TileUpdate
{
    /// <summary>
    /// Tile X coordinate (0-63)
    /// </summary>
    public int TileX { get; init; }

    /// <summary>
    /// Tile Y coordinate (0-63)
    /// </summary>
    public int TileY { get; init; }

    /// <summary>
    /// The layer this tile belongs to
    /// </summary>
    public LayerType Layer { get; init; }

    /// <summary>
    /// Pixel data in RGBA format (4 bytes per pixel)
    /// </summary>
    public byte[] PixelData { get; init; } = Array.Empty<byte>();

    /// <summary>
    /// Raw height values for 16-bit export (only populated for Height layer)
    /// </summary>
    public float[]? RawHeights { get; init; }

    /// <summary>
    /// Width of the tile in pixels
    /// </summary>
    public int Width { get; init; }

    /// <summary>
    /// Height of the tile in pixels
    /// </summary>
    public int Height { get; init; }
}

/// <summary>
/// Contains global height statistics for normalization
/// </summary>
public record HeightStats
{
    public float MinHeight { get; init; }
    public float MaxHeight { get; init; }
}
