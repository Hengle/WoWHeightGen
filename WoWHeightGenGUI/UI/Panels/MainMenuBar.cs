using ImGuiNET;
using WoWHeightGenGUI.App;
using WoWHeightGenLib.Configuration;
using WoWHeightGenLib.Models;

namespace WoWHeightGenGUI.UI.Panels;

public class MainMenuBar
{
    private readonly Application _app;
    private readonly PanelManager _panelManager;

    private bool _showInstallationDialog;
    private List<DetectedWowInstallation>? _detectedInstallations;
    private int _selectedInstallation;

    public MainMenuBar(Application app, PanelManager panelManager)
    {
        _app = app;
        _panelManager = panelManager;
    }

    public void Render()
    {
        if (ImGui.BeginMainMenuBar())
        {
            RenderFileMenu();
            RenderViewMenu();
            RenderWowMenu();
            RenderHelpMenu();

            // Status on the right
            var status = _app.Context != null
                ? $"Connected: {_app.Settings.Settings.WowProduct} ({_app.Context.VersionName})"
                : "Not connected";

            var statusWidth = ImGui.CalcTextSize(status).X;
            ImGui.SetCursorPosX(ImGui.GetWindowWidth() - statusWidth - 10);
            ImGui.TextDisabled(status);

            ImGui.EndMainMenuBar();
        }

        if (_showInstallationDialog)
        {
            RenderInstallationDialog();
        }
    }

    private void RenderFileMenu()
    {
        if (ImGui.BeginMenu("File"))
        {
            if (ImGui.MenuItem("Open BLP by FileDataID...", "Ctrl+O", false, _app.Context != null))
            {
                var explorer = _panelManager.GetPanel<FileExplorerPanel>();
                if (explorer != null)
                {
                    explorer.IsVisible = true;
                    explorer.FocusFileIdInput();
                }
            }

            ImGui.Separator();

            if (ImGui.BeginMenu("Recent Files", _app.RecentFiles.GetRecentFiles().Count > 0))
            {
                foreach (var entry in _app.RecentFiles.GetRecentFiles().Take(10))
                {
                    var label = entry.DisplayName ?? $"{entry.Type}: {entry.FileDataId}";
                    if (ImGui.MenuItem(label))
                    {
                        OpenRecentFile(entry);
                    }
                }

                ImGui.Separator();
                if (ImGui.MenuItem("Clear Recent Files"))
                {
                    _app.RecentFiles.Clear();
                }
                ImGui.EndMenu();
            }

            ImGui.Separator();

            if (ImGui.MenuItem("Export...", "Ctrl+E", false, false)) // TODO: Enable when viewer has content
            {
            }

            ImGui.Separator();

            if (ImGui.MenuItem("Exit", "Alt+F4"))
            {
                _app.RequestExit();
            }

            ImGui.EndMenu();
        }
    }

    private void RenderViewMenu()
    {
        if (ImGui.BeginMenu("View"))
        {
            foreach (var panel in _panelManager.Panels)
            {
                var isVisible = panel.IsVisible;
                if (ImGui.MenuItem(panel.Name, null, ref isVisible))
                {
                    panel.IsVisible = isVisible;
                }
            }

            ImGui.EndMenu();
        }
    }

    private void RenderWowMenu()
    {
        if (ImGui.BeginMenu("WoW"))
        {
            if (ImGui.MenuItem("Select Installation..."))
            {
                _showInstallationDialog = true;
                _detectedInstallations = null;
            }

            if (ImGui.MenuItem("Refresh CASC", null, false, _app.Context != null))
            {
                var path = _app.Settings.Settings.WowInstallPath;
                var product = _app.Settings.Settings.WowProduct;
                if (!string.IsNullOrEmpty(path) && !string.IsNullOrEmpty(product))
                {
                    _app.ConnectToWow(path, product);
                }
            }

            if (ImGui.MenuItem("Disconnect", null, false, _app.Context != null))
            {
                _app.DisconnectFromWow();
            }

            ImGui.EndMenu();
        }
    }

    private void RenderHelpMenu()
    {
        if (ImGui.BeginMenu("Help"))
        {
            if (ImGui.MenuItem("About"))
            {
                // TODO: About dialog
            }
            ImGui.EndMenu();
        }
    }

    private void RenderInstallationDialog()
    {
        ImGui.SetNextWindowSize(new System.Numerics.Vector2(500, 300), ImGuiCond.FirstUseEver);

        if (ImGui.Begin("Select WoW Installation", ref _showInstallationDialog, ImGuiWindowFlags.Modal))
        {
            if (_detectedInstallations == null)
            {
                _detectedInstallations = WowInstallationDetector.DetectInstallations();
                _selectedInstallation = 0;
            }

            if (_detectedInstallations.Count == 0)
            {
                ImGui.TextColored(new System.Numerics.Vector4(1, 0.5f, 0.5f, 1), "No WoW installations detected.");
                ImGui.TextWrapped("Make sure World of Warcraft is installed and has been run at least once.");
            }
            else
            {
                ImGui.Text("Detected Installations:");
                ImGui.Separator();

                for (int i = 0; i < _detectedInstallations.Count; i++)
                {
                    var install = _detectedInstallations[i];
                    var label = $"{install.ProductInfo.UninstallName} ({install.ProductInfo.Product})";

                    if (ImGui.RadioButton(label, ref _selectedInstallation, i))
                    {
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.BeginTooltip();
                        ImGui.Text($"Path: {install.InstallPath}");
                        ImGui.Text($"Product: {install.ProductInfo.Product}");
                        ImGui.EndTooltip();
                    }
                }

                ImGui.Separator();

                if (ImGui.Button("Connect", new System.Numerics.Vector2(100, 0)))
                {
                    var selected = _detectedInstallations[_selectedInstallation];
                    try
                    {
                        _app.ConnectToWow(selected.InstallPath, selected.ProductInfo.Product);
                        _showInstallationDialog = false;
                    }
                    catch
                    {
                        ImGui.OpenPopup("Connection Error");
                    }
                }

                ImGui.SameLine();

                if (ImGui.Button("Cancel", new System.Numerics.Vector2(100, 0)))
                {
                    _showInstallationDialog = false;
                }
            }
        }
        ImGui.End();
    }

    private void OpenRecentFile(Configuration.RecentFileEntry entry)
    {
        // TODO: Open the file based on type
        switch (entry.Type)
        {
            case Configuration.FileType.Blp:
                var viewer = _panelManager.GetPanel<TextureViewerPanel>();
                if (viewer != null)
                {
                    viewer.IsVisible = true;
                    viewer.LoadTexture(entry.FileDataId);
                }
                break;

            case Configuration.FileType.HeightMap:
            case Configuration.FileType.Wdt:
                var heightViewer = _panelManager.GetPanel<HeightMapViewerPanel>();
                if (heightViewer != null)
                {
                    heightViewer.IsVisible = true;
                    heightViewer.LoadWdt(entry.FileDataId);
                }
                break;

            case Configuration.FileType.AreaMap:
                var areaViewer = _panelManager.GetPanel<AreaMapViewerPanel>();
                if (areaViewer != null)
                {
                    areaViewer.IsVisible = true;
                    areaViewer.LoadWdt(entry.FileDataId);
                }
                break;
        }
    }
}
