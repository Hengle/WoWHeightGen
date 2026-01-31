using Silk.NET.OpenGL;
using WoWHeightGenGUI.Models;

namespace WoWHeightGenGUI.Rendering;

/// <summary>
/// Manages CPU and GPU storage for map quads with LRU eviction.
/// CPU side stores all loaded quad data, GPU side streams visible quads.
/// </summary>
public class QuadCache : IDisposable
{
    private readonly GL _gl;
    private readonly Dictionary<QuadCoord, QuadData> _cpuQuads = new();
    private readonly Dictionary<QuadCoord, QuadTextureSet> _gpuQuads = new();
    private readonly int _maxGpuQuads;
    private long _currentFrame;
    private bool _disposed;

    /// <summary>
    /// Detected minimap tile size (pixels per ADT for minimap layer)
    /// </summary>
    public int MinimapTileSize { get; set; } = 256;

    /// <summary>
    /// Number of quads currently stored in CPU memory
    /// </summary>
    public int CpuQuadCount => _cpuQuads.Count;

    /// <summary>
    /// Number of quads currently loaded in GPU memory
    /// </summary>
    public int GpuQuadCount => _gpuQuads.Count;

    /// <summary>
    /// Maximum number of quads allowed in GPU memory
    /// </summary>
    public int MaxGpuQuads => _maxGpuQuads;

    /// <summary>
    /// Current frame number (for LRU tracking)
    /// </summary>
    public long CurrentFrame => _currentFrame;

    /// <summary>
    /// Estimated total GPU memory usage in bytes
    /// </summary>
    public long TotalGpuMemoryBytes => _gpuQuads.Values.Sum(q => q.EstimatedMemoryBytes);

    /// <summary>
    /// Create a new quad cache
    /// </summary>
    /// <param name="gl">OpenGL context</param>
    /// <param name="maxGpuQuads">Maximum quads to keep in VRAM (default 4096 = full 64x64 grid).
    /// With 64x64 grid and 128x128 textures per quad, each quad uses ~192KB (3 layers).
    /// 4096 quads = ~768MB GPU memory max.</param>
    public QuadCache(GL gl, int maxGpuQuads = 4096)
    {
        _gl = gl;
        _maxGpuQuads = maxGpuQuads;
    }

    /// <summary>
    /// Get or create CPU quad data for the given coordinate
    /// </summary>
    public QuadData GetOrCreateCpuQuad(QuadCoord coord)
    {
        if (!_cpuQuads.TryGetValue(coord, out var quad))
        {
            quad = new QuadData(coord);
            _cpuQuads[coord] = quad;
        }
        return quad;
    }

    /// <summary>
    /// Get CPU quad data if it exists
    /// </summary>
    public QuadData? GetCpuQuad(QuadCoord coord)
    {
        return _cpuQuads.GetValueOrDefault(coord);
    }

    /// <summary>
    /// Check if a quad exists in CPU cache
    /// </summary>
    public bool HasCpuQuad(QuadCoord coord) => _cpuQuads.ContainsKey(coord);

    /// <summary>
    /// Get GPU quad if loaded
    /// </summary>
    public QuadTextureSet? GetGpuQuad(QuadCoord coord)
    {
        if (_gpuQuads.TryGetValue(coord, out var quad))
        {
            quad.LastUsedFrame = _currentFrame;
            return quad;
        }
        return null;
    }

    /// <summary>
    /// Check if a quad is loaded in GPU
    /// </summary>
    public bool HasGpuQuad(QuadCoord coord) => _gpuQuads.ContainsKey(coord);

    /// <summary>
    /// Ensure a specific quad is loaded in GPU (upload if needed)
    /// </summary>
    public QuadTextureSet? EnsureGpuQuad(QuadCoord coord)
    {
        // Check if already in GPU
        if (_gpuQuads.TryGetValue(coord, out var existing))
        {
            existing.LastUsedFrame = _currentFrame;

            // Upload any dirty data
            var cpuData = GetCpuQuad(coord);
            if (cpuData != null && cpuData.IsDirty)
            {
                existing.Upload(cpuData);
                cpuData.MarkClean();
            }

            return existing;
        }

        // Get CPU data
        var quadData = GetCpuQuad(coord);
        if (quadData == null || !quadData.HasAnyData)
            return null;

        // Evict if at capacity
        if (_gpuQuads.Count >= _maxGpuQuads)
        {
            EvictLruQuads(1);
        }

        // Create and upload
        var gpuQuad = new QuadTextureSet(_gl, coord);
        gpuQuad.Upload(quadData);
        gpuQuad.LastUsedFrame = _currentFrame;
        quadData.MarkClean();

        _gpuQuads[coord] = gpuQuad;
        return gpuQuad;
    }

    /// <summary>
    /// Ensure all required quads are loaded in GPU
    /// </summary>
    public void EnsureQuadsLoaded(IEnumerable<QuadCoord> requiredQuads)
    {
        var required = requiredQuads.ToHashSet();

        // First, update frame counter for already-loaded quads
        foreach (var coord in required)
        {
            if (_gpuQuads.TryGetValue(coord, out var existing))
            {
                existing.LastUsedFrame = _currentFrame;
            }
        }

        // Find quads that need to be loaded
        var toLoad = required.Where(c => !_gpuQuads.ContainsKey(c) && HasCpuQuad(c)).ToList();

        // Evict enough quads to make room
        int currentInUse = required.Count(c => _gpuQuads.ContainsKey(c));
        int spaceNeeded = toLoad.Count;
        int available = _maxGpuQuads - currentInUse;

        if (spaceNeeded > available)
        {
            // Need to evict some quads (prefer ones not in required set)
            int toEvict = spaceNeeded - available;
            EvictLruQuads(toEvict, required);
        }

        // Load new quads
        foreach (var coord in toLoad)
        {
            if (_gpuQuads.Count >= _maxGpuQuads)
                break;

            var cpuData = GetCpuQuad(coord);
            if (cpuData == null || !cpuData.HasAnyData)
                continue;

            var gpuQuad = new QuadTextureSet(_gl, coord);
            gpuQuad.Upload(cpuData);
            gpuQuad.LastUsedFrame = _currentFrame;
            cpuData.MarkClean();

            _gpuQuads[coord] = gpuQuad;
        }
    }

    /// <summary>
    /// Evict least recently used quads from GPU
    /// </summary>
    /// <param name="count">Number of quads to evict</param>
    /// <param name="exclude">Optional set of coords to never evict</param>
    public void EvictLruQuads(int count, HashSet<QuadCoord>? exclude = null)
    {
        if (count <= 0 || _gpuQuads.Count == 0)
            return;

        // Sort by last used frame (oldest first)
        var candidates = _gpuQuads
            .Where(kv => exclude == null || !exclude.Contains(kv.Key))
            .OrderBy(kv => kv.Value.LastUsedFrame)
            .Take(count)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var coord in candidates)
        {
            if (_gpuQuads.TryGetValue(coord, out var quad))
            {
                quad.Dispose();
                _gpuQuads.Remove(coord);
            }
        }
    }

    /// <summary>
    /// Evict all GPU quads except those in the required set
    /// </summary>
    public void EvictAllExcept(HashSet<QuadCoord> keep)
    {
        var toEvict = _gpuQuads.Keys.Where(c => !keep.Contains(c)).ToList();

        foreach (var coord in toEvict)
        {
            if (_gpuQuads.TryGetValue(coord, out var quad))
            {
                quad.Dispose();
                _gpuQuads.Remove(coord);
            }
        }
    }

    /// <summary>
    /// Increment the frame counter (call once per frame)
    /// </summary>
    public void TickFrame()
    {
        _currentFrame++;
    }

    /// <summary>
    /// Update dirty quads that are already in GPU
    /// </summary>
    public void UpdateDirtyGpuQuads()
    {
        foreach (var (coord, gpuQuad) in _gpuQuads)
        {
            var cpuData = GetCpuQuad(coord);
            if (cpuData != null && cpuData.IsDirty)
            {
                gpuQuad.Upload(cpuData);
                cpuData.MarkClean();
            }
        }
    }

    /// <summary>
    /// Get all quads that have any CPU data
    /// </summary>
    public IEnumerable<QuadCoord> GetLoadedQuadCoords()
    {
        return _cpuQuads.Where(kv => kv.Value.HasAnyData).Select(kv => kv.Key);
    }

    /// <summary>
    /// Get all GPU-loaded quad coordinates
    /// </summary>
    public IEnumerable<QuadCoord> GetGpuQuadCoords()
    {
        return _gpuQuads.Keys;
    }

    /// <summary>
    /// Clear all CPU and GPU data
    /// </summary>
    public void Clear()
    {
        foreach (var quad in _gpuQuads.Values)
        {
            quad.Dispose();
        }
        _gpuQuads.Clear();

        foreach (var quad in _cpuQuads.Values)
        {
            quad.Clear();
        }
        _cpuQuads.Clear();

        _currentFrame = 0;
    }

    /// <summary>
    /// Clear GPU data only (keep CPU data)
    /// </summary>
    public void ClearGpu()
    {
        foreach (var quad in _gpuQuads.Values)
        {
            quad.Dispose();
        }
        _gpuQuads.Clear();
    }

    public void Dispose()
    {
        if (_disposed) return;

        Clear();
        _disposed = true;
    }
}
