using System;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using global::Organizer.Application.Views;
using global::Organizer.Application.Services;
using global::Organizer.Application.ViewModels;

namespace Organizer.Organizer.Application.Views;

public partial class WorkspaceView : UserControl
{
    private const double ZoomFactor = 1.12;
    private const double MinZoom = 0.1;
    private const double MaxZoom = 5.0;
    private const double ViewportCullPadding = 800;

    private TopLevel? _topLevel;
    private WorkspaceViewModel? _subscribedViewModel;
    private bool _isPanning;
    private bool _hasInitializedCamera;
    private Point _panStart;
    private Point _translateStart;
    private Point _lastPointerPositionInViewport;
    private bool _hasLastPointerPosition;
    private bool _isVisibleItemsUpdateQueued;
    private double _zoom = 1.0;
    private bool _isSavingWorkspace;
    private bool _isClosingWorkspace;
    private readonly ScaleTransform _scaleTransform = new(1, 1);
    private readonly TranslateTransform _translateTransform = new();

    private readonly DispatcherTimer _autosaveTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(700)
    };

    private readonly DispatcherTimer _zoomQualityTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(160)
    };

    public WorkspaceView()
    {
        InitializeComponent();
        _autosaveTimer.Tick += OnAutosaveTimerTick;
        _zoomQualityTimer.Tick += OnZoomQualityTimerTick;

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
            SubscribeToViewModel();
            Viewport.Focus();
            UpdateZoomLabel();
            TryInitializeCamera();
        };

        DetachedFromVisualTree += (_, _) =>
        {
            _autosaveTimer.Stop();
            _zoomQualityTimer.Stop();

            if (_topLevel is not null)
                _topLevel.RemoveHandler(KeyDownEvent, OnKeyDown);

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
        if (e.Key == Key.Delete)
        {
            e.Handled = VM.RemoveSelectedItems();
            return;
        }

        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control))
            return;

        if (e.Key == Key.Z)
        {
            e.Handled = e.KeyModifiers.HasFlag(KeyModifiers.Shift)
                ? VM.Redo()
                : VM.Undo();
            return;
        }

        if (e.Key == Key.Y)
        {
            e.Handled = VM.Redo();
            return;
        }

        if (e.Key == Key.S)
        {
            await SaveWorkspaceFromShortcutAsync();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Q)
        {
            await CloseWorkspaceFromShortcutAsync();
            e.Handled = true;
            return;
        }

        var clipboard = _topLevel?.Clipboard ?? TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
            return;

        if (e.Key == Key.C)
        {
            if (await VM.CopySelectedImageAsync(clipboard))
                e.Handled = true;

            return;
        }

        if (e.Key == Key.V && await VM.TryPasteImagesAsync(clipboard, GetCurrentPasteAnchor()))
            e.Handled = true;
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        UpdateLastPointerPosition(e);

        var delta = e.Delta.Y > 0 ? ZoomFactor : 1.0 / ZoomFactor;
        var newZoom = Math.Clamp(_zoom * delta, MinZoom, MaxZoom);

        if (Math.Abs(newZoom - _zoom) < 0.0001)
            return;

        WorkspaceSurface.SetUseLowResBitmaps(true);
        RenderOptions.SetBitmapInterpolationMode(BoardRoot, BitmapInterpolationMode.LowQuality);
        _zoomQualityTimer.Stop();
        _zoomQualityTimer.Start();

        ApplyZoomKeepingViewportCenter(newZoom);
        e.Handled = true;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        UpdateLastPointerPosition(e);

        if (e.GetCurrentPoint(Viewport).Properties.IsMiddleButtonPressed)
        {
            StartPanning(e);
            return;
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        UpdateLastPointerPosition(e);

        if (_isPanning)
        {
            var current = e.GetPosition(Viewport);
            _translateTransform.X = _translateStart.X + (current.X - _panStart.X);
            _translateTransform.Y = _translateStart.Y + (current.Y - _panStart.Y);
            UpdateWorkspaceSurfaceViewport();
            e.Handled = true;
            return;
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isPanning)
            return;

        _isPanning = false;
        e.Pointer.Capture(null);
        Viewport.Cursor = Cursor.Default;
        WorkspaceSurface.SetCameraPanMode(false);

        WorkspaceSurface.SetUseLowResBitmaps(false);
        RenderOptions.SetBitmapInterpolationMode(BoardRoot, BitmapInterpolationMode.HighQuality);
        QueueVisibleItemsUpdate();
        e.Handled = true;
    }

    private async void OnOpenWorkspace(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is null)
            return;

        if (!await ConfirmReplaceWorkspaceAsync(topLevel))
            return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Abrir workspace",
            AllowMultiple = false,
            FileTypeFilter = [WorkspaceFilePicker.WorkspaceFileType]
        });

        var file = files.FirstOrDefault();
        if (file is null)
            return;

        await LoadWorkspaceFileAsync(file);
    }

    private async void OnOpenRecentWorkspace(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is not RecentWorkspaceItemViewModel workspace)
            return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is null)
            return;

        if (!await ConfirmReplaceWorkspaceAsync(topLevel))
            return;

        var file = await topLevel.StorageProvider.TryGetFileFromPathAsync(workspace.LocalPath);
        if (file is null)
        {
            VM.ForgetRecentWorkspace(workspace.LocalPath);
            VM.ErrorMessage = "Workspace recente nao foi encontrada.";
            return;
        }

        await LoadWorkspaceFileAsync(file);
    }

    private async Task<bool> ConfirmReplaceWorkspaceAsync(TopLevel topLevel)
    {
        if (!VM.HasUnsavedChanges || topLevel is not Window owner)
            return true;

        return await ConfirmationDialog.ShowAsync(
            owner,
            "Abrir workspace",
            "A workspace atual tem imagens. Abrir outro arquivo vai fechar a workspace atual.",
            "Abrir",
            "Cancelar",
            isDanger: true);
    }

    private async Task LoadWorkspaceFileAsync(IStorageFile file)
    {
        var loaded = false;

        try
        {
            await using var stream = await file.OpenReadAsync();
            loaded = await VM.LoadAsync(stream);
        }
        catch (Exception ex)
        {
            VM.ErrorMessage = $"Erro ao abrir workspace: {ex.Message}";
        }

        if (loaded)
        {
            VM.SetWorkspaceFile(file);
            await VM.RememberWorkspaceFileAsync(file);
            return;
        }

        file.Dispose();
    }

    private async void OnSaveWorkspace(object? sender, RoutedEventArgs e)
    {
        await SaveWorkspaceFromShortcutAsync();
    }

    private async void OnCloseWorkspace(object? sender, RoutedEventArgs e)
    {
        await CloseWorkspaceFromShortcutAsync();
    }

    private async Task<bool> SaveWorkspaceBeforeCloseAsync(Window owner)
    {
        if (VM.HasWorkspaceFile)
        {
            VM.SetWorkspaceThumbnail(VM.CreateWorkspaceThumbnail());
            return await VM.SaveToCurrentFileAsync();
        }

        var storage = owner.StorageProvider;
        var file = await storage.SaveFilePickerAsync(WorkspaceFilePicker.CreateSaveOptions());

        if (file is null)
            return false;

        VM.SetWorkspaceThumbnail(VM.CreateWorkspaceThumbnail());
        return await VM.SaveToFileAsync(file);
    }

    private async Task SaveWorkspaceFromShortcutAsync()
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null)
            return;

        var file = await storage.SaveFilePickerAsync(WorkspaceFilePicker.CreateSaveOptions());

        if (file is null)
            return;

        VM.SetWorkspaceThumbnail(VM.CreateWorkspaceThumbnail());
        await VM.SaveToFileAsync(file);
    }

    private async Task CloseWorkspaceFromShortcutAsync()
    {
        if (!VM.HasImages || _isClosingWorkspace)
            return;

        if (!VM.HasUnsavedChanges)
        {
            VM.CloseWorkspace();
            _autosaveTimer.Stop();
            return;
        }

        if (TopLevel.GetTopLevel(this) is not Window owner)
            return;

        _isClosingWorkspace = true;
        try
        {
            var shouldSave = await ConfirmationDialog.ShowAsync(
                owner,
                AppPreferencesService.Translate("Loc.Workspace.CloseConfirmTitle"),
                AppPreferencesService.Translate("Loc.Workspace.CloseConfirmMessage"),
                AppPreferencesService.Translate("Loc.Common.Save"),
                AppPreferencesService.Translate("Loc.Workspace.CloseWithoutSaving"),
                isDanger: true);

            if (shouldSave)
            {
                if (await SaveWorkspaceBeforeCloseAsync(owner))
                {
                    VM.CloseWorkspace();
                    _autosaveTimer.Stop();
                }

                return;
            }

            VM.CloseWorkspace();
            _autosaveTimer.Stop();
        }
        finally
        {
            _isClosingWorkspace = false;
        }
    }

    private void OnWorkspaceChanged()
    {
        QueueVisibleItemsUpdate();

        if (!VM.HasWorkspaceFile || _isSavingWorkspace || !VM.HasUnsavedChanges)
            return;

        _autosaveTimer.Stop();
        _autosaveTimer.Start();
    }

    private async void OnAutosaveTimerTick(object? sender, EventArgs e)
    {
        _autosaveTimer.Stop();

        if (!VM.HasWorkspaceFile || _isSavingWorkspace || !VM.HasUnsavedChanges)
            return;

        try
        {
            _isSavingWorkspace = true;
            VM.SetWorkspaceThumbnail(VM.CreateWorkspaceThumbnail());
            await VM.SaveToCurrentFileAsync();
        }
        finally
        {
            _isSavingWorkspace = false;
        }
    }

    private void OnZoomQualityTimerTick(object? sender, EventArgs e)
    {
        _zoomQualityTimer.Stop();

        if (_isPanning)
            return;

        WorkspaceSurface.SetUseLowResBitmaps(false);
        RenderOptions.SetBitmapInterpolationMode(BoardRoot, BitmapInterpolationMode.HighQuality);
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
        WorkspaceSurface.SetCameraPanMode(true);
        e.Pointer.Capture(Viewport);
        e.Handled = true;

        WorkspaceSurface.SetUseLowResBitmaps(true);
        RenderOptions.SetBitmapInterpolationMode(BoardRoot, BitmapInterpolationMode.LowQuality);
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
        UpdateWorkspaceSurfaceViewport();
    }

    private void SetZoom(double zoom)
    {
        _zoom = Math.Clamp(zoom, MinZoom, MaxZoom);
        _scaleTransform.ScaleX = _zoom;
        _scaleTransform.ScaleY = _zoom;
        WorkspaceSurface.SetZoom(_zoom);
        UpdateZoomLabel();
        QueueVisibleItemsUpdate();
    }

    private void QueueVisibleItemsUpdate()
    {
        if (_isVisibleItemsUpdateQueued)
            return;

        // Não enfileira durante o pan — o RenderTransform já move tudo
        // visualmente; o culling pode aguardar o fim do gesto
        if (_isPanning)
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
        UpdateWorkspaceSurfaceViewport();
    }

    private void UpdateWorkspaceSurfaceViewport()
    {
        if (Viewport.Bounds.Width <= 0 || Viewport.Bounds.Height <= 0)
            return;

        var left = (-_translateTransform.X) / _zoom - ViewportCullPadding;
        var top = (-_translateTransform.Y) / _zoom - ViewportCullPadding;
        var right = (Viewport.Bounds.Width - _translateTransform.X) / _zoom + ViewportCullPadding;
        var bottom = (Viewport.Bounds.Height - _translateTransform.Y) / _zoom + ViewportCullPadding;

        WorkspaceSurface.SetViewportBounds(new Rect(left, top, right - left, bottom - top));
    }
}
