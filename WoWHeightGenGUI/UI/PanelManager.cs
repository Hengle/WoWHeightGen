using ImGuiNET;
using WoWHeightGenGUI.App;
using WoWHeightGenGUI.ImGuiInternal;
using WoWHeightGenGUI.UI.Panels;

namespace WoWHeightGenGUI.UI;

public class PanelManager : IDisposable
{
    private readonly Application _app;
    private readonly List<IPanel> _panels = new();
    private MainMenuBar _menuBar = null!;
    private bool _firstFrame = true;
    private bool _dockLayoutInitialized = false;

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
        var viewport = ImGuiNET.ImGui.GetMainViewport();

        RenderDockSpace(viewport);
        _menuBar.Render();

        foreach (var panel in _panels)
        {
            if (panel.IsVisible)
            {
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
        ImGuiNET.ImGui.SetNextWindowPos(viewport.WorkPos);
        ImGuiNET.ImGui.SetNextWindowSize(viewport.WorkSize);
        ImGuiNET.ImGui.SetNextWindowViewport(viewport.ID);

        var windowFlags = ImGuiWindowFlags.NoDocking
            | ImGuiWindowFlags.NoTitleBar
            | ImGuiWindowFlags.NoCollapse
            | ImGuiWindowFlags.NoResize
            | ImGuiWindowFlags.NoMove
            | ImGuiWindowFlags.NoBringToFrontOnFocus
            | ImGuiWindowFlags.NoNavFocus
            | ImGuiWindowFlags.NoBackground;

        ImGuiNET.ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0.0f);
        ImGuiNET.ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0.0f);
        ImGuiNET.ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, System.Numerics.Vector2.Zero);

        ImGuiNET.ImGui.Begin("DockSpaceWindow", windowFlags);
        ImGuiNET.ImGui.PopStyleVar(3);

        var dockspaceId = ImGuiNET.ImGui.GetID("MainDockSpace");

        // Set up dock layout on first frame, AFTER getting the dockspace ID but BEFORE calling DockSpace
        if (_firstFrame && !_dockLayoutInitialized)
        {
            SetupInitialDockLayout(dockspaceId, viewport);
            _dockLayoutInitialized = true;
        }

        ImGuiNET.ImGui.DockSpace(dockspaceId, System.Numerics.Vector2.Zero, ImGuiDockNodeFlags.PassthruCentralNode);

        ImGuiNET.ImGui.End();
    }

    /// <summary>
    /// Set up the initial dock layout using DockBuilder API.
    /// Only runs on first launch (when no imgui.ini exists).
    /// </summary>
    private void SetupInitialDockLayout(uint dockspaceId, ImGuiViewportPtr viewport)
    {
        // Check if we have an existing layout saved
        var imguiIniPath = _app.Settings.GetImGuiIniPath();
        if (File.Exists(imguiIniPath))
        {
            // Let ImGui restore the saved layout
            return;
        }

        // Clear any existing layout for this dockspace
        DockBuilder.RemoveNode(dockspaceId);

        // Create the root dockspace node
        DockBuilder.AddNode(dockspaceId, DockNodeFlags.DockSpace);
        DockBuilder.SetNodeSize(dockspaceId, viewport.WorkSize);

        // Split layout: Left (30% for Map Browser) | Remaining (70%)
        DockBuilder.SplitNode(
            dockspaceId,
            Direction.Left,
            0.30f,
            out uint leftId,
            out uint remainingId);

        // Split remaining: Center | Right (22.5% of original = ~32% of remaining 70%)
        DockBuilder.SplitNode(
            remainingId,
            Direction.Right,
            0.32f,  // 22.5% of original / 70% remaining ≈ 0.32
            out uint rightId,
            out uint centerId);

        // Dock windows to their respective nodes
        DockBuilder.DockWindow("Map Browser", leftId);
        DockBuilder.DockWindow("Map Viewport", centerId);
        DockBuilder.DockWindow("Layers & Properties", rightId);

        // Finalize the layout
        DockBuilder.Finish(dockspaceId);
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
