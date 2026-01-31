namespace WoWHeightGenGUI.Models;

/// <summary>
/// Represents coordinates in the quad grid (64x64 quads, one per ADT tile).
/// Each quad is 1x1 world units and corresponds to exactly one ADT tile.
/// </summary>
public readonly record struct QuadCoord(int X, int Y) : IComparable<QuadCoord>
{
    /// <summary>
    /// Size of the quad grid (64x64 quads, one per ADT)
    /// </summary>
    public const int GridSize = 64;

    /// <summary>
    /// World size of each quad (1x1 units)
    /// </summary>
    public const float QuadWorldSize = 1.0f;

    /// <summary>
    /// Height/Area pixels per quad (128x128, same as one ADT tile)
    /// </summary>
    public const int HeightPixelsPerQuad = 128;

    /// <summary>
    /// Total map size in height/area pixels (64 * 128 = 8192)
    /// </summary>
    public const int TotalHeightPixels = GridSize * HeightPixelsPerQuad;

    /// <summary>
    /// Check if this coordinate is within valid bounds
    /// </summary>
    public bool IsValid => X >= 0 && X < GridSize && Y >= 0 && Y < GridSize;

    /// <summary>
    /// Get the world-space bounds of this quad.
    /// Returns (minX, minY, maxX, maxY) in world units.
    /// </summary>
    public (float minX, float minY, float maxX, float maxY) GetWorldBounds()
    {
        return (X * QuadWorldSize, Y * QuadWorldSize,
                (X + 1) * QuadWorldSize, (Y + 1) * QuadWorldSize);
    }

    /// <summary>
    /// Get the top-left position of this quad in world space.
    /// </summary>
    public (float x, float y) GetWorldPosition()
    {
        return (X * QuadWorldSize, Y * QuadWorldSize);
    }

    /// <summary>
    /// Get the center position of this quad in world space.
    /// </summary>
    public (float x, float y) GetWorldCenter()
    {
        return ((X + 0.5f) * QuadWorldSize, (Y + 0.5f) * QuadWorldSize);
    }

    /// <summary>
    /// Convert world coordinates to quad coordinates.
    /// </summary>
    public static QuadCoord FromWorldPos(float worldX, float worldY)
    {
        int x = (int)Math.Floor(worldX / QuadWorldSize);
        int y = (int)Math.Floor(worldY / QuadWorldSize);
        return new QuadCoord(
            Math.Clamp(x, 0, GridSize - 1),
            Math.Clamp(y, 0, GridSize - 1)
        );
    }

    /// <summary>
    /// Create a QuadCoord directly from ADT tile coordinates.
    /// Since there's a 1:1 mapping, this just validates and clamps.
    /// </summary>
    public static QuadCoord FromTileCoord(int tileX, int tileY)
    {
        return new QuadCoord(
            Math.Clamp(tileX, 0, GridSize - 1),
            Math.Clamp(tileY, 0, GridSize - 1)
        );
    }

    /// <summary>
    /// Get the pixel bounds for this quad in the full map texture space
    /// </summary>
    /// <param name="pixelsPerQuad">Pixels per quad (e.g., 128 for height/area, variable for minimap)</param>
    /// <returns>Pixel coordinates (x, y) and size</returns>
    public (int x, int y, int size) GetPixelBounds(int pixelsPerQuad)
    {
        return (X * pixelsPerQuad, Y * pixelsPerQuad, pixelsPerQuad);
    }

    /// <summary>
    /// Enumerate all valid quad coordinates in row-major order (Y outer, X inner).
    /// This matches the order used by the command-line tool for texture building.
    /// </summary>
    public static IEnumerable<QuadCoord> All()
    {
        for (int y = 0; y < GridSize; y++)
        {
            for (int x = 0; x < GridSize; x++)
            {
                yield return new QuadCoord(x, y);
            }
        }
    }

    /// <summary>
    /// Enumerate quad coordinates within a range (inclusive).
    /// </summary>
    public static IEnumerable<QuadCoord> Range(int minX, int maxX, int minY, int maxY)
    {
        minX = Math.Max(0, minX);
        maxX = Math.Min(GridSize - 1, maxX);
        minY = Math.Max(0, minY);
        maxY = Math.Min(GridSize - 1, maxY);

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                yield return new QuadCoord(x, y);
            }
        }
    }

    /// <summary>
    /// Get linear index for array storage (row-major order).
    /// </summary>
    public int ToLinearIndex() => Y * GridSize + X;

    /// <summary>
    /// Create from linear index.
    /// </summary>
    public static QuadCoord FromLinearIndex(int index)
    {
        return new QuadCoord(index % GridSize, index / GridSize);
    }

    public int CompareTo(QuadCoord other)
    {
        int yCompare = Y.CompareTo(other.Y);
        return yCompare != 0 ? yCompare : X.CompareTo(other.X);
    }

    public override string ToString() => $"Quad({X}, {Y})";
}
