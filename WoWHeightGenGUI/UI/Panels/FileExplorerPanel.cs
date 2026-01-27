using ImGuiNET;
using WoWHeightGenGUI.App;

namespace WoWHeightGenGUI.UI.Panels;

public class FileExplorerPanel : IPanel
{
    private readonly Application _app;

    public string Name => "File Explorer";
    private bool _isVisible = false;
    public bool IsVisible { get => _isVisible; set => _isVisible = value; }

    private string _fileIdInput = "";
    private bool _focusInput;

    public FileExplorerPanel(Application app)
    {
        _app = app;
    }

    public void FocusFileIdInput()
    {
        _focusInput = true;
    }

    public void Update(float deltaTime)
    {
    }

    public void Render()
    {
        if (ImGui.Begin(Name, ref _isVisible))
        {
            if (_app.Context == null)
            {
                ImGui.TextColored(new System.Numerics.Vector4(1, 0.5f, 0.5f, 1),
                    "Not connected to WoW installation.");
                ImGui.TextWrapped("Go to WoW > Select Installation... to connect.");
            }
            else
            {
                RenderConnectedUI();
            }
        }
        ImGui.End();
    }

    private void RenderConnectedUI()
    {
        ImGui.Text("Open by FileDataID:");

        if (_focusInput)
        {
            ImGui.SetKeyboardFocusHere();
            _focusInput = false;
        }

        ImGui.SetNextItemWidth(150);
        if (ImGui.InputText("##fileId", ref _fileIdInput, 20, ImGuiInputTextFlags.CharsDecimal))
        {
        }

        ImGui.SameLine();
        if (ImGui.Button("Open BLP") && TryParseFileId(out var blpId))
        {
            OpenBlp(blpId);
        }

        ImGui.SameLine();
        if (ImGui.Button("Open WDT") && TryParseFileId(out var wdtId))
        {
            OpenWdt(wdtId);
        }

        ImGui.Separator();
        ImGui.Text("Quick Actions:");

        if (ImGui.Button("Height Map from WDT") && TryParseFileId(out var heightId))
        {
            OpenHeightMap(heightId);
        }

        ImGui.SameLine();
        if (ImGui.Button("Area Map from WDT") && TryParseFileId(out var areaId))
        {
            OpenAreaMap(areaId);
        }

        ImGui.Separator();
        ImGui.TextDisabled("Common WDT FileDataIDs:");
        ImGui.BulletText("782779 - Kalimdor");
        ImGui.BulletText("782780 - Eastern Kingdoms");
        ImGui.BulletText("782781 - Outland");
        ImGui.BulletText("782782 - Northrend");
    }

    private bool TryParseFileId(out uint fileId)
    {
        return uint.TryParse(_fileIdInput, out fileId);
    }

    private void OpenBlp(uint fileId)
    {
        if (_app.Context == null) return;

        if (!_app.Context.FileExists(fileId))
        {
            // Could show error
            return;
        }

        // TODO: Signal to texture viewer panel
        _app.RecentFiles.AddEntry(fileId, Configuration.FileType.Blp,
            _app.Settings.Settings.WowProduct ?? "unknown");
    }

    private void OpenWdt(uint fileId)
    {
        if (_app.Context == null) return;

        if (!_app.Context.FileExists(fileId))
        {
            return;
        }

        _app.RecentFiles.AddEntry(fileId, Configuration.FileType.Wdt,
            _app.Settings.Settings.WowProduct ?? "unknown");
    }

    private void OpenHeightMap(uint wdtFileId)
    {
        if (_app.Context == null) return;

        _app.RecentFiles.AddEntry(wdtFileId, Configuration.FileType.HeightMap,
            _app.Settings.Settings.WowProduct ?? "unknown");
    }

    private void OpenAreaMap(uint wdtFileId)
    {
        if (_app.Context == null) return;

        _app.RecentFiles.AddEntry(wdtFileId, Configuration.FileType.AreaMap,
            _app.Settings.Settings.WowProduct ?? "unknown");
    }

    public void Dispose()
    {
    }
}
