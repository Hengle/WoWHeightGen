using ImGuiNET;
using Silk.NET.OpenGL;
using SereniaBLPLib;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using WoWHeightGenGUI.App;
using WoWHeightGenGUI.Rendering;

namespace WoWHeightGenGUI.UI.Panels;

public class TextureViewerPanel : IPanel
{
    private readonly Application _app;

    public string Name => "Texture Viewer";
    private bool _isVisible = false;
    public bool IsVisible { get => _isVisible; set => _isVisible = value; }

    private uint _currentFileId;
    private uint _glTextureId;
    private int _textureWidth;
    private int _textureHeight;
    private string? _errorMessage;

    private ImageViewport _viewport = new();

    public TextureViewerPanel(Application app)
    {
        _app = app;
    }

    public void LoadTexture(uint fileDataId)
    {
        _errorMessage = null;

        if (_app.Context == null)
        {
            _errorMessage = "Not connected to WoW";
            return;
        }

        if (!_app.Context.FileExists(fileDataId))
        {
            _errorMessage = $"File {fileDataId} not found";
            return;
        }

        try
        {
            // Clean up old texture
            if (_glTextureId != 0)
            {
                _app.GL.DeleteTexture(_glTextureId);
                _glTextureId = 0;
            }

            // Load BLP
            using var stream = _app.Context.OpenFile(fileDataId);
            using var blpFile = new BlpFile(stream);
            using var image = blpFile.GetImage(0);

            if (image == null)
            {
                _errorMessage = "Failed to decode BLP image";
                return;
            }

            _textureWidth = image.Width;
            _textureHeight = image.Height;

            // Extract pixel data
            byte[] pixels = new byte[_textureWidth * _textureHeight * 4];
            image.CopyPixelDataTo(pixels);

            // Upload to OpenGL
            _glTextureId = _app.GL.GenTexture();
            _app.GL.BindTexture(TextureTarget.Texture2D, _glTextureId);

            unsafe
            {
                fixed (byte* ptr = pixels)
                {
                    _app.GL.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8,
                        (uint)_textureWidth, (uint)_textureHeight, 0,
                        PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
                }
            }

            _app.GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            _app.GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            _app.GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            _app.GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            _app.GL.BindTexture(TextureTarget.Texture2D, 0);

            _currentFileId = fileDataId;
            _viewport.FitToViewport(new System.Numerics.Vector2(_textureWidth, _textureHeight));

            // Add to recent files
            _app.RecentFiles.AddEntry(fileDataId, Configuration.FileType.Blp,
                _app.Settings.Settings.WowProduct ?? "unknown");
        }
        catch (Exception ex)
        {
            _errorMessage = $"Error loading texture: {ex.Message}";
        }
    }

    public void Update(float deltaTime)
    {
    }

    public void Render()
    {
        if (ImGui.Begin(Name, ref _isVisible))
        {
            // Toolbar
            RenderToolbar();

            ImGui.Separator();

            if (!string.IsNullOrEmpty(_errorMessage))
            {
                ImGui.TextColored(new System.Numerics.Vector4(1, 0.5f, 0.5f, 1), _errorMessage);
            }
            else if (_glTextureId != 0)
            {
                RenderTextureView();
            }
            else
            {
                ImGui.TextDisabled("No texture loaded. Use File Explorer to open a BLP file.");
            }
        }
        ImGui.End();
    }

    private void RenderToolbar()
    {
        if (ImGui.Button("Fit"))
        {
            if (_textureWidth > 0 && _textureHeight > 0)
            {
                var contentRegion = ImGui.GetContentRegionAvail();
                _viewport.ViewportSize = contentRegion;
                _viewport.FitToViewport(new System.Numerics.Vector2(_textureWidth, _textureHeight));
            }
        }

        ImGui.SameLine();
        if (ImGui.Button("1:1"))
        {
            _viewport.Zoom = 1.0f;
            _viewport.Pan = System.Numerics.Vector2.Zero;
        }

        ImGui.SameLine();
        ImGui.Text($"Zoom: {_viewport.Zoom:P0}");

        if (_currentFileId != 0)
        {
            ImGui.SameLine();
            ImGui.TextDisabled($"| FileDataID: {_currentFileId} | {_textureWidth}x{_textureHeight}");
        }
    }

    private void RenderTextureView()
    {
        var contentRegion = ImGui.GetContentRegionAvail();
        _viewport.ViewportSize = contentRegion;

        // Handle input
        var cursorPos = ImGui.GetCursorScreenPos();

        // Create child region for image
        ImGui.BeginChild("TextureViewport", contentRegion, ImGuiChildFlags.None, ImGuiWindowFlags.NoScrollbar);

        // Calculate display size and position
        var displaySize = new System.Numerics.Vector2(_textureWidth, _textureHeight) * _viewport.Zoom;
        var center = contentRegion / 2 + _viewport.Pan;
        var imagePos = center - displaySize / 2;

        // Draw texture
        var drawList = ImGui.GetWindowDrawList();
        var screenPos = ImGui.GetCursorScreenPos();

        var uv0 = new System.Numerics.Vector2(0, 0);
        var uv1 = new System.Numerics.Vector2(1, 1);

        drawList.AddImage(
            (nint)_glTextureId,
            screenPos + imagePos,
            screenPos + imagePos + displaySize,
            uv0, uv1);

        // Handle mouse input
        if (ImGui.IsWindowHovered())
        {
            var io = ImGui.GetIO();

            // Zoom with scroll wheel
            if (io.MouseWheel != 0)
            {
                var mousePos = io.MousePos - screenPos;
                _viewport.HandleMouseWheel(io.MouseWheel, mousePos);
            }

            // Pan with middle mouse or left mouse + alt
            if (ImGui.IsMouseDragging(ImGuiMouseButton.Middle) ||
                (ImGui.IsMouseDragging(ImGuiMouseButton.Left) && io.KeyAlt))
            {
                _viewport.HandleMouseDrag(io.MouseDelta);
            }
        }

        ImGui.EndChild();
    }

    public void Dispose()
    {
        if (_glTextureId != 0)
        {
            try
            {
                _app.GL.DeleteTexture(_glTextureId);
            }
            catch
            {
                // GL context may already be destroyed during shutdown
            }
            _glTextureId = 0;
        }
    }
}
