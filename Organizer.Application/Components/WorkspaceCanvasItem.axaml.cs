using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Organizer.Application.ViewModels;

namespace Organizer.Application.Controls;

public partial class WorkspaceCanvasItem : UserControl
{
    private const double DragThreshold = 1;
    private const double MinSize = 48;

    private enum ResizeHandleKind
    {
        None,
        TopLeft,
        TopCenter,
        TopRight,
        MiddleLeft,
        MiddleRight,
        BottomLeft,
        BottomCenter,
        BottomRight
    }

    private bool _isPointerDown;
    private bool _isDragging;
    private bool _isResizing;
    private ResizeHandleKind _activeHandle;
    private Point _startPoint;
    private Point _startOrigin;
    private Size _startSize;
    private Canvas? _canvas;

    public WorkspaceCanvasItem()
    {
        InitializeComponent();
    }

    private WorkspaceCanvasItemViewModel? VM => DataContext as WorkspaceCanvasItemViewModel;

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (VM is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        var source = e.Source as Visual;
        if (IsActionButton(source))
            return;

        _canvas = this.FindAncestorOfType<Canvas>();
        if (_canvas is null)
            return;

        _isPointerDown = true;
        _activeHandle = GetResizeHandle(source);
        _isResizing = _activeHandle != ResizeHandleKind.None;
        _isDragging = false;
        _startPoint = GetCanvasPoint(e);
        _startOrigin = new Point(VM.X, VM.Y);
        _startSize = new Size(VM.Width, VM.Height);
        e.Pointer.Capture(InteractionLayer);
        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isPointerDown || VM is null || _canvas is null)
            return;

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            EndInteraction();
            e.Handled = true;
            return;
        }

        var current = GetCanvasPoint(e);
        var deltaX = current.X - _startPoint.X;
        var deltaY = current.Y - _startPoint.Y;

        if (!_isDragging && !_isResizing)
        {
            if (Math.Abs(deltaX) < DragThreshold && Math.Abs(deltaY) < DragThreshold)
                return;

            _isDragging = true;
        }

        if (_isResizing)
        {
            ResizeItem(deltaX, deltaY);
            e.Handled = true;
            return;
        }

        VM.X = _startOrigin.X + deltaX;
        VM.Y = _startOrigin.Y + deltaY;
        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isPointerDown)
            return;

        e.Pointer.Capture(null);
        EndInteraction();
        e.Handled = true;
    }

    private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        EndInteraction();
    }

    private void ResizeItem(double deltaX, double deltaY)
    {
        if (VM is null)
            return;

        var aspectRatio = _startSize.Width / Math.Max(1, _startSize.Height);
        var scale = 1d;

        switch (_activeHandle)
        {
            case ResizeHandleKind.TopLeft:
                scale = Math.Max(
                    (_startSize.Width - deltaX) / _startSize.Width,
                    (_startSize.Height - deltaY) / _startSize.Height);
                break;
            case ResizeHandleKind.TopCenter:
                scale = (_startSize.Height - deltaY) / _startSize.Height;
                break;
            case ResizeHandleKind.TopRight:
                scale = Math.Max(
                    (_startSize.Width + deltaX) / _startSize.Width,
                    (_startSize.Height - deltaY) / _startSize.Height);
                break;
            case ResizeHandleKind.MiddleLeft:
                scale = (_startSize.Width - deltaX) / _startSize.Width;
                break;
            case ResizeHandleKind.MiddleRight:
                scale = (_startSize.Width + deltaX) / _startSize.Width;
                break;
            case ResizeHandleKind.BottomLeft:
                scale = Math.Max(
                    (_startSize.Width - deltaX) / _startSize.Width,
                    (_startSize.Height + deltaY) / _startSize.Height);
                break;
            case ResizeHandleKind.BottomCenter:
                scale = (_startSize.Height + deltaY) / _startSize.Height;
                break;
            case ResizeHandleKind.BottomRight:
                scale = Math.Max(
                    (_startSize.Width + deltaX) / _startSize.Width,
                    (_startSize.Height + deltaY) / _startSize.Height);
                break;
        }

        scale = Math.Max(scale, MinSize / Math.Min(_startSize.Width, _startSize.Height));

        var newWidth = _startSize.Width * scale;
        var newHeight = newWidth / aspectRatio;
        var newX = _startOrigin.X;
        var newY = _startOrigin.Y;

        switch (_activeHandle)
        {
            case ResizeHandleKind.TopLeft:
                newX = _startOrigin.X + (_startSize.Width - newWidth);
                newY = _startOrigin.Y + (_startSize.Height - newHeight);
                break;
            case ResizeHandleKind.TopCenter:
                newX = _startOrigin.X + (_startSize.Width - newWidth) / 2;
                newY = _startOrigin.Y + (_startSize.Height - newHeight);
                break;
            case ResizeHandleKind.TopRight:
                newY = _startOrigin.Y + (_startSize.Height - newHeight);
                break;
            case ResizeHandleKind.MiddleLeft:
                newX = _startOrigin.X + (_startSize.Width - newWidth);
                newY = _startOrigin.Y + (_startSize.Height - newHeight) / 2;
                break;
            case ResizeHandleKind.MiddleRight:
                newY = _startOrigin.Y + (_startSize.Height - newHeight) / 2;
                break;
            case ResizeHandleKind.BottomLeft:
                newX = _startOrigin.X + (_startSize.Width - newWidth);
                break;
            case ResizeHandleKind.BottomCenter:
                newX = _startOrigin.X + (_startSize.Width - newWidth) / 2;
                break;
            case ResizeHandleKind.BottomRight:
                break;
        }

        VM.X = newX;
        VM.Y = newY;
        VM.Width = newWidth;
        VM.Height = newHeight;
    }

    private ResizeHandleKind GetResizeHandle(Visual? visual)
    {
        while (visual is not null)
        {
            if (visual == TopLeftHandle) return ResizeHandleKind.TopLeft;
            if (visual == TopCenterHandle) return ResizeHandleKind.TopCenter;
            if (visual == TopRightHandle) return ResizeHandleKind.TopRight;
            if (visual == MiddleLeftHandle) return ResizeHandleKind.MiddleLeft;
            if (visual == MiddleRightHandle) return ResizeHandleKind.MiddleRight;
            if (visual == BottomLeftHandle) return ResizeHandleKind.BottomLeft;
            if (visual == BottomCenterHandle) return ResizeHandleKind.BottomCenter;
            if (visual == BottomRightHandle) return ResizeHandleKind.BottomRight;

            if (visual is Button)
                return ResizeHandleKind.None;

            visual = visual.GetVisualParent();
        }

        return ResizeHandleKind.None;
    }

    private static bool IsActionButton(Visual? visual)
    {
        while (visual is not null)
        {
            if (visual is Button)
                return true;

            visual = visual.GetVisualParent();
        }

        return false;
    }

    private Point GetCanvasPoint(PointerEventArgs e)
    {
        if (_canvas is null || VM is null)
            return default;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
            return ToWorldPoint(e.GetPosition(_canvas));

        var transform = _canvas.TransformToVisual(topLevel);
        if (transform is null)
            return ToWorldPoint(e.GetPosition(_canvas));

        var matrix = transform.Value;
        if (!matrix.TryInvert(out var inverse))
            return ToWorldPoint(e.GetPosition(_canvas));

        return ToWorldPoint(inverse.Transform(e.GetPosition(topLevel)));
    }

    private void EndInteraction()
    {
        _isPointerDown = false;
        _isDragging = false;
        _isResizing = false;
        _activeHandle = ResizeHandleKind.None;
        _canvas = null;
    }

    private Point ToWorldPoint(Point canvasPoint)
    {
        if (VM is null)
            return canvasPoint;

        var offsetX = VM.X - VM.DisplayX;
        var offsetY = VM.Y - VM.DisplayY;
        return new Point(canvasPoint.X + offsetX, canvasPoint.Y + offsetY);
    }
}
