using System.Numerics;

namespace WoWHeightGenGUI.Rendering;

public class ImageViewport
{
    public Vector2 Pan { get; set; } = Vector2.Zero;
    public float Zoom { get; set; } = 1.0f;
    public Vector2 ViewportSize { get; set; }

    public float MinZoom { get; set; } = 0.1f;
    public float MaxZoom { get; set; } = 10.0f;
    public float ZoomSpeed { get; set; } = 0.1f;

    public Matrix4x4 GetTransformMatrix()
    {
        return Matrix4x4.CreateScale(Zoom) * Matrix4x4.CreateTranslation(Pan.X, Pan.Y, 0);
    }

    public void HandleMouseDrag(Vector2 delta)
    {
        Pan += delta;
    }

    public void HandleMouseWheel(float delta, Vector2 mousePos)
    {
        // mousePos is relative to viewport top-left
        var oldZoom = Zoom;
        Zoom *= 1.0f + delta * ZoomSpeed;
        Zoom = Math.Clamp(Zoom, MinZoom, MaxZoom);

        // Adjust pan to zoom towards mouse position
        // mousePos is relative to viewport top-left, convert to relative to center
        var zoomRatio = Zoom / oldZoom;
        var mouseRelativeToCenter = mousePos - ViewportSize / 2;

        // The mouse points to a location in image space: (mouseRelativeToCenter - Pan) / oldZoom
        // After zoom, we want the same image point to stay under the mouse
        // newPan should satisfy: (mouseRelativeToCenter - newPan) / newZoom = (mouseRelativeToCenter - oldPan) / oldZoom
        // Solving: newPan = mouseRelativeToCenter - (mouseRelativeToCenter - oldPan) * (newZoom / oldZoom)
        Pan = mouseRelativeToCenter - (mouseRelativeToCenter - Pan) * zoomRatio;
    }

    public void FitToViewport(Vector2 imageSize)
    {
        if (ViewportSize.X <= 0 || ViewportSize.Y <= 0 || imageSize.X <= 0 || imageSize.Y <= 0)
        {
            Zoom = 1.0f;
            Pan = Vector2.Zero;
            return;
        }

        var scaleX = ViewportSize.X / imageSize.X;
        var scaleY = ViewportSize.Y / imageSize.Y;
        Zoom = Math.Min(scaleX, scaleY) * 0.95f; // 5% margin
        Pan = Vector2.Zero;
    }

    public Vector2 ScreenToImage(Vector2 screenPos)
    {
        var center = ViewportSize / 2 + Pan;
        return (screenPos - center) / Zoom;
    }

    public Vector2 ImageToScreen(Vector2 imagePos)
    {
        var center = ViewportSize / 2 + Pan;
        return imagePos * Zoom + center;
    }
}
