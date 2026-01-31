namespace WoWHeightGenGUI.Models;

/// <summary>
/// Defines available colormap presets for height map visualization.
/// </summary>
public enum ColormapType
{
    /// <summary>
    /// Simple black to white gradient
    /// </summary>
    Grayscale = 0,

    /// <summary>
    /// Terrain colors: deep blue (water) -> green (low) -> brown (mid) -> white (high)
    /// </summary>
    Terrain = 1,

    /// <summary>
    /// Perceptually uniform colormap from purple to yellow
    /// </summary>
    Viridis = 2,

    /// <summary>
    /// Temperature-like gradient from blue (low) to red (high)
    /// </summary>
    Heatmap = 3
}
