namespace WoWHeightGenGUI.Models;

/// <summary>
/// Represents the state of a single map layer including visibility,
/// opacity, blend mode, and layer-specific options.
/// </summary>
public class LayerState
{
    /// <summary>
    /// The type of this layer (determines rendering order and available options)
    /// </summary>
    public LayerType Type { get; init; }

    /// <summary>
    /// Whether this layer is visible
    /// </summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// Layer opacity from 0 (transparent) to 1 (opaque)
    /// </summary>
    public float Opacity { get; set; } = 1.0f;

    /// <summary>
    /// How this layer blends with layers below it
    /// </summary>
    public BlendMode BlendMode { get; set; } = BlendMode.Normal;

    // Height layer specific options

    /// <summary>
    /// Colormap preset for height visualization (only used when Type == Height)
    /// </summary>
    public ColormapType HeightColormap { get; set; } = ColormapType.Grayscale;

    // Area layer specific options

    /// <summary>
    /// Set of area IDs to highlight (only used when Type == Area)
    /// </summary>
    public HashSet<uint> HighlightedAreaIds { get; } = new();

    /// <summary>
    /// When true, all areas are shown. When false, only highlighted areas are visible.
    /// (only used when Type == Area)
    /// </summary>
    public bool ShowAllAreas { get; set; } = true;

    /// <summary>
    /// Creates a new LayerState with default values
    /// </summary>
    public LayerState()
    {
    }

    /// <summary>
    /// Creates a new LayerState for the specified layer type
    /// </summary>
    public LayerState(LayerType type)
    {
        Type = type;
        // Only Minimap visible by default
        IsVisible = type == LayerType.Minimap;
    }

    /// <summary>
    /// Gets the display name for this layer
    /// </summary>
    public string DisplayName => Type switch
    {
        LayerType.Minimap => "Minimap",
        LayerType.Height => "Height Map",
        LayerType.Area => "Area Map",
        _ => "Unknown"
    };

    /// <summary>
    /// Resets this layer to default state
    /// </summary>
    public void Reset()
    {
        // Only Minimap visible by default
        IsVisible = Type == LayerType.Minimap;
        Opacity = 1.0f;
        BlendMode = BlendMode.Normal;
        HeightColormap = ColormapType.Grayscale;
        HighlightedAreaIds.Clear();
        ShowAllAreas = true;
    }
}
