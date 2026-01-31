namespace WoWHeightGenGUI.Models;

/// <summary>
/// Defines the available map layer types and their rendering order.
/// Lower values are rendered first (bottom), higher values on top.
/// </summary>
public enum LayerType
{
    /// <summary>
    /// Minimap texture layer - renders at the bottom
    /// </summary>
    Minimap = 0,

    /// <summary>
    /// Height map layer - renders in the middle
    /// </summary>
    Height = 1,

    /// <summary>
    /// Area ID map layer - renders on top
    /// </summary>
    Area = 2
}
