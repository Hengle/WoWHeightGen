using ImGuiNET;
using WoWHeightGenGUI.App;

namespace WoWHeightGenGUI.UI.Panels;

public class TextureInfoPanel : IPanel
{
    private readonly Application _app;

    public string Name => "Texture Info";
    private bool _isVisible = false;
    public bool IsVisible { get => _isVisible; set => _isVisible = value; }

    // Current texture info
    public uint FileDataId { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string? Format { get; set; }
    public int MipCount { get; set; }
    public string? TextureType { get; set; }

    // Map info
    public float MinHeight { get; set; }
    public float MaxHeight { get; set; }
    public int AreaCount { get; set; }

    public TextureInfoPanel(Application app)
    {
        _app = app;
    }

    public void Update(float deltaTime)
    {
    }

    public void Render()
    {
        if (ImGui.Begin(Name, ref _isVisible))
        {
            if (FileDataId == 0)
            {
                ImGui.TextDisabled("No texture loaded");
            }
            else
            {
                RenderTextureInfo();
            }
        }
        ImGui.End();
    }

    private void RenderTextureInfo()
    {
        ImGui.Text("File Information");
        ImGui.Separator();

        ImGui.BulletText($"FileDataID: {FileDataId}");
        ImGui.BulletText($"Dimensions: {Width} x {Height}");

        if (!string.IsNullOrEmpty(Format))
            ImGui.BulletText($"Format: {Format}");

        if (MipCount > 0)
            ImGui.BulletText($"Mip Levels: {MipCount}");

        if (!string.IsNullOrEmpty(TextureType))
            ImGui.BulletText($"Type: {TextureType}");

        // Map-specific info
        if (MinHeight != 0 || MaxHeight != 0)
        {
            ImGui.Spacing();
            ImGui.Text("Height Map Info");
            ImGui.Separator();
            ImGui.BulletText($"Min Height: {MinHeight:F2}");
            ImGui.BulletText($"Max Height: {MaxHeight:F2}");
            ImGui.BulletText($"Range: {MaxHeight - MinHeight:F2}");
        }

        if (AreaCount > 0)
        {
            ImGui.Spacing();
            ImGui.Text("Area Map Info");
            ImGui.Separator();
            ImGui.BulletText($"Unique Areas: {AreaCount}");
        }

        // Memory estimate
        ImGui.Spacing();
        ImGui.Text("Memory Usage");
        ImGui.Separator();
        long bytes = (long)Width * Height * 4;
        string memStr = bytes < 1024 * 1024
            ? $"{bytes / 1024.0:F1} KB"
            : $"{bytes / (1024.0 * 1024.0):F1} MB";
        ImGui.BulletText($"GPU Memory: ~{memStr}");
    }

    public void SetTextureInfo(uint fileDataId, int width, int height, string? format = null, int mipCount = 0)
    {
        FileDataId = fileDataId;
        Width = width;
        Height = height;
        Format = format;
        MipCount = mipCount;
        TextureType = "BLP Texture";
        MinHeight = 0;
        MaxHeight = 0;
        AreaCount = 0;
    }

    public void SetHeightMapInfo(uint wdtId, int width, int height, float minHeight, float maxHeight)
    {
        FileDataId = wdtId;
        Width = width;
        Height = height;
        Format = "RGBA8";
        MipCount = 1;
        TextureType = "Height Map";
        MinHeight = minHeight;
        MaxHeight = maxHeight;
        AreaCount = 0;
    }

    public void SetAreaMapInfo(uint wdtId, int width, int height, int areaCount)
    {
        FileDataId = wdtId;
        Width = width;
        Height = height;
        Format = "RGBA8";
        MipCount = 1;
        TextureType = "Area Map";
        MinHeight = 0;
        MaxHeight = 0;
        AreaCount = areaCount;
    }

    public void Clear()
    {
        FileDataId = 0;
        Width = 0;
        Height = 0;
        Format = null;
        MipCount = 0;
        TextureType = null;
        MinHeight = 0;
        MaxHeight = 0;
        AreaCount = 0;
    }

    public void Dispose()
    {
    }
}
