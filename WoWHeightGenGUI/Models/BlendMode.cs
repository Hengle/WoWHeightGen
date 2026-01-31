namespace WoWHeightGenGUI.Models;

/// <summary>
/// Defines blend modes for layer compositing.
/// These modes determine how a layer's pixels combine with the layers below.
/// </summary>
public enum BlendMode
{
    /// <summary>
    /// Standard alpha blending
    /// </summary>
    Normal = 0,

    /// <summary>
    /// Multiplies base and blend colors, resulting in a darker image
    /// </summary>
    Multiply = 1,

    /// <summary>
    /// Inverse multiply, resulting in a lighter image
    /// </summary>
    Screen = 2,

    /// <summary>
    /// Combines Multiply and Screen based on base color
    /// </summary>
    Overlay = 3,

    /// <summary>
    /// Softer version of Overlay
    /// </summary>
    SoftLight = 4,

    /// <summary>
    /// Stronger version of Overlay
    /// </summary>
    HardLight = 5
}
