using System.Numerics;
using ImGuiNET;
using WoWHeightGenGUI.App;
using WoWHeightGenGUI.Models;

namespace WoWHeightGenGUI.UI.Panels;

/// <summary>
/// Panel for managing layer visibility, opacity, and properties.
/// Works in conjunction with MapViewportPanel.
/// </summary>
public class LayersPropertiesPanel : IPanel, IDisposable
{
    private readonly Application _app;
    private readonly PanelManager _panelManager;

    private bool _isVisible = true;

    // Blend mode names for dropdown
    private static readonly string[] BlendModeNames =
    {
        "Normal", "Multiply", "Screen", "Overlay", "Soft Light", "Hard Light"
    };

    // Colormap names for dropdown
    private static readonly string[] ColormapNames =
    {
        "Grayscale", "Terrain", "Viridis", "Heatmap"
    };

    public string Name => "Layers & Properties";
    public bool IsVisible { get => _isVisible; set => _isVisible = value; }

    public LayersPropertiesPanel(Application app, PanelManager panelManager)
    {
        _app = app;
        _panelManager = panelManager;
    }

    public void Update(float deltaTime)
    {
    }

    public void Render()
    {
        if (ImGui.Begin(Name, ref _isVisible))
        {
            var viewportPanel = _panelManager.GetPanel<MapViewportPanel>();
            if (viewportPanel == null)
            {
                ImGui.TextDisabled("Viewport not available");
                ImGui.End();
                return;
            }

            var viewState = viewportPanel.ViewState;

            // Properties section (top)
            RenderPropertiesSection(viewState);

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Layers section (bottom)
            RenderLayersSection(viewState);
        }
        ImGui.End();
    }

    private void RenderPropertiesSection(MapViewState viewState)
    {
        var headerFlags = ImGuiTreeNodeFlags.DefaultOpen;

        if (ImGui.CollapsingHeader("Properties", headerFlags))
        {
            var selectedLayer = viewState.SelectedLayer;

            ImGui.Indent();

            // Layer name
            ImGui.TextColored(GetLayerColor(selectedLayer.Type), selectedLayer.DisplayName);
            ImGui.Spacing();

            // Opacity slider
            float opacity = selectedLayer.Opacity;
            ImGui.Text("Opacity");
            ImGui.SameLine(90);
            ImGui.SetNextItemWidth(-1);
            if (ImGui.SliderFloat("##Opacity", ref opacity, 0.0f, 1.0f, "%.2f"))
            {
                selectedLayer.Opacity = opacity;
            }

            ImGui.Spacing();

            // Blend mode dropdown
            int blendMode = (int)selectedLayer.BlendMode;
            ImGui.Text("Blend Mode");
            ImGui.SameLine(90);
            ImGui.SetNextItemWidth(-1);
            if (ImGui.Combo("##BlendMode", ref blendMode, BlendModeNames, BlendModeNames.Length))
            {
                selectedLayer.BlendMode = (BlendMode)blendMode;
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Layer-specific options
            switch (selectedLayer.Type)
            {
                case LayerType.Height:
                    RenderHeightLayerOptions(selectedLayer);
                    break;
                case LayerType.Area:
                    RenderAreaLayerOptions(selectedLayer);
                    break;
                case LayerType.Minimap:
                    RenderMinimapLayerOptions(selectedLayer);
                    break;
            }

            ImGui.Unindent();
        }
    }

    private void RenderHeightLayerOptions(LayerState layer)
    {
        ImGui.Text("Height Map Options");
        ImGui.Spacing();

        // Colormap selection
        int colormap = (int)layer.HeightColormap;
        ImGui.Text("Colormap");
        ImGui.SameLine(90);
        ImGui.SetNextItemWidth(-1);
        if (ImGui.Combo("##Colormap", ref colormap, ColormapNames, ColormapNames.Length))
        {
            layer.HeightColormap = (ColormapType)colormap;
        }

        // Preview colormap
        ImGui.Spacing();
        RenderColormapPreview(layer.HeightColormap);
    }

    private void RenderColormapPreview(ColormapType colormap)
    {
        var drawList = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = 20.0f;

        // Draw gradient preview
        int segments = 64;
        float segmentWidth = width / segments;

        for (int i = 0; i < segments; i++)
        {
            float t = i / (float)(segments - 1);
            var color = GetColormapColor(colormap, t);

            var segmentPos = new Vector2(pos.X + i * segmentWidth, pos.Y);
            var segmentEnd = new Vector2(pos.X + (i + 1) * segmentWidth, pos.Y + height);

            drawList.AddRectFilled(segmentPos, segmentEnd, ImGui.ColorConvertFloat4ToU32(color));
        }

        // Draw border
        drawList.AddRect(pos, new Vector2(pos.X + width, pos.Y + height),
            ImGui.ColorConvertFloat4ToU32(new Vector4(0.5f, 0.5f, 0.5f, 1.0f)));

        // Reserve space
        ImGui.Dummy(new Vector2(width, height));
    }

    private Vector4 GetColormapColor(ColormapType colormap, float t)
    {
        return colormap switch
        {
            ColormapType.Grayscale => new Vector4(t, t, t, 1.0f),
            ColormapType.Terrain => GetTerrainColor(t),
            ColormapType.Viridis => GetViridisColor(t),
            ColormapType.Heatmap => GetHeatmapColor(t),
            _ => new Vector4(t, t, t, 1.0f)
        };
    }

    private Vector4 GetTerrainColor(float t)
    {
        if (t < 0.1f)
            return new Vector4(0, 0.1f + t * 2, 0.4f + t * 3, 1);
        if (t < 0.2f)
            return Vector4.Lerp(new Vector4(0, 0.3f, 0.7f, 1), new Vector4(0.2f, 0.6f, 0.8f, 1), (t - 0.1f) * 10);
        if (t < 0.4f)
            return Vector4.Lerp(new Vector4(0.2f, 0.6f, 0.8f, 1), new Vector4(0.4f, 0.6f, 0.2f, 1), (t - 0.2f) * 5);
        if (t < 0.7f)
            return Vector4.Lerp(new Vector4(0.4f, 0.6f, 0.2f, 1), new Vector4(0.6f, 0.4f, 0.2f, 1), (t - 0.4f) * 3.33f);
        return Vector4.Lerp(new Vector4(0.6f, 0.4f, 0.2f, 1), new Vector4(1, 1, 1, 1), (t - 0.7f) * 3.33f);
    }

    private Vector4 GetViridisColor(float t)
    {
        // Simplified Viridis approximation
        if (t < 0.25f)
            return Vector4.Lerp(new Vector4(0.267f, 0.004f, 0.329f, 1), new Vector4(0.231f, 0.322f, 0.545f, 1), t * 4);
        if (t < 0.5f)
            return Vector4.Lerp(new Vector4(0.231f, 0.322f, 0.545f, 1), new Vector4(0.129f, 0.565f, 0.549f, 1), (t - 0.25f) * 4);
        if (t < 0.75f)
            return Vector4.Lerp(new Vector4(0.129f, 0.565f, 0.549f, 1), new Vector4(0.365f, 0.788f, 0.388f, 1), (t - 0.5f) * 4);
        return Vector4.Lerp(new Vector4(0.365f, 0.788f, 0.388f, 1), new Vector4(0.992f, 0.906f, 0.145f, 1), (t - 0.75f) * 4);
    }

    private Vector4 GetHeatmapColor(float t)
    {
        if (t < 0.25f)
            return Vector4.Lerp(new Vector4(0, 0, 1, 1), new Vector4(0, 1, 1, 1), t * 4);
        if (t < 0.5f)
            return Vector4.Lerp(new Vector4(0, 1, 1, 1), new Vector4(0, 1, 0, 1), (t - 0.25f) * 4);
        if (t < 0.75f)
            return Vector4.Lerp(new Vector4(0, 1, 0, 1), new Vector4(1, 1, 0, 1), (t - 0.5f) * 4);
        return Vector4.Lerp(new Vector4(1, 1, 0, 1), new Vector4(1, 0, 0, 1), (t - 0.75f) * 4);
    }

    private void RenderAreaLayerOptions(LayerState layer)
    {
        ImGui.Text("Area Map Options");
        ImGui.Spacing();

        // Show all areas toggle
        bool showAll = layer.ShowAllAreas;
        if (ImGui.Checkbox("Show All Areas", ref showAll))
        {
            layer.ShowAllAreas = showAll;
        }

        if (!layer.ShowAllAreas)
        {
            ImGui.Spacing();
            ImGui.TextDisabled("Only highlighted areas will be visible.");
            ImGui.TextDisabled($"Highlighted: {layer.HighlightedAreaIds.Count} areas");

            // Area ID input for highlighting
            ImGui.Spacing();
            ImGui.Text("Highlight Area ID:");

            ImGui.SetNextItemWidth(100);
            int areaIdToAdd = 0;
            if (ImGui.InputInt("##areaId", ref areaIdToAdd, 0, 0, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                if (areaIdToAdd > 0)
                {
                    layer.HighlightedAreaIds.Add((uint)areaIdToAdd);
                }
            }

            // List highlighted areas
            if (layer.HighlightedAreaIds.Count > 0)
            {
                ImGui.Spacing();
                ImGui.Text("Highlighted Areas:");

                uint? toRemove = null;
                foreach (var areaId in layer.HighlightedAreaIds.Take(10))
                {
                    ImGui.BulletText($"Area {areaId}");
                    ImGui.SameLine();
                    if (ImGui.SmallButton($"X##{areaId}"))
                    {
                        toRemove = areaId;
                    }
                }

                if (toRemove.HasValue)
                {
                    layer.HighlightedAreaIds.Remove(toRemove.Value);
                }

                if (layer.HighlightedAreaIds.Count > 10)
                {
                    ImGui.TextDisabled($"...and {layer.HighlightedAreaIds.Count - 10} more");
                }

                ImGui.Spacing();
                if (ImGui.Button("Clear All"))
                {
                    layer.HighlightedAreaIds.Clear();
                }
            }
        }
    }

    private void RenderMinimapLayerOptions(LayerState layer)
    {
        ImGui.Text("Minimap Options");
        ImGui.Spacing();
        ImGui.TextDisabled("No additional options available.");
    }

    private void RenderLayersSection(MapViewState viewState)
    {
        var headerFlags = ImGuiTreeNodeFlags.DefaultOpen;

        if (ImGui.CollapsingHeader("Layers", headerFlags))
        {
            ImGui.Indent();

            // Render layers in reverse order (top layer first visually)
            for (int i = MapViewState.LayerCount - 1; i >= 0; i--)
            {
                var layer = viewState.Layers[i];
                var isSelected = viewState.SelectedLayerIndex == i;

                ImGui.PushID(i);

                // Visibility toggle (eye icon)
                bool visible = layer.IsVisible;
                if (ImGui.Checkbox("##vis", ref visible))
                {
                    layer.IsVisible = visible;
                }

                ImGui.SameLine();

                // Layer color indicator
                var color = GetLayerColor(layer.Type);
                var drawList = ImGui.GetWindowDrawList();
                var cursorPos = ImGui.GetCursorScreenPos();
                drawList.AddRectFilled(cursorPos, cursorPos + new Vector2(4, 18),
                    ImGui.ColorConvertFloat4ToU32(color));
                ImGui.Dummy(new Vector2(8, 0));
                ImGui.SameLine();

                // Layer name (selectable)
                var flags = ImGuiSelectableFlags.None;
                if (ImGui.Selectable(layer.DisplayName, isSelected, flags, new Vector2(0, 18)))
                {
                    viewState.SelectedLayerIndex = i;
                }

                // Opacity indicator
                ImGui.SameLine(ImGui.GetWindowWidth() - 60);
                ImGui.TextDisabled($"{layer.Opacity:P0}");

                ImGui.PopID();
            }

            ImGui.Unindent();

            // Quick actions
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            if (ImGui.Button("Show All"))
            {
                foreach (var layer in viewState.Layers)
                    layer.IsVisible = true;
            }

            ImGui.SameLine();

            if (ImGui.Button("Hide All"))
            {
                foreach (var layer in viewState.Layers)
                    layer.IsVisible = false;
            }

            ImGui.SameLine();

            if (ImGui.Button("Reset"))
            {
                foreach (var layer in viewState.Layers)
                    layer.Reset();
            }
        }
    }

    private Vector4 GetLayerColor(LayerType type)
    {
        return type switch
        {
            LayerType.Minimap => new Vector4(0.4f, 0.8f, 0.4f, 1.0f),  // Green
            LayerType.Height => new Vector4(0.6f, 0.6f, 1.0f, 1.0f),   // Blue
            LayerType.Area => new Vector4(1.0f, 0.6f, 0.4f, 1.0f),     // Orange
            _ => new Vector4(0.7f, 0.7f, 0.7f, 1.0f)
        };
    }

    public void Dispose()
    {
    }
}
