using ImGuiNET;
using WoWHeightGenGUI.App;
using WoWHeightGenGUI.UI.Dialogs;

namespace WoWHeightGenGUI.UI.Panels;

public class MainMenuBar
{
    private readonly Application _app;
    private readonly PanelManager _panelManager;

    private InstallSwitchDialog? _installSwitchDialog;

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

            // Status on the right (FPS + connection)
            var io = ImGui.GetIO();
            var fps = $"FPS: {io.Framerate:F1}";
            var connectionStatus = _app.Context != null
                ? $"Connected: {_app.Settings.Settings.WowProduct} ({_app.Context.VersionName})"
                : "Not connected";
            var status = $"{fps}  |  {connectionStatus}";

            var statusWidth = ImGui.CalcTextSize(status).X;
            ImGui.SetCursorPosX(ImGui.GetWindowWidth() - statusWidth - 10);
            ImGui.TextDisabled(status);

            ImGui.EndMainMenuBar();
        }

        // Render install switch dialog if open
        if (_installSwitchDialog != null)
        {
            _installSwitchDialog.Render();

            // Check if user selected an installation
            if (_installSwitchDialog.HasSelection)
            {
                try
                {
                    _app.ConnectToWow(_installSwitchDialog.SelectedPath!, _installSwitchDialog.SelectedProduct!);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to connect: {ex.Message}");
                }
                _installSwitchDialog = null;
            }
            else if (!_installSwitchDialog.IsOpen)
            {
                _installSwitchDialog = null;
            }
        }
    }

    private void RenderFileMenu()
    {
        if (ImGui.BeginMenu("File"))
        {
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
            if (ImGui.MenuItem("Switch Installation..."))
            {
                _installSwitchDialog = new InstallSwitchDialog();
                _installSwitchDialog.Open();
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
}
