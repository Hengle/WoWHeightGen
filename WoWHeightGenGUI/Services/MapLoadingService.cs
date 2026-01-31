using System.Collections.Concurrent;
using SereniaBLPLib;
using WoWHeightGenGUI.Models;
using WoWHeightGenLib.Models;
using WoWHeightGenLib.Services;

namespace WoWHeightGenGUI.Services;

/// <summary>
/// Service for loading map data in the background with spiral loading pattern.
/// Generates tile updates that can be processed on the main thread for GPU upload.
/// </summary>
public class MapLoadingService : IDisposable
{
    private readonly MapGenerationContext _context;
    private CancellationTokenSource? _cts;
    private Task? _loadingTask;
    private readonly ConcurrentQueue<TileUpdate> _pendingUpdates = new();
    private readonly object _lock = new();

    /// <summary>
    /// Map size (tiles per side)
    /// </summary>
    public const int MapSize = 64;

    /// <summary>
    /// Height/Area tile resolution in pixels
    /// </summary>
    public const int HeightTileSize = 128;

    /// <summary>
    /// Default minimap tile resolution in pixels
    /// </summary>
    public const int DefaultMinimapTileSize = 256;

    /// <summary>
    /// Fired when a tile has been loaded
    /// </summary>
    public event Action<TileUpdate>? OnTileLoaded;

    /// <summary>
    /// Fired when loading progress changes (0-1)
    /// </summary>
    public event Action<float>? OnProgressChanged;

    /// <summary>
    /// Fired when loading completes
    /// </summary>
    public event Action? OnLoadingComplete;

    /// <summary>
    /// Fired when loading is cancelled
    /// </summary>
    public event Action? OnLoadingCancelled;

    /// <summary>
    /// Fired when an error occurs during loading
    /// </summary>
    public event Action<Exception>? OnLoadingError;

    /// <summary>
    /// Whether a map is currently being loaded
    /// </summary>
    public bool IsLoading { get; private set; }

    /// <summary>
    /// Current loading progress (0-1)
    /// </summary>
    public float Progress { get; private set; }

    /// <summary>
    /// The detected minimap tile resolution
    /// </summary>
    public int MinimapTileSize { get; private set; } = DefaultMinimapTileSize;

    /// <summary>
    /// Global height statistics for normalization
    /// </summary>
    public HeightStats? HeightStats { get; private set; }

    /// <summary>
    /// Area color mapping (area ID -> RGBA color)
    /// </summary>
    public ConcurrentDictionary<uint, uint> AreaColors { get; } = new();

    /// <summary>
    /// Area ID map for tooltip lookup (tileX, tileY) -> area IDs array
    /// Each tile has HeightTileSize x HeightTileSize area IDs stored in row-major order
    /// </summary>
    public ConcurrentDictionary<(int, int), uint[]> AreaIdMap { get; } = new();

    /// <summary>
    /// Height map for tooltip lookup (tileX, tileY) -> heights array
    /// Each tile has HeightTileSize x HeightTileSize heights stored in row-major order
    /// </summary>
    public ConcurrentDictionary<(int, int), float[]> HeightMap { get; } = new();

    /// <summary>
    /// Size of one ADT tile in world units (yards)
    /// </summary>
    public const float AdtSize = 533.33333f;

    /// <summary>
    /// Total world size (64 ADTs * 533.33333 yards)
    /// </summary>
    public const float WorldSize = AdtSize * MapSize;

    public MapLoadingService(MapGenerationContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Start loading a map in the background
    /// </summary>
    public void StartLoading(uint wdtFileDataId, LayerType[]? layersToLoad = null)
    {
        // Cancel any existing loading operation
        Cancel();

        layersToLoad ??= new[] { LayerType.Minimap, LayerType.Height, LayerType.Area };

        _cts = new CancellationTokenSource();
        IsLoading = true;
        Progress = 0;
        HeightStats = null;

        _loadingTask = Task.Run(() => LoadMapAsync(wdtFileDataId, layersToLoad, _cts.Token));
    }

    /// <summary>
    /// Cancel the current loading operation
    /// </summary>
    public void Cancel()
    {
        lock (_lock)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        // Clear pending updates
        while (_pendingUpdates.TryDequeue(out _)) { }

        IsLoading = false;
        Progress = 0;
    }

    /// <summary>
    /// Try to get a pending tile update for processing on the main thread
    /// </summary>
    public bool TryGetPendingUpdate(out TileUpdate? update)
    {
        return _pendingUpdates.TryDequeue(out update);
    }

    /// <summary>
    /// Get the number of pending updates waiting to be processed
    /// </summary>
    public int PendingUpdateCount => _pendingUpdates.Count;

    /// <summary>
    /// Get the area ID at a specific pixel position
    /// </summary>
    /// <param name="pixelX">X pixel coordinate (0 to MapSize * HeightTileSize)</param>
    /// <param name="pixelY">Y pixel coordinate (0 to MapSize * HeightTileSize)</param>
    /// <returns>The area ID at that position, or 0 if not found</returns>
    public uint GetAreaIdAtPixel(int pixelX, int pixelY)
    {
        // Calculate tile coordinates
        int tileX = pixelX / HeightTileSize;
        int tileY = pixelY / HeightTileSize;

        // Calculate local pixel within tile
        int localX = pixelX % HeightTileSize;
        int localY = pixelY % HeightTileSize;

        // Look up the area ID
        if (AreaIdMap.TryGetValue((tileX, tileY), out var areaIds))
        {
            int index = localY * HeightTileSize + localX;
            if (index >= 0 && index < areaIds.Length)
            {
                return areaIds[index];
            }
        }

        return 0;
    }

    /// <summary>
    /// Get the height at a specific pixel position
    /// </summary>
    /// <param name="pixelX">X pixel coordinate (0 to MapSize * HeightTileSize)</param>
    /// <param name="pixelY">Y pixel coordinate (0 to MapSize * HeightTileSize)</param>
    /// <param name="height">The height value if found</param>
    /// <returns>True if height data exists at that position</returns>
    public bool TryGetHeightAtPixel(int pixelX, int pixelY, out float height)
    {
        height = 0;

        // Calculate tile coordinates
        int tileX = pixelX / HeightTileSize;
        int tileY = pixelY / HeightTileSize;

        // Calculate local pixel within tile
        int localX = pixelX % HeightTileSize;
        int localY = pixelY % HeightTileSize;

        // Look up the height
        if (HeightMap.TryGetValue((tileX, tileY), out var heights))
        {
            int index = localY * HeightTileSize + localX;
            if (index >= 0 && index < heights.Length)
            {
                height = heights[index];
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Convert pixel coordinates to world coordinates
    /// </summary>
    /// <param name="pixelX">X pixel coordinate</param>
    /// <param name="pixelY">Y pixel coordinate</param>
    /// <returns>World coordinates (X, Y) in yards</returns>
    public static (float worldX, float worldY) PixelToWorldCoords(int pixelX, int pixelY)
    {
        // WoW coordinate system: center of map is (0, 0)
        // Map spans from -WorldSize/2 to +WorldSize/2
        // Pixel (0,0) is top-left corner of the map
        float halfWorld = WorldSize / 2f;
        float pixelsPerYard = (MapSize * HeightTileSize) / WorldSize;

        // Convert pixel to world - note WoW has Y increasing southward in map terms
        float worldX = halfWorld - (pixelY / pixelsPerYard);
        float worldY = halfWorld - (pixelX / pixelsPerYard);

        return (worldX, worldY);
    }

    /// <summary>
    /// Get ADT cell coordinates (chunk within ADT)
    /// </summary>
    /// <param name="pixelX">X pixel coordinate</param>
    /// <param name="pixelY">Y pixel coordinate</param>
    /// <returns>Cell coordinates (chunkX, chunkY) within the ADT (0-15)</returns>
    public static (int cellX, int cellY) GetCellCoords(int pixelX, int pixelY)
    {
        // Each ADT has 128 pixels, divided into 16 chunks = 8 pixels per chunk
        int localX = pixelX % HeightTileSize;
        int localY = pixelY % HeightTileSize;

        int cellX = localX / 8;  // 0-15
        int cellY = localY / 8;  // 0-15

        return (cellX, cellY);
    }

    /// <summary>
    /// Get tile bounds from a WDT file without loading individual ADT files
    /// </summary>
    /// <param name="wdtFileDataId">The WDT FileDataID</param>
    /// <returns>Tile bounds (minX, maxX, minY, maxY) or null if WDT cannot be loaded</returns>
    public (int minX, int maxX, int minY, int maxY)? GetTileBoundsFromWdt(uint wdtFileDataId)
    {
        if (!_context.FileExists(wdtFileDataId))
            return null;

        try
        {
            using var wdtStream = _context.OpenFile(wdtFileDataId);
            var wdt = new Wdt(wdtStream);

            if (wdt.fileInfo == null)
                return null;

            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;

            for (int y = 0; y < MapSize; y++)
            {
                for (int x = 0; x < MapSize; x++)
                {
                    // Check if tile exists by looking at rootADT FileDataID
                    if (wdt.fileInfo[x, y].rootADT != 0)
                    {
                        minX = Math.Min(minX, x);
                        maxX = Math.Max(maxX, x);
                        minY = Math.Min(minY, y);
                        maxY = Math.Max(maxY, y);
                    }
                }
            }

            if (minX <= maxX && minY <= maxY)
                return (minX, maxX, minY, maxY);

            return null;
        }
        catch
        {
            return null;
        }
    }

    private async Task LoadMapAsync(uint wdtFileDataId, LayerType[] layers, CancellationToken ct)
    {
        try
        {
            if (!_context.FileExists(wdtFileDataId))
            {
                OnLoadingError?.Invoke(new FileNotFoundException($"WDT file not found: {wdtFileDataId}"));
                return;
            }

            // Load WDT
            Wdt wdt;
            using (var wdtStream = _context.OpenFile(wdtFileDataId))
            {
                wdt = new Wdt(wdtStream);
            }

            if (wdt.fileInfo == null)
            {
                OnLoadingError?.Invoke(new InvalidDataException("WDT file contains no tile information"));
                return;
            }

            // Detect minimap resolution
            MinimapTileSize = DetectMinimapResolution(wdt, ct);

            // First pass: Calculate global height statistics if loading height layer
            if (layers.Contains(LayerType.Height))
            {
                await Task.Run(() => CalculateHeightStats(wdt, ct), ct);
            }

            ct.ThrowIfCancellationRequested();

            // Generate spiral order for loading
            var spiralOrder = GenerateSpiralOrder(wdt);
            int totalTiles = spiralOrder.Count * layers.Length;
            int completedTiles = 0;

            // Load tiles in spiral order
            foreach (var (x, y) in spiralOrder)
            {
                ct.ThrowIfCancellationRequested();

                foreach (var layer in layers)
                {
                    ct.ThrowIfCancellationRequested();

                    var tileUpdate = await Task.Run(() => GenerateTile(wdt, x, y, layer, ct), ct);

                    if (tileUpdate != null)
                    {
                        _pendingUpdates.Enqueue(tileUpdate);
                        OnTileLoaded?.Invoke(tileUpdate);
                    }

                    completedTiles++;
                    Progress = (float)completedTiles / totalTiles;
                    OnProgressChanged?.Invoke(Progress);
                }
            }

            IsLoading = false;
            Progress = 1.0f;
            OnLoadingComplete?.Invoke();
        }
        catch (OperationCanceledException)
        {
            IsLoading = false;
            OnLoadingCancelled?.Invoke();
        }
        catch (Exception ex)
        {
            IsLoading = false;
            OnLoadingError?.Invoke(ex);
        }
    }

    private int DetectMinimapResolution(Wdt wdt, CancellationToken ct)
    {
        if (wdt.fileInfo == null) return DefaultMinimapTileSize;

        for (int y = 0; y < MapSize && !ct.IsCancellationRequested; y++)
        {
            for (int x = 0; x < MapSize && !ct.IsCancellationRequested; x++)
            {
                var info = wdt.fileInfo[x, y];
                if (_context.FileExists(info.minimapTexture))
                {
                    try
                    {
                        using var stream = _context.OpenFile(info.minimapTexture);
                        var blp = new BlpFile(stream);
                        var img = blp.GetImage(0);
                        if (img != null)
                        {
                            return img.Width;
                        }
                    }
                    catch
                    {
                        // Continue searching
                    }
                }
            }
        }

        return DefaultMinimapTileSize;
    }

    private void CalculateHeightStats(Wdt wdt, CancellationToken ct)
    {
        if (wdt.fileInfo == null) return;

        float minHeight = float.MaxValue;
        float maxHeight = float.MinValue;

        for (int y = 0; y < MapSize && !ct.IsCancellationRequested; y++)
        {
            for (int x = 0; x < MapSize && !ct.IsCancellationRequested; x++)
            {
                var info = wdt.fileInfo[x, y];
                if (!_context.FileExists(info.rootADT)) continue;

                try
                {
                    using var stream = _context.OpenFile(info.rootADT);
                    var adt = new Adt(stream);

                    if (adt.minHeight < minHeight)
                        minHeight = adt.minHeight;
                    if (adt.maxHeight > maxHeight)
                        maxHeight = adt.maxHeight;
                }
                catch
                {
                    // Skip problematic ADTs
                }
            }
        }

        if (minHeight < float.MaxValue && maxHeight > float.MinValue)
        {
            HeightStats = new HeightStats
            {
                MinHeight = minHeight,
                MaxHeight = maxHeight
            };
        }
    }

    private List<(int x, int y)> GenerateSpiralOrder(Wdt wdt)
    {
        var result = new List<(int x, int y)>();
        if (wdt.fileInfo == null) return result;

        // Start from center (32, 32) and spiral outward
        int centerX = MapSize / 2;
        int centerY = MapSize / 2;

        // Track visited positions
        var visited = new bool[MapSize, MapSize];

        // Directions: right, down, left, up
        int[] dx = { 1, 0, -1, 0 };
        int[] dy = { 0, 1, 0, -1 };

        int x = centerX;
        int y = centerY;
        int direction = 0;
        int stepsInDirection = 1;
        int stepsTaken = 0;
        int directionChanges = 0;

        // Add center first if valid
        if (HasValidTile(wdt, x, y))
        {
            result.Add((x, y));
        }
        visited[x, y] = true;

        // Spiral outward
        while (result.Count < MapSize * MapSize)
        {
            // Move one step
            x += dx[direction];
            y += dy[direction];
            stepsTaken++;

            // Check bounds
            if (x >= 0 && x < MapSize && y >= 0 && y < MapSize && !visited[x, y])
            {
                visited[x, y] = true;
                if (HasValidTile(wdt, x, y))
                {
                    result.Add((x, y));
                }
            }

            // Change direction if needed
            if (stepsTaken >= stepsInDirection)
            {
                stepsTaken = 0;
                direction = (direction + 1) % 4;
                directionChanges++;

                // Increase step count every two direction changes
                if (directionChanges % 2 == 0)
                {
                    stepsInDirection++;
                }
            }

            // Safety check - if we've completed enough spiral iterations
            if (stepsInDirection > MapSize * 2)
            {
                break;
            }
        }

        return result;
    }

    private bool HasValidTile(Wdt wdt, int x, int y)
    {
        if (wdt.fileInfo == null) return false;
        if (x < 0 || x >= MapSize || y < 0 || y >= MapSize) return false;

        var info = wdt.fileInfo[x, y];
        return _context.FileExists(info.rootADT) ||
               _context.FileExists(info.minimapTexture);
    }

    private TileUpdate? GenerateTile(Wdt wdt, int x, int y, LayerType layer, CancellationToken ct)
    {
        if (wdt.fileInfo == null) return null;
        ct.ThrowIfCancellationRequested();

        var info = wdt.fileInfo[x, y];

        return layer switch
        {
            LayerType.Minimap => GenerateMinimapTile(info, x, y, ct),
            LayerType.Height => GenerateHeightTile(info, x, y, ct),
            LayerType.Area => GenerateAreaTile(info, x, y, ct),
            _ => null
        };
    }

    private TileUpdate? GenerateMinimapTile(Wdt.FileInfo info, int x, int y, CancellationToken ct)
    {
        if (!_context.FileExists(info.minimapTexture))
            return null;

        try
        {
            ct.ThrowIfCancellationRequested();

            using var stream = _context.OpenFile(info.minimapTexture);
            var blp = new BlpFile(stream);
            var img = blp.GetImage(0);

            if (img == null) return null;

            // Output at native resolution to preserve full detail
            int tileSize = MinimapTileSize;
            var pixelData = new byte[tileSize * tileSize * 4];
            int idx = 0;

            // If the image is exactly the expected size, copy directly
            if (img.Width == tileSize && img.Height == tileSize)
            {
                for (int py = 0; py < tileSize; py++)
                {
                    for (int px = 0; px < tileSize; px++)
                    {
                        var pixel = img[px, py];
                        pixelData[idx++] = pixel.R;
                        pixelData[idx++] = pixel.G;
                        pixelData[idx++] = pixel.B;
                        pixelData[idx++] = pixel.A;
                    }
                }
            }
            else
            {
                // Resample if source size differs from detected MinimapTileSize
                float scaleX = (float)img.Width / tileSize;
                float scaleY = (float)img.Height / tileSize;

                for (int py = 0; py < tileSize; py++)
                {
                    for (int px = 0; px < tileSize; px++)
                    {
                        // Bilinear sample from source image
                        float srcX = px * scaleX;
                        float srcY = py * scaleY;

                        int x0 = (int)srcX;
                        int y0 = (int)srcY;
                        int x1 = Math.Min(x0 + 1, img.Width - 1);
                        int y1 = Math.Min(y0 + 1, img.Height - 1);

                        float fx = srcX - x0;
                        float fy = srcY - y0;

                        var p00 = img[x0, y0];
                        var p10 = img[x1, y0];
                        var p01 = img[x0, y1];
                        var p11 = img[x1, y1];

                        // Bilinear interpolation
                        byte r = (byte)(p00.R * (1 - fx) * (1 - fy) + p10.R * fx * (1 - fy) + p01.R * (1 - fx) * fy + p11.R * fx * fy);
                        byte g = (byte)(p00.G * (1 - fx) * (1 - fy) + p10.G * fx * (1 - fy) + p01.G * (1 - fx) * fy + p11.G * fx * fy);
                        byte b = (byte)(p00.B * (1 - fx) * (1 - fy) + p10.B * fx * (1 - fy) + p01.B * (1 - fx) * fy + p11.B * fx * fy);
                        byte a = (byte)(p00.A * (1 - fx) * (1 - fy) + p10.A * fx * (1 - fy) + p01.A * (1 - fx) * fy + p11.A * fx * fy);

                        pixelData[idx++] = r;
                        pixelData[idx++] = g;
                        pixelData[idx++] = b;
                        pixelData[idx++] = a;
                    }
                }
            }

            return new TileUpdate
            {
                TileX = x,
                TileY = y,
                Layer = LayerType.Minimap,
                PixelData = pixelData,
                Width = tileSize,
                Height = tileSize
            };
        }
        catch
        {
            return null;
        }
    }

    private TileUpdate? GenerateHeightTile(Wdt.FileInfo info, int x, int y, CancellationToken ct)
    {
        if (!_context.FileExists(info.rootADT))
            return null;

        try
        {
            ct.ThrowIfCancellationRequested();

            using var stream = _context.OpenFile(info.rootADT);
            var adt = new Adt(stream);

            if (adt.heightmap == null) return null;

            // Use global height stats for normalization
            float minHeight = HeightStats?.MinHeight ?? adt.minHeight;
            float maxHeight = HeightStats?.MaxHeight ?? adt.maxHeight;
            float range = maxHeight - minHeight;

            if (range <= 0) range = 1;

            // Convert height map to RGBA (store normalized height in R channel)
            var pixelData = new byte[HeightTileSize * HeightTileSize * 4];
            var heights = new float[HeightTileSize * HeightTileSize];
            int idx = 0;

            for (int py = 0; py < HeightTileSize; py++)
            {
                for (int px = 0; px < HeightTileSize; px++)
                {
                    float height = adt.heightmap[py, px];

                    // Store raw height for tooltip lookup
                    heights[py * HeightTileSize + px] = height;

                    float normalized = (height - minHeight) / range;
                    normalized = Math.Clamp(normalized, 0f, 1f);
                    byte value = (byte)(normalized * 255f);

                    pixelData[idx++] = value; // R - normalized height
                    pixelData[idx++] = value; // G
                    pixelData[idx++] = value; // B
                    pixelData[idx++] = 255;   // A - fully opaque
                }
            }

            // Store heights for this tile
            HeightMap[(x, y)] = heights;

            return new TileUpdate
            {
                TileX = x,
                TileY = y,
                Layer = LayerType.Height,
                PixelData = pixelData,
                Width = HeightTileSize,
                Height = HeightTileSize
            };
        }
        catch
        {
            return null;
        }
    }

    private TileUpdate? GenerateAreaTile(Wdt.FileInfo info, int x, int y, CancellationToken ct)
    {
        if (!_context.FileExists(info.rootADT))
            return null;

        try
        {
            ct.ThrowIfCancellationRequested();

            using var stream = _context.OpenFile(info.rootADT);
            var adt = new Adt(stream);

            if (adt.areaIDmap == null) return null;

            // Convert area map to RGBA with deterministic colors
            var pixelData = new byte[HeightTileSize * HeightTileSize * 4];
            var areaIds = new uint[HeightTileSize * HeightTileSize];
            int idx = 0;

            for (int py = 0; py < HeightTileSize; py++)
            {
                for (int px = 0; px < HeightTileSize; px++)
                {
                    uint areaId = adt.areaIDmap[py, px];

                    // Store area ID for tooltip lookup
                    areaIds[py * HeightTileSize + px] = areaId;

                    // Get or create color for this area
                    if (!AreaColors.TryGetValue(areaId, out uint color))
                    {
                        color = GenerateAreaColor(areaId);
                        AreaColors.TryAdd(areaId, color);
                    }

                    // Extract RGBA from packed color
                    pixelData[idx++] = (byte)(color >> 24);        // R
                    pixelData[idx++] = (byte)((color >> 16) & 0xFF); // G
                    pixelData[idx++] = (byte)((color >> 8) & 0xFF);  // B
                    pixelData[idx++] = (byte)(color & 0xFF);         // A
                }
            }

            // Store area IDs for this tile
            AreaIdMap[(x, y)] = areaIds;

            return new TileUpdate
            {
                TileX = x,
                TileY = y,
                Layer = LayerType.Area,
                PixelData = pixelData,
                Width = HeightTileSize,
                Height = HeightTileSize
            };
        }
        catch
        {
            return null;
        }
    }

    private static uint GenerateAreaColor(uint areaId)
    {
        // Use area ID as seed for deterministic color generation
        var random = new Random((int)areaId);

        byte r = (byte)random.Next(64, 256);
        byte g = (byte)random.Next(64, 256);
        byte b = (byte)random.Next(64, 256);
        byte a = 255; // Full opacity

        return (uint)((r << 24) | (g << 16) | (b << 8) | a);
    }

    public void Dispose()
    {
        Cancel();

        // Wait for task to complete before disposing
        // Use a short timeout to avoid blocking indefinitely
        if (_loadingTask != null)
        {
            try
            {
                // Wait for the task to complete (it should be cancelled)
                _loadingTask.Wait(TimeSpan.FromSeconds(2));
            }
            catch (AggregateException)
            {
                // Task was cancelled or faulted, that's expected
            }
            catch (OperationCanceledException)
            {
                // Task was cancelled, that's expected
            }

            // Only dispose if the task is in a completion state
            if (_loadingTask.IsCompleted)
            {
                _loadingTask.Dispose();
            }
        }
    }
}
