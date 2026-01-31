using System.Numerics;

namespace WoWHeightGenGUI.Rendering;

/// <summary>
/// 2D camera for viewport navigation with proper pan/zoom handling.
/// Position is in world units, zoom is pixels per world unit.
/// World coordinate system: (0,0) at top-left, X increases right, Y increases down.
/// Each ADT tile is 1x1 world units, so the full 64x64 map spans (0,0) to (64,64).
/// </summary>
public class Camera2D
{
    private Vector2 _position = new Vector2(32f, 32f);
    private float _zoom = 1.0f;

    /// <summary>
    /// Camera position in world units (center of view).
    /// Automatically clamped to keep view within map bounds.
    /// </summary>
    public Vector2 Position
    {
        get => _position;
        set
        {
            _position = value;
            ClampPosition();
        }
    }

    /// <summary>
    /// Zoom level (pixels per world unit).
    /// At zoom=1, one world unit = 1 pixel.
    /// At zoom=128, one world unit = 128 pixels (100% for height/area).
    /// At zoom=512, one world unit = 512 pixels (400%).
    /// </summary>
    public float Zoom
    {
        get => _zoom;
        set
        {
            _zoom = Math.Clamp(value, GetMinZoom(), MaxZoom);
            ClampPosition();
        }
    }

    /// <summary>
    /// Maximum zoom level (1000% = 10x native resolution = 1280 pixels per tile)
    /// </summary>
    public float MaxZoom { get; set; } = 1280.0f;

    /// <summary>
    /// Zoom speed multiplier for mouse wheel
    /// </summary>
    public float ZoomSpeed { get; set; } = 0.15f;

    /// <summary>
    /// Viewport size in screen pixels
    /// </summary>
    public Vector2 ViewportSize { get; set; }

    /// <summary>
    /// Map size in world units (64x64 tiles)
    /// </summary>
    public const float MapSize = 64f;

    /// <summary>
    /// Pixels per tile for height/area data (100% zoom)
    /// </summary>
    public const int PixelsPerTile = 128;

    /// <summary>
    /// Get minimum zoom level that fits the entire 64x64 map in the viewport
    /// </summary>
    public float GetMinZoom()
    {
        if (ViewportSize.X <= 0 || ViewportSize.Y <= 0)
            return 1.0f;

        // Zoom where map fits exactly in viewport (smaller dimension)
        float zoomX = ViewportSize.X / MapSize;
        float zoomY = ViewportSize.Y / MapSize;
        return Math.Min(zoomX, zoomY);
    }

    /// <summary>
    /// Clamp camera position to keep the view within the map bounds (0,0) to (64,64)
    /// </summary>
    private void ClampPosition()
    {
        if (ViewportSize.X <= 0 || ViewportSize.Y <= 0)
            return;

        // Half of the visible world size
        float halfViewX = ViewportSize.X / (2 * _zoom);
        float halfViewY = ViewportSize.Y / (2 * _zoom);

        // Clamp position so view stays within map
        // If view is larger than map, center on map
        float minX, maxX, minY, maxY;

        if (halfViewX * 2 >= MapSize)
        {
            // View wider than map - center horizontally
            minX = maxX = MapSize / 2;
        }
        else
        {
            minX = halfViewX;
            maxX = MapSize - halfViewX;
        }

        if (halfViewY * 2 >= MapSize)
        {
            // View taller than map - center vertically
            minY = maxY = MapSize / 2;
        }
        else
        {
            minY = halfViewY;
            maxY = MapSize - halfViewY;
        }

        _position = new Vector2(
            Math.Clamp(_position.X, minX, maxX),
            Math.Clamp(_position.Y, minY, maxY)
        );
    }

    /// <summary>
    /// Get the view-projection matrix for rendering.
    /// Transforms world coordinates to NDC (-1 to 1).
    /// </summary>
    public Matrix4x4 GetViewProjectionMatrix()
    {
        if (ViewportSize.X <= 0 || ViewportSize.Y <= 0)
            return Matrix4x4.Identity;

        // View matrix: translate to center camera on Position
        var view = Matrix4x4.CreateTranslation(-_position.X, -_position.Y, 0);

        // Projection matrix: scale from world units to NDC
        // At zoom=1, 1 world unit = 1 pixel
        // NDC range is -1 to 1 (2 units), viewport is ViewportSize pixels
        float scaleX = 2.0f * _zoom / ViewportSize.X;
        float scaleY = -2.0f * _zoom / ViewportSize.Y; // Negative for screen Y-down

        var projection = Matrix4x4.CreateScale(scaleX, scaleY, 1.0f);

        return view * projection;
    }

    /// <summary>
    /// Handle mouse drag for panning.
    /// Delta is in screen pixels, converted to world units.
    /// The pan accounts for zoom so mouse travels exact screen distance.
    /// </summary>
    public void Pan(Vector2 screenDelta)
    {
        // Convert screen delta to world delta
        // screenDelta / Zoom = worldDelta
        // Negate because moving mouse right should move camera left (view moves right)
        Position -= screenDelta / _zoom;
    }

    /// <summary>
    /// Handle mouse wheel for zooming towards a point.
    /// </summary>
    /// <param name="delta">Scroll delta (positive = zoom in)</param>
    /// <param name="screenPos">Mouse position relative to viewport top-left</param>
    public void ZoomTowards(float delta, Vector2 screenPos)
    {
        // Get world position under mouse before zoom
        var worldPosBefore = ScreenToWorld(screenPos);

        // Apply zoom
        float zoomFactor = 1.0f + delta * ZoomSpeed;
        float newZoom = _zoom * zoomFactor;
        _zoom = Math.Clamp(newZoom, GetMinZoom(), MaxZoom);

        // Get world position under mouse after zoom
        var worldPosAfter = ScreenToWorld(screenPos);

        // Adjust position to keep same world point under mouse
        _position += worldPosBefore - worldPosAfter;
        ClampPosition();
    }

    /// <summary>
    /// Convert screen coordinates to world coordinates.
    /// </summary>
    /// <param name="screenPos">Position relative to viewport top-left</param>
    public Vector2 ScreenToWorld(Vector2 screenPos)
    {
        // Screen center is at Position in world space
        var screenCenter = ViewportSize / 2;
        var offsetFromCenter = screenPos - screenCenter;

        // Convert to world units and add camera position
        return _position + offsetFromCenter / _zoom;
    }

    /// <summary>
    /// Convert world coordinates to screen coordinates.
    /// </summary>
    /// <returns>Position relative to viewport top-left</returns>
    public Vector2 WorldToScreen(Vector2 worldPos)
    {
        var screenCenter = ViewportSize / 2;
        var offsetFromCamera = worldPos - _position;

        return screenCenter + offsetFromCamera * _zoom;
    }

    /// <summary>
    /// Get the visible world bounds (AABB).
    /// </summary>
    public (Vector2 min, Vector2 max) GetVisibleWorldBounds()
    {
        var halfViewWorld = ViewportSize / (2 * _zoom);
        return (_position - halfViewWorld, _position + halfViewWorld);
    }

    /// <summary>
    /// Fit the camera to show the specified bounds with optional margin.
    /// </summary>
    public void FitToBounds(Vector2 boundsMin, Vector2 boundsMax, float margin = 0.05f)
    {
        if (ViewportSize.X <= 0 || ViewportSize.Y <= 0)
            return;

        var boundsSize = boundsMax - boundsMin;
        if (boundsSize.X <= 0 || boundsSize.Y <= 0)
            return;

        var boundsCenter = (boundsMin + boundsMax) / 2;

        // Calculate zoom to fit bounds with margin
        float zoomX = ViewportSize.X / (boundsSize.X * (1 + margin));
        float zoomY = ViewportSize.Y / (boundsSize.Y * (1 + margin));

        _zoom = Math.Min(zoomX, zoomY);
        _zoom = Math.Clamp(_zoom, GetMinZoom(), MaxZoom);
        _position = boundsCenter;
        ClampPosition();
    }

    /// <summary>
    /// Fit the camera to show the entire map.
    /// </summary>
    public void FitToMap()
    {
        FitToBounds(Vector2.Zero, new Vector2(MapSize, MapSize));
    }

    /// <summary>
    /// Fit the camera to show a specific tile range.
    /// </summary>
    public void FitToTileRange(int minX, int maxX, int minY, int maxY)
    {
        // Add 1 to max because tiles are 1 unit each
        FitToBounds(
            new Vector2(minX, minY),
            new Vector2(maxX + 1, maxY + 1));
    }

    /// <summary>
    /// Reset to default view (fit entire map).
    /// </summary>
    public void Reset()
    {
        FitToMap();
    }

    /// <summary>
    /// Get the tile coordinate at a screen position.
    /// </summary>
    public (int tileX, int tileY) ScreenToTile(Vector2 screenPos)
    {
        var worldPos = ScreenToWorld(screenPos);
        int tileX = (int)Math.Floor(worldPos.X);
        int tileY = (int)Math.Floor(worldPos.Y);
        return (Math.Clamp(tileX, 0, 63), Math.Clamp(tileY, 0, 63));
    }

    /// <summary>
    /// Get the pixel coordinate within the full map at a screen position.
    /// </summary>
    public (int pixelX, int pixelY) ScreenToMapPixel(Vector2 screenPos)
    {
        var worldPos = ScreenToWorld(screenPos);
        int pixelX = (int)(worldPos.X * PixelsPerTile);
        int pixelY = (int)(worldPos.Y * PixelsPerTile);
        return (
            Math.Clamp(pixelX, 0, (int)(MapSize * PixelsPerTile) - 1),
            Math.Clamp(pixelY, 0, (int)(MapSize * PixelsPerTile) - 1));
    }

    /// <summary>
    /// Get zoom as a percentage (100% = native resolution = 128 pixels per tile)
    /// </summary>
    public float GetZoomPercent() => _zoom / PixelsPerTile * 100f;
}
