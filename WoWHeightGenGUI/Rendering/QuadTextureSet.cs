using Silk.NET.OpenGL;
using WoWHeightGenGUI.Models;

namespace WoWHeightGenGUI.Rendering;

/// <summary>
/// GPU-side textures for a single quad's three layers.
/// Manages OpenGL texture handles and provides binding methods.
/// </summary>
public class QuadTextureSet : IDisposable
{
    private readonly GL _gl;
    private bool _disposed;

    /// <summary>
    /// The coordinate of this quad in the grid
    /// </summary>
    public QuadCoord Coord { get; }

    /// <summary>
    /// OpenGL texture handle for minimap layer
    /// </summary>
    public uint MinimapHandle { get; private set; }

    // Backing fields for height and area handles (needed for ref parameter in upload)
    private uint _heightHandle;
    private uint _areaHandle;

    /// <summary>
    /// OpenGL texture handle for height layer
    /// </summary>
    public uint HeightHandle => _heightHandle;

    /// <summary>
    /// OpenGL texture handle for area layer
    /// </summary>
    public uint AreaHandle => _areaHandle;

    /// <summary>
    /// Size of the minimap texture (width = height)
    /// </summary>
    public int MinimapSize { get; private set; }

    /// <summary>
    /// Size of height/area textures (always 128 - one ADT tile)
    /// </summary>
    public int HeightAreaSize => QuadCoord.HeightPixelsPerQuad;

    /// <summary>
    /// Frame number when this quad was last used (for LRU eviction)
    /// </summary>
    public long LastUsedFrame { get; set; }

    /// <summary>
    /// Whether any textures have been uploaded
    /// </summary>
    public bool IsInitialized => MinimapHandle != 0 || HeightHandle != 0 || AreaHandle != 0;

    /// <summary>
    /// Whether the minimap texture is valid
    /// </summary>
    public bool HasMinimap => MinimapHandle != 0;

    /// <summary>
    /// Whether the height texture is valid
    /// </summary>
    public bool HasHeight => HeightHandle != 0;

    /// <summary>
    /// Whether the area texture is valid
    /// </summary>
    public bool HasArea => AreaHandle != 0;

    public QuadTextureSet(GL gl, QuadCoord coord)
    {
        _gl = gl;
        Coord = coord;
    }

    /// <summary>
    /// Upload quad data to GPU textures
    /// </summary>
    public void Upload(QuadData data)
    {
        if (data.MinimapPixels != null && data.MinimapDirty)
        {
            UploadMinimap(data.MinimapPixels, data.MinimapSize);
        }

        if (data.HeightPixels != null && data.HeightDirty)
        {
            UploadHeightArea(ref _heightHandle, data.HeightPixels, HeightAreaSize);
        }

        if (data.AreaPixels != null && data.AreaDirty)
        {
            UploadHeightArea(ref _areaHandle, data.AreaPixels, HeightAreaSize);
        }
    }

    private unsafe void UploadMinimap(byte[] pixels, int size)
    {
        if (MinimapHandle == 0)
        {
            MinimapHandle = _gl.GenTexture();
        }

        MinimapSize = size;

        _gl.BindTexture(TextureTarget.Texture2D, MinimapHandle);

        fixed (byte* ptr = pixels)
        {
            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8,
                (uint)size, (uint)size, 0, PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
        }

        SetTextureParameters();
        _gl.BindTexture(TextureTarget.Texture2D, 0);
    }

    private unsafe void UploadHeightArea(ref uint handle, byte[] pixels, int size)
    {
        if (handle == 0)
        {
            handle = _gl.GenTexture();
        }

        _gl.BindTexture(TextureTarget.Texture2D, handle);

        fixed (byte* ptr = pixels)
        {
            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8,
                (uint)size, (uint)size, 0, PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
        }

        SetTextureParameters();
        _gl.BindTexture(TextureTarget.Texture2D, 0);
    }

    private void SetTextureParameters()
    {
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
    }

    /// <summary>
    /// Bind the minimap texture to the specified texture unit
    /// </summary>
    public void BindMinimap(TextureUnit unit)
    {
        _gl.ActiveTexture(unit);
        _gl.BindTexture(TextureTarget.Texture2D, MinimapHandle);
    }

    /// <summary>
    /// Bind the height texture to the specified texture unit
    /// </summary>
    public void BindHeight(TextureUnit unit)
    {
        _gl.ActiveTexture(unit);
        _gl.BindTexture(TextureTarget.Texture2D, _heightHandle);
    }

    /// <summary>
    /// Bind the area texture to the specified texture unit
    /// </summary>
    public void BindArea(TextureUnit unit)
    {
        _gl.ActiveTexture(unit);
        _gl.BindTexture(TextureTarget.Texture2D, _areaHandle);
    }

    /// <summary>
    /// Read pixels from the minimap texture
    /// </summary>
    public unsafe byte[]? ReadMinimapPixels()
    {
        if (MinimapHandle == 0) return null;

        var pixels = new byte[MinimapSize * MinimapSize * 4];
        _gl.BindTexture(TextureTarget.Texture2D, MinimapHandle);

        fixed (byte* ptr = pixels)
        {
            _gl.GetTexImage(TextureTarget.Texture2D, 0, PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
        }

        _gl.BindTexture(TextureTarget.Texture2D, 0);
        return pixels;
    }

    /// <summary>
    /// Read pixels from the height texture
    /// </summary>
    public unsafe byte[]? ReadHeightPixels()
    {
        if (_heightHandle == 0) return null;

        var pixels = new byte[HeightAreaSize * HeightAreaSize * 4];
        _gl.BindTexture(TextureTarget.Texture2D, _heightHandle);

        fixed (byte* ptr = pixels)
        {
            _gl.GetTexImage(TextureTarget.Texture2D, 0, PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
        }

        _gl.BindTexture(TextureTarget.Texture2D, 0);
        return pixels;
    }

    /// <summary>
    /// Read pixels from the area texture
    /// </summary>
    public unsafe byte[]? ReadAreaPixels()
    {
        if (_areaHandle == 0) return null;

        var pixels = new byte[HeightAreaSize * HeightAreaSize * 4];
        _gl.BindTexture(TextureTarget.Texture2D, _areaHandle);

        fixed (byte* ptr = pixels)
        {
            _gl.GetTexImage(TextureTarget.Texture2D, 0, PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
        }

        _gl.BindTexture(TextureTarget.Texture2D, 0);
        return pixels;
    }

    /// <summary>
    /// Estimated GPU memory usage in bytes
    /// </summary>
    public long EstimatedMemoryBytes
    {
        get
        {
            long total = 0;
            if (MinimapHandle != 0)
                total += MinimapSize * MinimapSize * 4;
            if (_heightHandle != 0)
                total += HeightAreaSize * HeightAreaSize * 4;
            if (_areaHandle != 0)
                total += HeightAreaSize * HeightAreaSize * 4;
            return total;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        if (MinimapHandle != 0)
        {
            _gl.DeleteTexture(MinimapHandle);
            MinimapHandle = 0;
        }

        if (_heightHandle != 0)
        {
            _gl.DeleteTexture(_heightHandle);
            _heightHandle = 0;
        }

        if (_areaHandle != 0)
        {
            _gl.DeleteTexture(_areaHandle);
            _areaHandle = 0;
        }

        _disposed = true;
    }
}
