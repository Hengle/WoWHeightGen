using Silk.NET.OpenGL;
using WoWHeightGenGUI.Models;

namespace WoWHeightGenGUI.Rendering;

/// <summary>
/// Manages an OpenGL texture for a single map layer.
/// Supports partial updates for progressive loading.
/// </summary>
public class LayerTexture : IDisposable
{
    private readonly GL _gl;
    private uint _textureHandle;
    private bool _isInitialized;
    private bool _disposed;

    /// <summary>
    /// The layer type this texture represents
    /// </summary>
    public LayerType LayerType { get; }

    /// <summary>
    /// Width of the texture in pixels
    /// </summary>
    public int Width { get; private set; }

    /// <summary>
    /// Height of the texture in pixels
    /// </summary>
    public int Height { get; private set; }

    /// <summary>
    /// OpenGL texture handle
    /// </summary>
    public uint Handle => _textureHandle;

    /// <summary>
    /// Whether the texture has been initialized with data
    /// </summary>
    public bool IsInitialized => _isInitialized;

    public LayerTexture(GL gl, LayerType layerType)
    {
        _gl = gl;
        LayerType = layerType;
    }

    /// <summary>
    /// Initialize the texture with the given dimensions.
    /// Creates an empty RGBA texture.
    /// </summary>
    public unsafe void Initialize(int width, int height)
    {
        if (_textureHandle != 0)
        {
            _gl.DeleteTexture(_textureHandle);
        }

        Width = width;
        Height = height;

        _textureHandle = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, _textureHandle);

        // Allocate texture storage
        _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8,
            (uint)width, (uint)height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, null);

        // Set texture parameters
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

        _gl.BindTexture(TextureTarget.Texture2D, 0);
        _isInitialized = true;
    }

    /// <summary>
    /// Upload full texture data
    /// </summary>
    public unsafe void Upload(byte[] data, int width, int height)
    {
        if (width != Width || height != Height)
        {
            Initialize(width, height);
        }

        _gl.BindTexture(TextureTarget.Texture2D, _textureHandle);

        fixed (byte* ptr = data)
        {
            _gl.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0,
                (uint)width, (uint)height, PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
        }

        _gl.BindTexture(TextureTarget.Texture2D, 0);
    }

    /// <summary>
    /// Update a tile region of the texture.
    /// Used for progressive loading.
    /// </summary>
    public unsafe void UpdateTile(byte[] tileData, int tileX, int tileY, int tileWidth, int tileHeight)
    {
        if (!_isInitialized)
            return;

        int pixelX = tileX * tileWidth;
        int pixelY = tileY * tileHeight;

        // Bounds check
        if (pixelX + tileWidth > Width || pixelY + tileHeight > Height)
            return;

        _gl.BindTexture(TextureTarget.Texture2D, _textureHandle);

        fixed (byte* ptr = tileData)
        {
            _gl.TexSubImage2D(TextureTarget.Texture2D, 0,
                pixelX, pixelY, (uint)tileWidth, (uint)tileHeight,
                PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
        }

        _gl.BindTexture(TextureTarget.Texture2D, 0);
    }

    /// <summary>
    /// Clear the texture to a specific color
    /// </summary>
    public unsafe void Clear(byte r = 0, byte g = 0, byte b = 0, byte a = 0)
    {
        if (!_isInitialized)
            return;

        var clearData = new byte[Width * Height * 4];
        for (int i = 0; i < clearData.Length; i += 4)
        {
            clearData[i] = r;
            clearData[i + 1] = g;
            clearData[i + 2] = b;
            clearData[i + 3] = a;
        }

        Upload(clearData, Width, Height);
    }

    /// <summary>
    /// Bind the texture to the specified texture unit
    /// </summary>
    public void Bind(TextureUnit unit)
    {
        _gl.ActiveTexture(unit);
        _gl.BindTexture(TextureTarget.Texture2D, _textureHandle);
    }

    /// <summary>
    /// Read all pixel data from the texture
    /// </summary>
    /// <returns>RGBA pixel data, or null if texture is not initialized</returns>
    public unsafe byte[]? ReadPixels()
    {
        if (!_isInitialized || _textureHandle == 0)
            return null;

        var pixels = new byte[Width * Height * 4];

        _gl.BindTexture(TextureTarget.Texture2D, _textureHandle);

        fixed (byte* ptr = pixels)
        {
            _gl.GetTexImage(TextureTarget.Texture2D, 0, PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
        }

        _gl.BindTexture(TextureTarget.Texture2D, 0);

        return pixels;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        if (_textureHandle != 0)
        {
            _gl.DeleteTexture(_textureHandle);
            _textureHandle = 0;
        }

        _disposed = true;
    }
}
