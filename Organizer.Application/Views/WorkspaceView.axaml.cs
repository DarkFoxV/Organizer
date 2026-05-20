using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Organizer.Application.ViewModels;

namespace Organizer.Organizer.Application.Views;

public partial class WorkspaceView : UserControl
{
    private const double ZoomFactor = 1.12;
    private const double MinZoom = 0.1;
    private const double MaxZoom = 5.0;

    private TopLevel? _topLevel;
    private WorkspaceViewModel? _subscribedViewModel;
    private bool _isSpacePressed;
    private bool _isPanning;
    private bool _hasInitializedCamera;
    private Point _panStart;
    private Point _translateStart;
    private Point _lastPointerPositionInViewport;
    private bool _hasLastPointerPosition;
    private double _zoom = 1.0;
    private readonly ScaleTransform _scaleTransform = new(1, 1);
    private readonly TranslateTransform _translateTransform = new();

    public WorkspaceView()
    {
        InitializeComponent();

        BoardRoot.RenderTransform = new TransformGroup
        {
            Children =
            [
                _scaleTransform,
                _translateTransform
            ]
        };
        BoardRoot.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Relative);

        Viewport.AddHandler(PointerWheelChangedEvent, OnPointerWheelChanged, RoutingStrategies.Tunnel);
        Viewport.AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
        Viewport.AddHandler(PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel);
        Viewport.AddHandler(PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel);
        Viewport.PropertyChanged += OnViewportPropertyChanged;
        BoardRoot.PropertyChanged += OnBoardRootPropertyChanged;

        AttachedToVisualTree += (_, _) =>
        {
            _topLevel = TopLevel.GetTopLevel(this);
            _topLevel?.AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
            _topLevel?.AddHandler(KeyUpEvent, OnKeyUp, RoutingStrategies.Tunnel);
            SubscribeToViewModel();
            Viewport.Focus();
            UpdateZoomLabel();
            TryInitializeCamera();
        };

        DetachedFromVisualTree += (_, _) =>
        {
            if (_topLevel is null)
                return;

            _topLevel.RemoveHandler(KeyDownEvent, OnKeyDown);
            _topLevel.RemoveHandler(KeyUpEvent, OnKeyUp);
            UnsubscribeFromViewModel();
            Viewport.PropertyChanged -= OnViewportPropertyChanged;
            BoardRoot.PropertyChanged -= OnBoardRootPropertyChanged;
            _topLevel = null;
        };
    }

    private WorkspaceViewModel VM => (WorkspaceViewModel)DataContext!;

    private void SubscribeToViewModel()
    {
        if (_subscribedViewModel == DataContext)
            return;

        UnsubscribeFromViewModel();

        if (DataContext is not WorkspaceViewModel vm)
            return;

        _subscribedViewModel = vm;
        _subscribedViewModel.BoardOriginShifted += OnBoardOriginShifted;
    }

    private void UnsubscribeFromViewModel()
    {
        if (_subscribedViewModel is null)
            return;

        _subscribedViewModel.BoardOriginShifted -= OnBoardOriginShifted;
        _subscribedViewModel = null;
    }

    private void OnBoardOriginShifted(double deltaX, double deltaY)
    {
        _translateTransform.X += deltaX * _zoom;
        _translateTransform.Y += deltaY * _zoom;
    }

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space)
        {
            _isSpacePressed = true;
            Viewport.Cursor = new Cursor(StandardCursorType.SizeAll);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Delete)
        {
            e.Handled = VM.RemoveSelectedItem();
            return;
        }

        if (e.Key != Key.V || !e.KeyModifiers.HasFlag(KeyModifiers.Control))
            return;

        var clipboard = _topLevel?.Clipboard ?? TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
            return;

        if (await VM.TryPasteImagesAsync(clipboard, GetCurrentPasteAnchor()))
            e.Handled = true;
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Space)
            return;

        _isSpacePressed = false;
        if (!_isPanning)
            Viewport.Cursor = Cursor.Default;

        e.Handled = true;
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        UpdateLastPointerPosition(e);

        var delta = e.Delta.Y > 0 ? ZoomFactor : 1.0 / ZoomFactor;
        var newZoom = Math.Clamp(_zoom * delta, MinZoom, MaxZoom);

        if (Math.Abs(newZoom - _zoom) < 0.0001)
            return;

        var viewportCenter = new Point(Viewport.Bounds.Width / 2, Viewport.Bounds.Height / 2);
        var boardX = (viewportCenter.X - _translateTransform.X) / _zoom;
        var boardY = (viewportCenter.Y - _translateTransform.Y) / _zoom;

        _zoom = newZoom;
        _scaleTransform.ScaleX = newZoom;
        _scaleTransform.ScaleY = newZoom;
        _translateTransform.X = viewportCenter.X - boardX * newZoom;
        _translateTransform.Y = viewportCenter.Y - boardY * newZoom;

        UpdateZoomLabel();
        e.Handled = true;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        UpdateLastPointerPosition(e);

        if (_isSpacePressed && e.GetCurrentPoint(Viewport).Properties.IsLeftButtonPressed)
        {
            StartPanning(e);
            return;
        }

        var item = GetItemFromSource(e.Source as Visual);
        if (item is null)
            VM.ClearSelection();
        else
            VM.SelectItem(item);
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        UpdateLastPointerPosition(e);

        if (!_isPanning)
            return;

        var current = e.GetPosition(Viewport);
        _translateTransform.X = _translateStart.X + (current.X - _panStart.X);
        _translateTransform.Y = _translateStart.Y + (current.Y - _panStart.Y);
        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isPanning)
            return;

        _isPanning = false;
        e.Pointer.Capture(null);
        Viewport.Cursor = _isSpacePressed
            ? new Cursor(StandardCursorType.SizeAll)
            : Cursor.Default;
        e.Handled = true;
    }

    private void UpdateZoomLabel()
    {
        ZoomText.Text = $"{_zoom * 100:0}%";
    }

    private void OnViewportPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == BoundsProperty)
            TryInitializeCamera();
    }

    private void OnBoardRootPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == BoundsProperty)
            TryInitializeCamera();
    }

    private void StartPanning(PointerPressedEventArgs e)
    {
        _isPanning = true;
        _hasInitializedCamera = true;
        _panStart = e.GetPosition(Viewport);
        _translateStart = new Point(_translateTransform.X, _translateTransform.Y);
        Viewport.Cursor = new Cursor(StandardCursorType.SizeAll);
        e.Pointer.Capture(Viewport);
        e.Handled = true;
    }

    private void TryInitializeCamera()
    {
        if (_hasInitializedCamera || Viewport.Bounds.Width <= 0 || Viewport.Bounds.Height <= 0)
            return;

        if (BoardRoot.Bounds.Width <= 0 || BoardRoot.Bounds.Height <= 0)
            return;

        _translateTransform.X = (Viewport.Bounds.Width - BoardRoot.Bounds.Width * _zoom) / 2;
        _translateTransform.Y = (Viewport.Bounds.Height - BoardRoot.Bounds.Height * _zoom) / 2;
        _hasInitializedCamera = true;
    }

    private void UpdateLastPointerPosition(PointerEventArgs e)
    {
        _lastPointerPositionInViewport = e.GetPosition(Viewport);
        _hasLastPointerPosition = true;
    }

    private Point GetCurrentPasteAnchor()
    {
        if (!_hasLastPointerPosition)
            return new Point(
                BoardRoot.Bounds.Width / 2 + VM.BoardStartX,
                BoardRoot.Bounds.Height / 2 + VM.BoardStartY);

        return new Point(
            (_lastPointerPositionInViewport.X - _translateTransform.X) / _zoom + VM.BoardStartX,
            (_lastPointerPositionInViewport.Y - _translateTransform.Y) / _zoom + VM.BoardStartY);
    }

    private static WorkspaceCanvasItemViewModel? GetItemFromSource(Visual? visual)
    {
        while (visual is not null)
        {
            if (visual is StyledElement { DataContext: WorkspaceCanvasItemViewModel item })
                return item;

            visual = visual.GetVisualParent();
        }

        return null;
    }
}
