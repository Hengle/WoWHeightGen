using ImGuiNET;
using WoWHeightGenGUI.App;
using WoWHeightGenGUI.Configuration;

namespace WoWHeightGenGUI.UI.Panels;

public class RecentFilesPanel : IPanel
{
    private readonly Application _app;

    public string Name => "Recent Files";
    private bool _isVisible = false;
    public bool IsVisible { get => _isVisible; set => _isVisible = value; }

    public RecentFilesPanel(Application app)
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
            var recentFiles = _app.RecentFiles.GetRecentFiles();

            if (recentFiles.Count == 0)
            {
                ImGui.TextDisabled("No recent files");
            }
            else
            {
                if (ImGui.Button("Clear All"))
                {
                    _app.RecentFiles.Clear();
                }

                ImGui.Separator();

                foreach (var entry in recentFiles)
                {
                    RenderFileEntry(entry);
                }
            }
        }
        ImGui.End();
    }

    private void RenderFileEntry(RecentFileEntry entry)
    {
        var icon = entry.Type switch
        {
            FileType.Blp => "[BLP]",
            FileType.Wdt => "[WDT]",
            FileType.HeightMap => "[HGT]",
            FileType.AreaMap => "[AREA]",
            FileType.Minimap => "[MAP]",
            _ => "[?]"
        };

        var label = entry.DisplayName ?? $"{entry.FileDataId}";

        if (ImGui.Selectable($"{icon} {label}"))
        {
            OpenFile(entry);
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.Text($"FileDataID: {entry.FileDataId}");
            ImGui.Text($"Type: {entry.Type}");
            ImGui.Text($"Product: {entry.Product}");
            ImGui.Text($"Opened: {entry.LastOpened:g}");
            ImGui.EndTooltip();
        }
    }

    private void OpenFile(RecentFileEntry entry)
    {
        // TODO: Implement file opening based on type
        // This would typically signal to the appropriate viewer panel
    }

    public void Dispose()
    {
    }
}
