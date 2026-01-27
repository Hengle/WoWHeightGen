using System.Numerics;
using ImGuiNET;
using WoWHeightGenGUI.App;
using WoWHeightGenGUI.Services;

namespace WoWHeightGenGUI.UI.Panels;

public class MapBrowserPanel : IPanel, IConnectionAwarePanel
{
    private readonly Application _app;
    private readonly PanelManager _panelManager;

    public string Name => "Map Browser";
    private bool _isVisible = true;
    public bool IsVisible { get => _isVisible; set => _isVisible = value; }

    private List<MapEntry>? _maps;
    private List<MapEntry>? _filteredMaps;
    private string _searchFilter = "";
    private int _typeFilter = -1; // -1 = All
    private bool _isLoading;
    private string? _errorMessage;
    private int _selectedIndex = -1;

    private static readonly string[] TypeFilters = { "All", "World", "Dungeon", "Raid", "Battleground", "Arena", "Scenario" };
    private static readonly int[] TypeFilterValues = { -1, 0, 1, 2, 3, 4, 5 };

    public MapBrowserPanel(Application app, PanelManager panelManager)
    {
        _app = app;
        _panelManager = panelManager;
    }

    public void OnConnectionChanged()
    {
        // Reset and reload maps when connection changes
        _maps = null;
        _filteredMaps = null;
        _errorMessage = null;
        _selectedIndex = -1;
    }

    public void Update(float deltaTime)
    {
    }

    public void Render()
    {
        if (ImGui.Begin(Name, ref _isVisible))
        {
            if (_app.Context == null || _app.Db2Service == null)
            {
                ImGui.TextColored(new Vector4(1, 0.5f, 0.5f, 1), "Not connected to WoW installation.");
            }
            else
            {
                // Load maps if not loaded yet
                if (_maps == null && !_isLoading)
                {
                    LoadMaps();
                }

                RenderToolbar();
                ImGui.Separator();

                if (_isLoading)
                {
                    ImGui.Text("Loading maps...");
                }
                else if (!string.IsNullOrEmpty(_errorMessage))
                {
                    ImGui.TextColored(new Vector4(1, 0.4f, 0.4f, 1), _errorMessage);
                }
                else if (_filteredMaps != null)
                {
                    RenderMapList();
                }
            }
        }
        ImGui.End();
    }

    private void LoadMaps()
    {
        _isLoading = true;
        _errorMessage = null;

        try
        {
            _maps = _app.Db2Service!.GetMaps();
            ApplyFilters();
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to load maps: {ex.Message}";
            _maps = new List<MapEntry>();
            _filteredMaps = new List<MapEntry>();
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void RenderToolbar()
    {
        // Search box
        ImGui.Text("Search:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(200);
        if (ImGui.InputText("##search", ref _searchFilter, 100))
        {
            ApplyFilters();
        }

        ImGui.SameLine();

        // Type filter
        ImGui.Text("Type:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(120);

        int currentTypeIndex = Array.IndexOf(TypeFilterValues, _typeFilter);
        if (currentTypeIndex < 0) currentTypeIndex = 0;

        if (ImGui.Combo("##typeFilter", ref currentTypeIndex, TypeFilters, TypeFilters.Length))
        {
            _typeFilter = TypeFilterValues[currentTypeIndex];
            ApplyFilters();
        }

        ImGui.SameLine();

        // Refresh button
        if (ImGui.Button("Refresh"))
        {
            _maps = null;
            _filteredMaps = null;
            _selectedIndex = -1;
        }

        // Stats
        if (_filteredMaps != null && _maps != null)
        {
            ImGui.SameLine();
            ImGui.TextDisabled($"| {_filteredMaps.Count} / {_maps.Count} maps");
        }
    }

    private void ApplyFilters()
    {
        if (_maps == null)
        {
            _filteredMaps = null;
            return;
        }

        _filteredMaps = _maps.Where(m =>
        {
            // Type filter
            if (_typeFilter >= 0 && m.InstanceType != _typeFilter)
                return false;

            // Search filter
            if (!string.IsNullOrWhiteSpace(_searchFilter))
            {
                var search = _searchFilter.Trim();
                if (!m.Name.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                    !m.Id.ToString().Contains(search))
                {
                    return false;
                }
            }

            return true;
        }).ToList();

        _selectedIndex = -1;
    }

    private void RenderMapList()
    {
        if (_filteredMaps == null || _filteredMaps.Count == 0)
        {
            ImGui.TextDisabled("No maps found matching filters.");
            return;
        }

        var availableHeight = ImGui.GetContentRegionAvail().Y - 35; // Leave room for button

        // Table with columns
        if (ImGui.BeginTable("MapsTable", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.Resizable, new Vector2(0, availableHeight)))
        {
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("ID", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed, 100);
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableHeadersRow();

            for (int i = 0; i < _filteredMaps.Count; i++)
            {
                var map = _filteredMaps[i];

                ImGui.TableNextRow();
                ImGui.TableNextColumn();

                var isSelected = _selectedIndex == i;
                if (ImGui.Selectable(map.Name, isSelected, ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowDoubleClick))
                {
                    _selectedIndex = i;

                    if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                    {
                        LoadSelectedMap();
                    }
                }

                ImGui.TableNextColumn();
                ImGui.Text(map.Id.ToString());

                ImGui.TableNextColumn();
                ImGui.TextColored(GetTypeColor(map.InstanceType), map.InstanceTypeName);
            }

            ImGui.EndTable();
        }

        // Load button
        ImGui.Spacing();
        ImGui.BeginDisabled(_selectedIndex < 0);
        if (ImGui.Button("Load Map", new Vector2(100, 25)))
        {
            LoadSelectedMap();
        }
        ImGui.EndDisabled();

        if (_selectedIndex >= 0)
        {
            var selected = _filteredMaps[_selectedIndex];
            ImGui.SameLine();
            ImGui.TextDisabled($"WDT FileDataID: {selected.WdtFileDataId}");
        }
    }

    private Vector4 GetTypeColor(int instanceType)
    {
        return instanceType switch
        {
            0 => new Vector4(0.4f, 0.8f, 0.4f, 1.0f), // World - green
            1 => new Vector4(0.4f, 0.6f, 1.0f, 1.0f), // Dungeon - blue
            2 => new Vector4(1.0f, 0.6f, 0.2f, 1.0f), // Raid - orange
            3 => new Vector4(1.0f, 0.4f, 0.4f, 1.0f), // Battleground - red
            4 => new Vector4(1.0f, 1.0f, 0.4f, 1.0f), // Arena - yellow
            5 => new Vector4(0.8f, 0.4f, 1.0f, 1.0f), // Scenario - purple
            _ => new Vector4(0.7f, 0.7f, 0.7f, 1.0f)  // Unknown - gray
        };
    }

    private void LoadSelectedMap()
    {
        if (_selectedIndex < 0 || _filteredMaps == null)
            return;

        var map = _filteredMaps[_selectedIndex];

        // Load in height map viewer
        var heightViewer = FindPanel<HeightMapViewerPanel>();
        if (heightViewer != null)
        {
            heightViewer.IsVisible = true;
            heightViewer.LoadWdt(map.WdtFileDataId);
        }

        // Add to recent files
        _app.RecentFiles.AddEntry(map.WdtFileDataId, Configuration.FileType.HeightMap,
            _app.Settings.Settings.WowProduct ?? "unknown", $"{map.Name} (Map {map.Id})");
    }

    private T? FindPanel<T>() where T : class, IPanel
    {
        return _panelManager.GetPanel<T>();
    }

    public void Dispose()
    {
    }
}
