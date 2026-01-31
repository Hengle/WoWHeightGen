using Silk.NET.OpenGL;
using WoWHeightGenGUI.Models;

namespace WoWHeightGenGUI.Rendering;

/// <summary>
/// Provides colormap presets for height map visualization.
/// Creates a 1D texture containing all colormap presets.
/// </summary>
public class ColormapPresets : IDisposable
{
    private readonly GL _gl;
    private uint _textureHandle;
    private bool _disposed;

    /// <summary>
    /// Number of samples per colormap
    /// </summary>
    public const int SamplesPerColormap = 256;

    /// <summary>
    /// Number of available colormaps
    /// </summary>
    public const int ColormapCount = 4;

    /// <summary>
    /// Total texture width (all colormaps side by side)
    /// </summary>
    public const int TotalWidth = SamplesPerColormap * ColormapCount;

    /// <summary>
    /// OpenGL texture handle
    /// </summary>
    public uint Handle => _textureHandle;

    public ColormapPresets(GL gl)
    {
        _gl = gl;
    }

    /// <summary>
    /// Initialize the colormap texture with all presets
    /// </summary>
    public unsafe void Initialize()
    {
        if (_textureHandle != 0)
        {
            _gl.DeleteTexture(_textureHandle);
        }

        _textureHandle = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture1D, _textureHandle);

        // Generate all colormaps
        var data = new byte[TotalWidth * 4]; // RGBA

        GenerateGrayscale(data, 0);
        GenerateTerrain(data, SamplesPerColormap);
        GenerateViridis(data, SamplesPerColormap * 2);
        GenerateHeatmap(data, SamplesPerColormap * 3);

        fixed (byte* ptr = data)
        {
            _gl.TexImage1D(TextureTarget.Texture1D, 0, InternalFormat.Rgba8,
                TotalWidth, 0, PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
        }

        _gl.TexParameter(TextureTarget.Texture1D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture1D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture1D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);

        _gl.BindTexture(TextureTarget.Texture1D, 0);
    }

    /// <summary>
    /// Bind the colormap texture to the specified texture unit
    /// </summary>
    public void Bind(TextureUnit unit)
    {
        _gl.ActiveTexture(unit);
        _gl.BindTexture(TextureTarget.Texture1D, _textureHandle);
    }

    private static void GenerateGrayscale(byte[] data, int offset)
    {
        for (int i = 0; i < SamplesPerColormap; i++)
        {
            byte v = (byte)i;
            int idx = (offset + i) * 4;
            data[idx] = v;     // R
            data[idx + 1] = v; // G
            data[idx + 2] = v; // B
            data[idx + 3] = 255; // A
        }
    }

    private static void GenerateTerrain(byte[] data, int offset)
    {
        // Terrain colormap: deep blue -> green -> yellow/brown -> white
        // 0-0.1: Deep water (dark blue)
        // 0.1-0.2: Shallow water (lighter blue)
        // 0.2-0.4: Lowlands (green)
        // 0.4-0.7: Hills (yellow/brown)
        // 0.7-1.0: Mountains/Snow (gray to white)

        for (int i = 0; i < SamplesPerColormap; i++)
        {
            float t = i / (float)(SamplesPerColormap - 1);
            int idx = (offset + i) * 4;

            byte r, g, b;

            if (t < 0.1f)
            {
                // Deep water
                r = 0;
                g = (byte)(30 + t * 400);
                b = (byte)(100 + t * 500);
            }
            else if (t < 0.2f)
            {
                // Shallow water
                float lt = (t - 0.1f) / 0.1f;
                r = (byte)(lt * 50);
                g = (byte)(70 + lt * 80);
                b = (byte)(150 + lt * 50);
            }
            else if (t < 0.4f)
            {
                // Lowlands (green)
                float lt = (t - 0.2f) / 0.2f;
                r = (byte)(50 + lt * 80);
                g = (byte)(150 - lt * 30);
                b = (byte)(200 - lt * 150);
            }
            else if (t < 0.7f)
            {
                // Hills (yellow/brown)
                float lt = (t - 0.4f) / 0.3f;
                r = (byte)(130 + lt * 60);
                g = (byte)(120 - lt * 40);
                b = (byte)(50 + lt * 30);
            }
            else
            {
                // Mountains/Snow
                float lt = (t - 0.7f) / 0.3f;
                r = (byte)(190 + lt * 65);
                g = (byte)(80 + lt * 175);
                b = (byte)(80 + lt * 175);
            }

            data[idx] = r;
            data[idx + 1] = g;
            data[idx + 2] = b;
            data[idx + 3] = 255;
        }
    }

    private static void GenerateViridis(byte[] data, int offset)
    {
        // Viridis colormap: purple -> blue -> green -> yellow
        // Perceptually uniform colormap

        // Key color stops for Viridis
        var stops = new (float t, byte r, byte g, byte b)[]
        {
            (0.0f, 68, 1, 84),
            (0.25f, 59, 82, 139),
            (0.5f, 33, 144, 140),
            (0.75f, 93, 201, 99),
            (1.0f, 253, 231, 37)
        };

        for (int i = 0; i < SamplesPerColormap; i++)
        {
            float t = i / (float)(SamplesPerColormap - 1);
            int idx = (offset + i) * 4;

            // Find the two stops to interpolate between
            int stopIdx = 0;
            for (int j = 1; j < stops.Length; j++)
            {
                if (t <= stops[j].t)
                {
                    stopIdx = j - 1;
                    break;
                }
            }

            var s0 = stops[stopIdx];
            var s1 = stops[Math.Min(stopIdx + 1, stops.Length - 1)];

            float lt = (s1.t > s0.t) ? (t - s0.t) / (s1.t - s0.t) : 0;

            data[idx] = (byte)(s0.r + (s1.r - s0.r) * lt);
            data[idx + 1] = (byte)(s0.g + (s1.g - s0.g) * lt);
            data[idx + 2] = (byte)(s0.b + (s1.b - s0.b) * lt);
            data[idx + 3] = 255;
        }
    }

    private static void GenerateHeatmap(byte[] data, int offset)
    {
        // Heatmap: blue -> cyan -> green -> yellow -> red
        for (int i = 0; i < SamplesPerColormap; i++)
        {
            float t = i / (float)(SamplesPerColormap - 1);
            int idx = (offset + i) * 4;

            byte r, g, b;

            if (t < 0.25f)
            {
                // Blue to Cyan
                float lt = t / 0.25f;
                r = 0;
                g = (byte)(lt * 255);
                b = 255;
            }
            else if (t < 0.5f)
            {
                // Cyan to Green
                float lt = (t - 0.25f) / 0.25f;
                r = 0;
                g = 255;
                b = (byte)(255 - lt * 255);
            }
            else if (t < 0.75f)
            {
                // Green to Yellow
                float lt = (t - 0.5f) / 0.25f;
                r = (byte)(lt * 255);
                g = 255;
                b = 0;
            }
            else
            {
                // Yellow to Red
                float lt = (t - 0.75f) / 0.25f;
                r = 255;
                g = (byte)(255 - lt * 255);
                b = 0;
            }

            data[idx] = r;
            data[idx + 1] = g;
            data[idx + 2] = b;
            data[idx + 3] = 255;
        }
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
