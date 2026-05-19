namespace Organizer.Application.Controls;
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;

public class ZoomBorder : Border
{
    private Point  _origin;
    private Point  _start;
    private bool   _isDragging;
    private double _zoom = 1.0;

    private const double ZoomFactor = 1.1;
    private const double MinZoom    = 0.1;
    private const double MaxZoom    = 10.0;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        PointerWheelChanged += OnWheel;
        PointerPressed      += OnPointerPressed;
        PointerMoved        += OnPointerMoved;
        PointerReleased     += OnPointerReleased;

        ClipToBounds = true;
        ResetTransform();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        PointerWheelChanged -= OnWheel;
        PointerPressed      -= OnPointerPressed;
        PointerMoved        -= OnPointerMoved;
        PointerReleased     -= OnPointerReleased;

        base.OnDetachedFromVisualTree(e);
    }

    // ── Zoom com scroll ───────────────────────────────────────────────────────
    private void OnWheel(object? sender, PointerWheelEventArgs e)
    {
        if (Child is null) return;

        var delta    = e.Delta.Y > 0 ? ZoomFactor : 1.0 / ZoomFactor;
        var newZoom  = Math.Clamp(_zoom * delta, MinZoom, MaxZoom);

        if (Child.RenderTransform is not TransformGroup group)
            return;

        var scale = (ScaleTransform)group.Children[0];

        scale.ScaleX = newZoom;
        scale.ScaleY = newZoom;
        _zoom        = newZoom;

        ClampTranslation();

        e.Handled = true;
    }

    // ── Pan com drag ──────────────────────────────────────────────────────────
    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Child?.RenderTransform is not TransformGroup group) return;

        var translate = (TranslateTransform)group.Children[1];
        _start      = e.GetPosition(this);
        _origin     = new Point(translate.X, translate.Y);
        _isDragging = true;
        Cursor      = new Cursor(StandardCursorType.SizeAll);
        e.Pointer.Capture(this);
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragging || Child?.RenderTransform is not TransformGroup group) return;

        var translate = (TranslateTransform)group.Children[1];
        var pos       = e.GetPosition(this);
        translate.X   = _origin.X + (pos.X - _start.X);
        translate.Y   = _origin.Y + (pos.Y - _start.Y);
        ClampTranslation();
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isDragging = false;
        Cursor      = Cursor.Default;
        e.Pointer.Capture(null);
    }

    // ── Reset ─────────────────────────────────────────────────────────────────
    public void Reset()
    {
        _zoom = 1.0;
        ResetTransform();
    }

    private void ResetTransform()
    {
        if (Child is null) return;

        Child.RenderTransformOrigin = RelativePoint.Center;
        Child.RenderTransform = new TransformGroup
        {
            Children =
            [
                new ScaleTransform(1, 1),
                new TranslateTransform(0, 0)
            ]
        };

        ClampTranslation();
    }

    private void ClampTranslation()
    {
        if (Child?.RenderTransform is not TransformGroup group)
            return;

        var translate = (TranslateTransform)group.Children[1];
        var imageSize = GetDisplayedImageSize();

        if (Bounds.Width <= 0 || Bounds.Height <= 0 || imageSize.Width <= 0 || imageSize.Height <= 0)
        {
            translate.X = 0;
            translate.Y = 0;
            return;
        }

        var maxOffsetX = Math.Max(0, (imageSize.Width * _zoom - Bounds.Width) / 2);
        var maxOffsetY = Math.Max(0, (imageSize.Height * _zoom - Bounds.Height) / 2);

        translate.X = Math.Clamp(translate.X, -maxOffsetX, maxOffsetX);
        translate.Y = Math.Clamp(translate.Y, -maxOffsetY, maxOffsetY);
    }

    private Size GetDisplayedImageSize()
    {
        if (Child is Image { Source: Bitmap bitmap })
        {
            var sourceWidth  = bitmap.PixelSize.Width;
            var sourceHeight = bitmap.PixelSize.Height;

            if (sourceWidth <= 0 || sourceHeight <= 0 || Bounds.Width <= 0 || Bounds.Height <= 0)
                return default;

            var ratio = Math.Min(Bounds.Width / sourceWidth, Bounds.Height / sourceHeight);
            return new Size(sourceWidth * ratio, sourceHeight * ratio);
        }

        return Child?.Bounds.Size ?? default;
    }
}
