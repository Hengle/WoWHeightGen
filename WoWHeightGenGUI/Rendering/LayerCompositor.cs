using System.Numerics;
using System.Reflection;
using Silk.NET.OpenGL;
using WoWHeightGenGUI.Models;

namespace WoWHeightGenGUI.Rendering;

/// <summary>
/// Handles GPU-based compositing of map layers using OpenGL shaders.
/// Supports both full-screen rendering and quad-based rendering.
/// </summary>
public class LayerCompositor : IDisposable
{
    private readonly GL _gl;
    private uint _shaderProgram;
    private uint _vao;
    private uint _vbo;
    private uint _ebo;
    private bool _disposed;

    // Shader uniform locations
    private int _uViewProjection;
    private int _uMinimapTex;
    private int _uHeightTex;
    private int _uAreaTex;
    private int _uColormapTex;
    private int _uOpacities;
    private int _uVisibilities;
    private int _uBlendModes;
    private int _uColormapType;
    private int _uColormapSize;
    private int _uAreaHighlightMode;
    private int _uHighlightAreas1;
    private int _uHighlightAreas2;
    private int _uHighlightCount;

    // Quad positioning uniforms (world space)
    private int _uQuadWorldPos;
    private int _uQuadWorldSize;

    /// <summary>
    /// Colormap presets texture
    /// </summary>
    public ColormapPresets? Colormaps { get; private set; }

    public LayerCompositor(GL gl)
    {
        _gl = gl;
    }

    /// <summary>
    /// Initialize the compositor (compile shaders, create buffers)
    /// </summary>
    public void Initialize()
    {
        // Create and compile shaders
        var vertexSource = LoadShaderSource("layer_composite.vert");
        var fragmentSource = LoadShaderSource("layer_composite.frag");

        var vertexShader = CompileShader(ShaderType.VertexShader, vertexSource);
        var fragmentShader = CompileShader(ShaderType.FragmentShader, fragmentSource);

        _shaderProgram = _gl.CreateProgram();
        _gl.AttachShader(_shaderProgram, vertexShader);
        _gl.AttachShader(_shaderProgram, fragmentShader);
        _gl.LinkProgram(_shaderProgram);

        // Check for linking errors
        _gl.GetProgram(_shaderProgram, ProgramPropertyARB.LinkStatus, out int linkStatus);
        if (linkStatus == 0)
        {
            var infoLog = _gl.GetProgramInfoLog(_shaderProgram);
            throw new Exception($"Shader program linking failed: {infoLog}");
        }

        // Clean up individual shaders
        _gl.DetachShader(_shaderProgram, vertexShader);
        _gl.DetachShader(_shaderProgram, fragmentShader);
        _gl.DeleteShader(vertexShader);
        _gl.DeleteShader(fragmentShader);

        // Get uniform locations
        _uViewProjection = _gl.GetUniformLocation(_shaderProgram, "uViewProjection");
        _uMinimapTex = _gl.GetUniformLocation(_shaderProgram, "uMinimapTex");
        _uHeightTex = _gl.GetUniformLocation(_shaderProgram, "uHeightTex");
        _uAreaTex = _gl.GetUniformLocation(_shaderProgram, "uAreaTex");
        _uColormapTex = _gl.GetUniformLocation(_shaderProgram, "uColormapTex");
        _uOpacities = _gl.GetUniformLocation(_shaderProgram, "uOpacities");
        _uVisibilities = _gl.GetUniformLocation(_shaderProgram, "uVisibilities");
        _uBlendModes = _gl.GetUniformLocation(_shaderProgram, "uBlendModes");
        _uColormapType = _gl.GetUniformLocation(_shaderProgram, "uColormapType");
        _uColormapSize = _gl.GetUniformLocation(_shaderProgram, "uColormapSize");
        _uAreaHighlightMode = _gl.GetUniformLocation(_shaderProgram, "uAreaHighlightMode");
        _uHighlightAreas1 = _gl.GetUniformLocation(_shaderProgram, "uHighlightAreas1");
        _uHighlightAreas2 = _gl.GetUniformLocation(_shaderProgram, "uHighlightAreas2");
        _uHighlightCount = _gl.GetUniformLocation(_shaderProgram, "uHighlightCount");

        // Quad positioning uniforms (world space)
        _uQuadWorldPos = _gl.GetUniformLocation(_shaderProgram, "uQuadWorldPos");
        _uQuadWorldSize = _gl.GetUniformLocation(_shaderProgram, "uQuadWorldSize");

        // Create geometry buffers (fullscreen quad)
        CreateQuadBuffers();

        // Initialize colormap texture
        Colormaps = new ColormapPresets(_gl);
        Colormaps.Initialize();
    }

    private unsafe void CreateQuadBuffers()
    {
        // Vertex data: position (2) + texcoord (2)
        // Quad from -1 to 1, tex coords from 0 to 1
        float[] vertices =
        {
            // Position     // TexCoord
            -1.0f, -1.0f,   0.0f, 1.0f,  // Bottom-left (flipped Y for OpenGL)
             1.0f, -1.0f,   1.0f, 1.0f,  // Bottom-right
             1.0f,  1.0f,   1.0f, 0.0f,  // Top-right
            -1.0f,  1.0f,   0.0f, 0.0f   // Top-left
        };

        uint[] indices =
        {
            0, 1, 2,
            2, 3, 0
        };

        _vao = _gl.GenVertexArray();
        _vbo = _gl.GenBuffer();
        _ebo = _gl.GenBuffer();

        _gl.BindVertexArray(_vao);

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        fixed (float* v = vertices)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertices.Length * sizeof(float)), v, BufferUsageARB.StaticDraw);
        }

        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
        fixed (uint* i = indices)
        {
            _gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(indices.Length * sizeof(uint)), i, BufferUsageARB.StaticDraw);
        }

        // Position attribute
        _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)0);
        _gl.EnableVertexAttribArray(0);

        // TexCoord attribute
        _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)(2 * sizeof(float)));
        _gl.EnableVertexAttribArray(1);

        _gl.BindVertexArray(0);
    }

    /// <summary>
    /// Render a single quad using QuadTextureSet textures.
    /// </summary>
    /// <param name="quad">The quad textures to render</param>
    /// <param name="layerStates">Layer visibility/opacity/blend settings</param>
    /// <param name="viewProjection">View-projection matrix from Camera2D</param>
    /// <param name="quadWorldPos">Quad position in world space (top-left corner)</param>
    /// <param name="quadWorldSize">Quad size in world units (1.0 for standard tiles)</param>
    public unsafe void RenderQuad(
        QuadTextureSet quad,
        LayerState[] layerStates,
        Matrix4x4 viewProjection,
        Vector2 quadWorldPos,
        float quadWorldSize = 1.0f)
    {
        _gl.UseProgram(_shaderProgram);

        // Set view-projection matrix
        _gl.UniformMatrix4(_uViewProjection, 1, false, (float*)&viewProjection);

        // Set quad world positioning
        _gl.Uniform2(_uQuadWorldPos, quadWorldPos.X, quadWorldPos.Y);
        _gl.Uniform1(_uQuadWorldSize, quadWorldSize);

        // Bind quad textures
        if (quad.HasMinimap)
        {
            quad.BindMinimap(TextureUnit.Texture0);
        }
        _gl.Uniform1(_uMinimapTex, 0);

        if (quad.HasHeight)
        {
            quad.BindHeight(TextureUnit.Texture1);
        }
        _gl.Uniform1(_uHeightTex, 1);

        if (quad.HasArea)
        {
            quad.BindArea(TextureUnit.Texture2);
        }
        _gl.Uniform1(_uAreaTex, 2);

        // Bind colormap texture
        Colormaps?.Bind(TextureUnit.Texture3);
        _gl.Uniform1(_uColormapTex, 3);

        // Set layer state uniforms
        SetLayerStateUniforms(layerStates);

        // Draw the quad
        _gl.BindVertexArray(_vao);
        _gl.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, (void*)0);
        _gl.BindVertexArray(0);

        _gl.UseProgram(0);
    }

    /// <summary>
    /// Render the composited layers using LayerTexture (full-screen mode for export).
    /// </summary>
    public unsafe void Render(
        LayerTexture? minimapTexture,
        LayerTexture? heightTexture,
        LayerTexture? areaTexture,
        LayerState[] layerStates,
        Matrix4x4 viewProjection)
    {
        _gl.UseProgram(_shaderProgram);

        // Set view-projection matrix
        _gl.UniformMatrix4(_uViewProjection, 1, false, (float*)&viewProjection);

        // Set default quad uniforms (full screen: pos at origin, size covers full map)
        _gl.Uniform2(_uQuadWorldPos, 0.0f, 0.0f);
        _gl.Uniform1(_uQuadWorldSize, Camera2D.MapSize);

        // Bind layer textures
        if (minimapTexture?.IsInitialized == true)
        {
            minimapTexture.Bind(TextureUnit.Texture0);
        }
        _gl.Uniform1(_uMinimapTex, 0);

        if (heightTexture?.IsInitialized == true)
        {
            heightTexture.Bind(TextureUnit.Texture1);
        }
        _gl.Uniform1(_uHeightTex, 1);

        if (areaTexture?.IsInitialized == true)
        {
            areaTexture.Bind(TextureUnit.Texture2);
        }
        _gl.Uniform1(_uAreaTex, 2);

        // Bind colormap texture
        Colormaps?.Bind(TextureUnit.Texture3);
        _gl.Uniform1(_uColormapTex, 3);

        // Set layer state uniforms
        SetLayerStateUniforms(layerStates);

        // Draw the quad
        _gl.BindVertexArray(_vao);
        _gl.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, (void*)0);
        _gl.BindVertexArray(0);

        _gl.UseProgram(0);
    }

    /// <summary>
    /// Set layer state uniforms (shared between Render and RenderQuad)
    /// </summary>
    private unsafe void SetLayerStateUniforms(LayerState[] layerStates)
    {
        var minimapState = layerStates[(int)LayerType.Minimap];
        var heightState = layerStates[(int)LayerType.Height];
        var areaState = layerStates[(int)LayerType.Area];

        _gl.Uniform3(_uOpacities, minimapState.Opacity, heightState.Opacity, areaState.Opacity);

        // Set layer visibilities
        _gl.Uniform3(_uVisibilities,
            minimapState.IsVisible ? 1 : 0,
            heightState.IsVisible ? 1 : 0,
            areaState.IsVisible ? 1 : 0);

        // Set blend modes
        _gl.Uniform3(_uBlendModes,
            (int)minimapState.BlendMode,
            (int)heightState.BlendMode,
            (int)areaState.BlendMode);

        // Set height colormap
        _gl.Uniform1(_uColormapType, (int)heightState.HeightColormap);
        _gl.Uniform1(_uColormapSize, ColormapPresets.SamplesPerColormap);

        // Set area highlight settings
        _gl.Uniform1(_uAreaHighlightMode, areaState.ShowAllAreas ? 0 : 1);

        var highlightIds = areaState.HighlightedAreaIds.Take(8).ToArray();
        int[] highlight1 = new int[4];
        int[] highlight2 = new int[4];

        for (int i = 0; i < Math.Min(4, highlightIds.Length); i++)
            highlight1[i] = (int)highlightIds[i];
        for (int i = 0; i < Math.Min(4, Math.Max(0, highlightIds.Length - 4)); i++)
            highlight2[i] = (int)highlightIds[i + 4];

        fixed (int* h1 = highlight1)
            _gl.Uniform4(_uHighlightAreas1, 1, h1);
        fixed (int* h2 = highlight2)
            _gl.Uniform4(_uHighlightAreas2, 1, h2);

        _gl.Uniform1(_uHighlightCount, highlightIds.Length);
    }

    private uint CompileShader(ShaderType type, string source)
    {
        var shader = _gl.CreateShader(type);
        _gl.ShaderSource(shader, source);
        _gl.CompileShader(shader);

        _gl.GetShader(shader, ShaderParameterName.CompileStatus, out int compileStatus);
        if (compileStatus == 0)
        {
            var infoLog = _gl.GetShaderInfoLog(shader);
            throw new Exception($"{type} compilation failed: {infoLog}");
        }

        return shader;
    }

    private static string LoadShaderSource(string filename)
    {
        // Try to load from embedded resource first
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"WoWHeightGenGUI.Rendering.Shaders.{filename}";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream != null)
        {
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        // Fallback to file system
        var basePath = AppDomain.CurrentDomain.BaseDirectory;
        var filePath = Path.Combine(basePath, "Rendering", "Shaders", filename);

        if (File.Exists(filePath))
        {
            return File.ReadAllText(filePath);
        }

        // Try relative path from current directory
        filePath = Path.Combine("Rendering", "Shaders", filename);
        if (File.Exists(filePath))
        {
            return File.ReadAllText(filePath);
        }

        throw new FileNotFoundException($"Could not find shader file: {filename}");
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Colormaps?.Dispose();

        if (_shaderProgram != 0)
        {
            _gl.DeleteProgram(_shaderProgram);
            _shaderProgram = 0;
        }

        if (_vao != 0)
        {
            _gl.DeleteVertexArray(_vao);
            _vao = 0;
        }

        if (_vbo != 0)
        {
            _gl.DeleteBuffer(_vbo);
            _vbo = 0;
        }

        if (_ebo != 0)
        {
            _gl.DeleteBuffer(_ebo);
            _ebo = 0;
        }

        _disposed = true;
    }
}
