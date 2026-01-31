using ImGuiNET;
using WoWHeightGenGUI.App;
using WoWHeightGenGUI.UI.Panels;

namespace WoWHeightGenGUI.UI;

public class PanelManager : IDisposable
{
    private readonly Application _app;
    private readonly List<IPanel> _panels = new();
    private MainMenuBar _menuBar = null!;
    private bool _firstFrame = true;

    public PanelManager(Application app)
    {
        _app = app;
    }

    public void Initialize()
    {
        _menuBar = new MainMenuBar(_app, this);

        // Create main panels for the UI layout
        RegisterPanel(new MapBrowserPanel(_app, this));
        RegisterPanel(new MapViewportPanel(_app, this));
        RegisterPanel(new LayersPropertiesPanel(_app, this));
    }

    public void RegisterPanel(IPanel panel)
    {
        _panels.Add(panel);
    }

    public T? GetPanel<T>() where T : class, IPanel
    {
        return _panels.OfType<T>().FirstOrDefault();
    }

    public IReadOnlyList<IPanel> Panels => _panels;

    public void OnConnectionChanged()
    {
        // Notify panels that need to refresh when WoW connection changes
        foreach (var panel in _panels)
        {
            if (panel is IConnectionAwarePanel connectionAware)
            {
                connectionAware.OnConnectionChanged();
            }
        }
    }

    public void Render()
    {
        var viewport = ImGui.GetMainViewport();
        RenderDockSpace(viewport);
        _menuBar.Render();

        foreach (var panel in _panels)
        {
            if (panel.IsVisible)
            {
                // Set initial positions/docking for key panels on first use
                if (_firstFrame)
                {
                    SetupPanelLayout(panel, viewport);
                }
                panel.Render();
            }
        }

        if (_firstFrame)
        {
            _firstFrame = false;
        }
    }

    private void RenderDockSpace(ImGuiViewportPtr viewport)
    {
        ImGui.SetNextWindowPos(viewport.WorkPos);
        ImGui.SetNextWindowSize(viewport.WorkSize);
        ImGui.SetNextWindowViewport(viewport.ID);

        var windowFlags = ImGuiWindowFlags.NoDocking
            | ImGuiWindowFlags.NoTitleBar
            | ImGuiWindowFlags.NoCollapse
            | ImGuiWindowFlags.NoResize
            | ImGuiWindowFlags.NoMove
            | ImGuiWindowFlags.NoBringToFrontOnFocus
            | ImGuiWindowFlags.NoNavFocus
            | ImGuiWindowFlags.NoBackground;

        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, System.Numerics.Vector2.Zero);

        ImGui.Begin("DockSpaceWindow", windowFlags);
        ImGui.PopStyleVar(3);

        var dockspaceId = ImGui.GetID("MainDockSpace");
        ImGui.DockSpace(dockspaceId, System.Numerics.Vector2.Zero, ImGuiDockNodeFlags.PassthruCentralNode);

        ImGui.End();
    }

    private void SetupPanelLayout(IPanel panel, ImGuiViewportPtr viewport)
    {
        var workSize = viewport.WorkSize;
        var workPos = viewport.WorkPos;

        // 3-panel layout: Left (15%) | Center (70%) | Right (15%)
        var leftWidth = workSize.X * 0.15f;
        var rightWidth = workSize.X * 0.15f;
        var centerWidth = workSize.X * 0.70f;
        var centerX = workPos.X + leftWidth;
        var rightX = centerX + centerWidth;

        // Set initial positions based on panel type
        if (panel.Name == "Map Browser")
        {
            ImGui.SetNextWindowPos(workPos, ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize(new System.Numerics.Vector2(leftWidth, workSize.Y - 20), ImGuiCond.FirstUseEver);
            // Tell window to dock to the main dockspace
            var dockspaceId = ImGui.GetID("MainDockSpace");
            ImGui.SetNextWindowDockID(dockspaceId, ImGuiCond.FirstUseEver);
        }
        else if (panel.Name == "Map Viewport")
        {
            ImGui.SetNextWindowPos(new System.Numerics.Vector2(centerX, workPos.Y), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize(new System.Numerics.Vector2(centerWidth, workSize.Y - 20), ImGuiCond.FirstUseEver);
            var dockspaceId = ImGui.GetID("MainDockSpace");
            ImGui.SetNextWindowDockID(dockspaceId, ImGuiCond.FirstUseEver);
        }
        else if (panel.Name == "Layers & Properties")
        {
            ImGui.SetNextWindowPos(new System.Numerics.Vector2(rightX, workPos.Y), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize(new System.Numerics.Vector2(rightWidth, workSize.Y - 20), ImGuiCond.FirstUseEver);
            var dockspaceId = ImGui.GetID("MainDockSpace");
            ImGui.SetNextWindowDockID(dockspaceId, ImGuiCond.FirstUseEver);
        }
    }

    public void Dispose()
    {
        foreach (var panel in _panels)
        {
            panel.Dispose();
        }
        _panels.Clear();
    }
}
