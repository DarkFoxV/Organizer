using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Organizer.Application.Views;
using Avalonia.VisualTree;
using Organizer.Application.Services;
using Organizer.Application.ViewModels;

namespace Organizer.Organizer.Application.Views;

public partial class WorkspaceView : UserControl
{
    private static readonly FilePickerFileType WorkspaceZipFileType = new("Organizer workspace")
    {
        Patterns = ["*.zip"],
        MimeTypes = ["application/zip", "application/x-zip-compressed"]
    };

    private const double ZoomFactor = 1.12;
    private const double MinZoom = 0.1;
    private const double MaxZoom = 5.0;
    private const double ViewportCullPadding = 800;

    private TopLevel? _topLevel;
    private WorkspaceViewModel? _subscribedViewModel;
    private bool _isSpacePressed;
    private bool _isPanning;
    private bool _hasInitializedCamera;
    private Point _panStart;
    private Point _translateStart;
    private Point _lastPointerPositionInViewport;
    private bool _hasLastPointerPosition;
    private bool _isVisibleItemsUpdateQueued;
    private double _zoom = 1.0;
    private IStorageFile? _workspaceFile;
    private bool _isSavingWorkspace;
    private readonly ScaleTransform _scaleTransform = new(1, 1);
    private readonly TranslateTransform _translateTransform = new();
    private readonly DispatcherTimer _autosaveTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(700)
    };

    public WorkspaceView()
    {
        InitializeComponent();
        _autosaveTimer.Tick += OnAutosaveTimerTick;

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
        _subscribedViewModel.WorkspacePreferencesChanged += OnWorkspacePreferencesChanged;
        _subscribedViewModel.WorkspaceChanged += OnWorkspaceChanged;
        _subscribedViewModel.Items.CollectionChanged += OnWorkspaceItemsChanged;
        QueueVisibleItemsUpdate();
    }

    private void UnsubscribeFromViewModel()
    {
        if (_subscribedViewModel is null)
            return;

        _subscribedViewModel.BoardOriginShifted -= OnBoardOriginShifted;
        _subscribedViewModel.WorkspacePreferencesChanged -= OnWorkspacePreferencesChanged;
        _subscribedViewModel.WorkspaceChanged -= OnWorkspaceChanged;
        _subscribedViewModel.Items.CollectionChanged -= OnWorkspaceItemsChanged;
        _subscribedViewModel = null;
    }

    private void OnBoardOriginShifted(double deltaX, double deltaY)
    {
        _translateTransform.X += deltaX * _zoom;
        _translateTransform.Y += deltaY * _zoom;
        QueueVisibleItemsUpdate();
    }

    private void OnWorkspacePreferencesChanged()
    {
        ApplyZoomKeepingViewportCenter(VM.InitialZoom);
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
            e.Handled = VM.RemoveSelectedItems();
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

        ApplyZoomKeepingViewportCenter(newZoom);
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
        {
            VM.ClearSelection();
        }
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            VM.ToggleItemSelection(item);
        }
        else if (!item.IsSelected)
        {
            VM.SelectItem(item);
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        UpdateLastPointerPosition(e);

        if (!_isPanning)
            return;

        var current = e.GetPosition(Viewport);
        _translateTransform.X = _translateStart.X + (current.X - _panStart.X);
        _translateTransform.Y = _translateStart.Y + (current.Y - _panStart.Y);
        QueueVisibleItemsUpdate();
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

    private async void OnOpenWorkspace(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is null)
            return;

        if (VM.HasImages && topLevel is Window owner)
        {
            var canReplace = await ConfirmationDialog.ShowAsync(
                owner,
                "Abrir workspace",
                "A workspace atual tem imagens. Abrir outro arquivo vai fechar a workspace atual.",
                "Abrir",
                "Cancelar",
                isDanger: true);

            if (!canReplace)
                return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Abrir workspace",
            AllowMultiple = false,
            FileTypeFilter = [WorkspaceZipFileType]
        });

        var file = files.FirstOrDefault();
        if (file is null)
            return;

        await using var stream = await file.OpenReadAsync();
        if (await VM.LoadAsync(stream))
            _workspaceFile = file;
    }

    private async void OnSaveWorkspace(object? sender, RoutedEventArgs e)
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null)
            return;

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Salvar workspace",
            SuggestedFileName = "workspace.zip",
            DefaultExtension = "zip",
            FileTypeChoices = [WorkspaceZipFileType]
        });

        if (file is null)
            return;

        await using var stream = await file.OpenWriteAsync();
        if (await VM.SaveAsync(stream))
            _workspaceFile = file;
    }

    private async void OnCloseWorkspace(object? sender, RoutedEventArgs e)
    {
        if (!VM.HasImages)
            return;

        if (TopLevel.GetTopLevel(this) is not Window owner)
            return;

        var canClose = await ConfirmationDialog.ShowAsync(
            owner,
            "Fechar workspace",
            "As imagens no canvas serao removidas. Salve a workspace antes se quiser manter esse layout.",
            "Fechar",
            "Cancelar",
            isDanger: true);

        if (canClose)
        {
            VM.CloseWorkspace();
            _workspaceFile = null;
            _autosaveTimer.Stop();
        }
    }

    private void OnWorkspaceChanged()
    {
        QueueVisibleItemsUpdate();

        if (_workspaceFile is null || _isSavingWorkspace || !VM.HasUnsavedChanges)
            return;

        _autosaveTimer.Stop();
        _autosaveTimer.Start();
    }

    private async void OnAutosaveTimerTick(object? sender, EventArgs e)
    {
        _autosaveTimer.Stop();

        if (_workspaceFile is null || _isSavingWorkspace || !VM.HasUnsavedChanges)
            return;

        try
        {
            _isSavingWorkspace = true;
            await using var stream = await _workspaceFile.OpenWriteAsync();
            await VM.SaveAsync(stream);
        }
        finally
        {
            _isSavingWorkspace = false;
        }
    }

    private void UpdateZoomLabel()
    {
        ZoomText.Text = $"{_zoom * 100:0}%";
    }

    private void OnViewportPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == BoundsProperty)
        {
            TryInitializeCamera();
            QueueVisibleItemsUpdate();
        }
    }

    private void OnBoardRootPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == BoundsProperty)
        {
            TryInitializeCamera();
            QueueVisibleItemsUpdate();
        }
    }

    private void OnWorkspaceItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        QueueVisibleItemsUpdate();
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

        SetZoom(VM.InitialZoom);
        _translateTransform.X = (Viewport.Bounds.Width - BoardRoot.Bounds.Width * _zoom) / 2;
        _translateTransform.Y = (Viewport.Bounds.Height - BoardRoot.Bounds.Height * _zoom) / 2;
        _hasInitializedCamera = true;
        QueueVisibleItemsUpdate();
    }

    private void UpdateLastPointerPosition(PointerEventArgs e)
    {
        _lastPointerPositionInViewport = e.GetPosition(Viewport);
        _hasLastPointerPosition = true;
    }

    private Point? GetCurrentPasteAnchor()
    {
        if (VM.PasteMode == WorkspacePastePreference.Cascade)
            return null;

        if (VM.PasteMode == WorkspacePastePreference.Center)
            return ViewportPointToWorldPoint(new Point(
                Viewport.Bounds.Width / 2,
                Viewport.Bounds.Height / 2));

        if (!_hasLastPointerPosition)
            return ViewportPointToWorldPoint(new Point(
                Viewport.Bounds.Width / 2,
                Viewport.Bounds.Height / 2));

        return ViewportPointToWorldPoint(_lastPointerPositionInViewport);
    }

    private Point ViewportPointToWorldPoint(Point viewportPoint)
    {
        return new Point(
            (viewportPoint.X - _translateTransform.X) / _zoom + VM.BoardStartX,
            (viewportPoint.Y - _translateTransform.Y) / _zoom + VM.BoardStartY);
    }

    private void ApplyZoomKeepingViewportCenter(double newZoom)
    {
        newZoom = Math.Clamp(newZoom, MinZoom, MaxZoom);

        if (Viewport.Bounds.Width <= 0 || Viewport.Bounds.Height <= 0)
        {
            SetZoom(newZoom);
            return;
        }

        var viewportCenter = new Point(Viewport.Bounds.Width / 2, Viewport.Bounds.Height / 2);
        var boardX = (viewportCenter.X - _translateTransform.X) / _zoom;
        var boardY = (viewportCenter.Y - _translateTransform.Y) / _zoom;

        SetZoom(newZoom);
        _translateTransform.X = viewportCenter.X - boardX * newZoom;
        _translateTransform.Y = viewportCenter.Y - boardY * newZoom;
    }

    private void SetZoom(double zoom)
    {
        _zoom = Math.Clamp(zoom, MinZoom, MaxZoom);
        _scaleTransform.ScaleX = _zoom;
        _scaleTransform.ScaleY = _zoom;
        UpdateZoomLabel();
        QueueVisibleItemsUpdate();
    }

    private void QueueVisibleItemsUpdate()
    {
        if (_isVisibleItemsUpdateQueued)
            return;

        _isVisibleItemsUpdateQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _isVisibleItemsUpdateQueued = false;
            UpdateVisibleItems();
        }, DispatcherPriority.Render);
    }

    private void UpdateVisibleItems()
    {
        if (DataContext is not WorkspaceViewModel vm || Viewport.Bounds.Width <= 0 || Viewport.Bounds.Height <= 0)
            return;

        var viewportLeft = (-_translateTransform.X) / _zoom + vm.BoardStartX - ViewportCullPadding;
        var viewportTop = (-_translateTransform.Y) / _zoom + vm.BoardStartY - ViewportCullPadding;
        var viewportRight = (Viewport.Bounds.Width - _translateTransform.X) / _zoom + vm.BoardStartX + ViewportCullPadding;
        var viewportBottom = (Viewport.Bounds.Height - _translateTransform.Y) / _zoom + vm.BoardStartY + ViewportCullPadding;

        foreach (var item in vm.Items)
        {
            var isVisible = item.X + item.Width >= viewportLeft
                && item.X <= viewportRight
                && item.Y + item.Height >= viewportTop
                && item.Y <= viewportBottom;

            if (item.IsInViewport != isVisible)
                item.IsInViewport = isVisible;
        }
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
