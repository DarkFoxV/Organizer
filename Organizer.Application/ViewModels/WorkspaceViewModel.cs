using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Organizer.Application.Services;

namespace Organizer.Application.ViewModels;

public partial class WorkspaceViewModel : ObservableObject, IDisposable
{
    private const double CanvasPadding = 200;
    private const double PasteOffsetStep = 48;
    private const double MinimumBoardWidth = 4000;
    private const double MinimumBoardHeight = 2500;

    private readonly IClipboardService _clipboardService;
    private readonly AppPreferencesService _preferencesService;
    private readonly WorkspaceArchiveService _workspaceArchiveService;
    private WorkspaceCanvasItemViewModel? _selectedItem;
    private readonly Dictionary<WorkspaceCanvasItemViewModel, Point> _moveStartPositions = [];
    private bool _isBatchMovingSelection;
    private double _boardWidth = MinimumBoardWidth;
    private double _boardHeight = MinimumBoardHeight;
    private double _nextFallbackPasteX = CanvasPadding;
    private double _nextFallbackPasteY = CanvasPadding;
    private int _nextZIndex;
    private static bool _isMemoryCompactionQueued;

    [ObservableProperty] private bool _isPasting;

    [ObservableProperty] private string? _errorMessage;

    [ObservableProperty] private double _boardStartX;

    [ObservableProperty] private double _boardStartY;

    [ObservableProperty] private bool _hasUnsavedChanges;

    public event Action<double, double>? BoardOriginShifted;
    public event Action? WorkspacePreferencesChanged;
    public event Action? WorkspaceChanged;

    public ObservableCollection<WorkspaceCanvasItemViewModel> Items { get; } = [];

    public IBrush WorkspaceViewportBackground => BrushFromHex(GetWorkspacePalette().Viewport);

    public IBrush WorkspaceBoardBackground => BrushFromHex(GetWorkspacePalette().Board);

    public IBrush WorkspaceBoardBorderBrush => BrushFromHex(GetWorkspacePalette().Border);

    public double InitialZoom => Math.Clamp(
        _preferencesService.Current.WorkspaceDefaultZoomPercent / 100d,
        0.1,
        2.0);

    public WorkspacePastePreference PasteMode => _preferencesService.Current.WorkspacePasteMode;

    public bool IsEmpty => Items.Count == 0;

    public bool HasImages => Items.Count > 0;

    public string CounterText => Items.Count == 1 ? "1 imagem no canvas" : $"{Items.Count} imagens no canvas";

    public double BoardWidth => _boardWidth;

    public double BoardHeight => _boardHeight;

    public WorkspaceViewModel(
        IClipboardService clipboardService,
        AppPreferencesService preferencesService,
        WorkspaceArchiveService workspaceArchiveService)
    {
        _clipboardService = clipboardService;
        _preferencesService = preferencesService;
        _workspaceArchiveService = workspaceArchiveService;
        _preferencesService.PreferencesChanged += OnPreferencesChanged;
        Items.CollectionChanged += OnItemsChanged;
    }

    public async Task<bool> TryPasteImagesAsync(IClipboard clipboard, Point? pasteAnchor)
    {
        if (IsPasting)
            return false;

        IsPasting = true;
        ErrorMessage = null;

        try
        {
            var images = await _clipboardService.GetImagesAsync(clipboard);
            if (images.Count == 0)
            {
                ErrorMessage = "A area de transferencia nao contem uma imagem suportada.";
                return false;
            }

            for (var i = 0; i < images.Count; i++)
                AddImage(images[i], pasteAnchor, i);

            MarkDirty();
            return true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Erro ao colar imagem: {ex.Message}";
            return false;
        }
        finally
        {
            IsPasting = false;
        }
    }

    public void Dispose()
    {
        Items.CollectionChanged -= OnItemsChanged;
        _preferencesService.PreferencesChanged -= OnPreferencesChanged;
        ClearAll();
    }

    public async Task<bool> SaveAsync(Stream output)
    {
        if (Items.Count == 0)
        {
            ErrorMessage = "Nao ha imagens no workspace para salvar.";
            return false;
        }

        try
        {
            ErrorMessage = null;

            var archiveItems = Items
                .OrderBy(item => item.ZIndex)
                .Select(ToArchiveItem)
                .ToList();

            await _workspaceArchiveService.SaveAsync(output, archiveItems);
            HasUnsavedChanges = false;
            return true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Erro ao salvar workspace: {ex.Message}";
            return false;
        }
    }

    public async Task<bool> LoadAsync(Stream input)
    {
        try
        {
            ErrorMessage = null;
            var archiveItems = await _workspaceArchiveService.LoadAsync(input);

            ClearAll();

            foreach (var item in archiveItems.OrderBy(item => item.ZIndex))
                AddArchiveItem(item);

            ClearSelection();
            NotifyBoardStateChanged();
            HasUnsavedChanges = false;
            return true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Erro ao abrir workspace: {ex.Message}";
            return false;
        }
    }

    public void CloseWorkspace()
    {
        ClearAll();
        HasUnsavedChanges = false;
    }

    [RelayCommand]
    private void ClearAll()
    {
        var hadImages = Items.Count > 0;

        foreach (var item in Items.ToList())
        {
            item.RemoveRequested -= OnRemoveRequested;
            item.PropertyChanged -= OnItemPropertyChanged;
            item.Dispose();
        }

        Items.Clear();
        _selectedItem = null;
        _moveStartPositions.Clear();
        _nextFallbackPasteX = CanvasPadding;
        _nextFallbackPasteY = CanvasPadding;
        _nextZIndex = 0;
        ErrorMessage = null;
        NotifyBoardStateChanged();

        if (hadImages)
            QueueLargeImageMemoryCompaction();
    }

    public void SelectItem(WorkspaceCanvasItemViewModel item)
    {
        foreach (var selectedItem in Items.Where(i => i != item && i.IsSelected))
            selectedItem.IsSelected = false;

        _selectedItem = item;
        _selectedItem.IsSelected = true;
    }

    public void ToggleItemSelection(WorkspaceCanvasItemViewModel item)
    {
        item.IsSelected = !item.IsSelected;
        _selectedItem = item.IsSelected
            ? item
            : Items.LastOrDefault(i => i.IsSelected);
    }

    public void ClearSelection()
    {
        foreach (var item in Items.Where(i => i.IsSelected))
            item.IsSelected = false;

        _selectedItem = null;
    }

    public bool RemoveSelectedItems()
    {
        var selectedItems = Items.Where(i => i.IsSelected).ToList();
        if (selectedItems.Count == 0)
            return false;

        foreach (var item in selectedItems)
            OnRemoveRequested(item);

        MarkDirty();
        return true;
    }

    public void BeginMoveSelectedItems(WorkspaceCanvasItemViewModel anchorItem)
    {
        if (!anchorItem.IsSelected)
            SelectItem(anchorItem);

        _moveStartPositions.Clear();

        foreach (var item in Items.Where(i => i.IsSelected))
            _moveStartPositions[item] = new Point(item.X, item.Y);
    }

    public void MoveSelectedItems(double deltaX, double deltaY)
    {
        if (_moveStartPositions.Count == 0)
            return;

        _isBatchMovingSelection = true;

        try
        {
            foreach (var (item, startPosition) in _moveStartPositions)
            {
                item.X = startPosition.X + deltaX;
                item.Y = startPosition.Y + deltaY;
            }
        }
        finally
        {
            _isBatchMovingSelection = false;
        }

        NotifyBoardStateChanged();
        MarkDirty();
    }

    public void EndMoveSelectedItems()
    {
        _moveStartPositions.Clear();
    }

    private void AddImage(ClipboardImageData image, Point? pasteAnchor, int pasteIndex)
    {
        using var stream = new MemoryStream(image.Data, writable: false);
        AddBitmap(new Bitmap(stream), image.Filename, image.MimeType, image.Data, pasteAnchor, pasteIndex);
    }

    private void AddBitmap(
        Bitmap bitmap,
        string sourceName,
        string mimeType,
        byte[] imageData,
        Point? pasteAnchor,
        int pasteIndex)
    {
        var width = bitmap.PixelSize.Width;
        var height = bitmap.PixelSize.Height;
        var position = GetPastePosition(width, height, pasteAnchor, pasteIndex);

        var item = new WorkspaceCanvasItemViewModel
        {
            Bitmap = bitmap,
            Label = sourceName,
            MimeType = mimeType,
            ImageData = imageData,
            OriginalWidth = width,
            OriginalHeight = height,
            Width = width,
            Height = height,
            X = position.X,
            Y = position.Y,
            ZIndex = _nextZIndex++
        };

        item.RemoveRequested += OnRemoveRequested;
        item.PropertyChanged += OnItemPropertyChanged;
        Items.Add(item);
        SelectItem(item);
        NotifyBoardStateChanged();
    }

    private void AddArchiveItem(WorkspaceArchiveItem archiveItem)
    {
        using var stream = new MemoryStream(archiveItem.ImageData, writable: false);

        var item = new WorkspaceCanvasItemViewModel
        {
            Bitmap = new Bitmap(stream),
            Label = archiveItem.Label,
            MimeType = archiveItem.MimeType,
            ImageData = archiveItem.ImageData,
            OriginalWidth = archiveItem.OriginalWidth,
            OriginalHeight = archiveItem.OriginalHeight,
            Width = archiveItem.Width,
            Height = archiveItem.Height,
            X = archiveItem.X,
            Y = archiveItem.Y,
            ZIndex = archiveItem.ZIndex
        };

        item.RemoveRequested += OnRemoveRequested;
        item.PropertyChanged += OnItemPropertyChanged;
        Items.Add(item);
        _nextZIndex = Math.Max(_nextZIndex, item.ZIndex + 1);
    }

    private static WorkspaceArchiveItem ToArchiveItem(WorkspaceCanvasItemViewModel item)
    {
        var imageData = item.ImageData.Length > 0
            ? item.ImageData
            : EncodeBitmap(item.Bitmap);

        return new WorkspaceArchiveItem(
            Label: item.Label,
            MimeType: item.MimeType,
            ImageData: imageData,
            X: item.X,
            Y: item.Y,
            Width: item.Width,
            Height: item.Height,
            OriginalWidth: item.OriginalWidth,
            OriginalHeight: item.OriginalHeight,
            ZIndex: item.ZIndex);
    }

    private static byte[] EncodeBitmap(Bitmap? bitmap)
    {
        if (bitmap is null)
            return [];

        using var stream = new MemoryStream();
        bitmap.Save(stream);
        return stream.ToArray();
    }

    private void OnRemoveRequested(WorkspaceCanvasItemViewModel item)
    {
        item.RemoveRequested -= OnRemoveRequested;
        item.PropertyChanged -= OnItemPropertyChanged;

        if (_selectedItem == item)
            _selectedItem = Items.LastOrDefault(i => i != item && i.IsSelected);

        _moveStartPositions.Remove(item);

        Items.Remove(item);
        item.Dispose();
        QueueLargeImageMemoryCompaction();

        if (Items.Count == 0)
        {
            _nextFallbackPasteX = CanvasPadding;
            _nextFallbackPasteY = CanvasPadding;
            _nextZIndex = 0;
        }

        NotifyBoardStateChanged();
        MarkDirty();
    }

    private void MarkDirty()
    {
        if (Items.Count > 0)
            HasUnsavedChanges = true;

        WorkspaceChanged?.Invoke();
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasImages));
        OnPropertyChanged(nameof(CounterText));
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WorkspaceCanvasItemViewModel.X)
            or nameof(WorkspaceCanvasItemViewModel.Y)
            or nameof(WorkspaceCanvasItemViewModel.Width)
            or nameof(WorkspaceCanvasItemViewModel.Height))
        {
            if (_isBatchMovingSelection)
                return;

            NotifyBoardStateChanged(sender as WorkspaceCanvasItemViewModel);
            MarkDirty();
        }
    }

    private void NotifyBoardStateChanged(WorkspaceCanvasItemViewModel? changedItem = null)
    {
        var previousBoardWidth = _boardWidth;
        var previousBoardHeight = _boardHeight;

        UpdateBoardLayout(changedItem);

        if (Math.Abs(_boardWidth - previousBoardWidth) >= 0.001)
            OnPropertyChanged(nameof(BoardWidth));

        if (Math.Abs(_boardHeight - previousBoardHeight) >= 0.001)
            OnPropertyChanged(nameof(BoardHeight));
    }

    private void UpdateBoardLayout(WorkspaceCanvasItemViewModel? changedItem = null)
    {
        var previousStartX = BoardStartX;
        var previousStartY = BoardStartY;

        if (Items.Count == 0)
        {
            BoardStartX = 0;
            BoardStartY = 0;
            _boardWidth = MinimumBoardWidth;
            _boardHeight = MinimumBoardHeight;
            NotifyBoardOriginShifted(previousStartX, previousStartY);
            return;
        }

        var minX = double.PositiveInfinity;
        var minY = double.PositiveInfinity;
        var maxRight = double.NegativeInfinity;
        var maxBottom = double.NegativeInfinity;

        foreach (var item in Items)
        {
            minX = Math.Min(minX, item.X);
            minY = Math.Min(minY, item.Y);
            maxRight = Math.Max(maxRight, item.X + item.Width);
            maxBottom = Math.Max(maxBottom, item.Y + item.Height);
        }

        BoardStartX = Math.Min(0, minX - CanvasPadding);
        BoardStartY = Math.Min(0, minY - CanvasPadding);
        _boardWidth = Math.Max(MinimumBoardWidth, maxRight + CanvasPadding) - BoardStartX;
        _boardHeight = Math.Max(MinimumBoardHeight, maxBottom + CanvasPadding) - BoardStartY;

        var originChanged = Math.Abs(BoardStartX - previousStartX) >= 0.001
            || Math.Abs(BoardStartY - previousStartY) >= 0.001;

        if (originChanged || changedItem is null)
        {
            foreach (var item in Items)
                UpdateDisplayPosition(item);
        }
        else
        {
            UpdateDisplayPosition(changedItem);
        }

        NotifyBoardOriginShifted(previousStartX, previousStartY);
    }

    private void UpdateDisplayPosition(WorkspaceCanvasItemViewModel item)
    {
        item.DisplayX = item.X - BoardStartX;
        item.DisplayY = item.Y - BoardStartY;
    }

    private void NotifyBoardOriginShifted(double previousStartX, double previousStartY)
    {
        var deltaX = BoardStartX - previousStartX;
        var deltaY = BoardStartY - previousStartY;

        if (Math.Abs(deltaX) < 0.001 && Math.Abs(deltaY) < 0.001)
            return;

        BoardOriginShifted?.Invoke(deltaX, deltaY);
    }

    private Point GetPastePosition(double width, double height, Point? pasteAnchor, int pasteIndex)
    {
        if (pasteAnchor is { } anchor)
        {
            var offset = PasteOffsetStep * pasteIndex;
            return new Point(
                anchor.X - width / 2 + offset,
                anchor.Y - height / 2 + offset);
        }

        var point = new Point(_nextFallbackPasteX, _nextFallbackPasteY);
        AdvanceFallbackPastePosition(width, height);
        return point;
    }

    private void AdvanceFallbackPastePosition(double width, double height)
    {
        _nextFallbackPasteX += PasteOffsetStep;
        _nextFallbackPasteY += PasteOffsetStep;

        var maxX = Math.Max(CanvasPadding, MinimumBoardWidth - width - CanvasPadding);
        var maxY = Math.Max(CanvasPadding, MinimumBoardHeight - height - CanvasPadding);

        if (_nextFallbackPasteX > maxX || _nextFallbackPasteY > maxY)
        {
            _nextFallbackPasteX = CanvasPadding;
            _nextFallbackPasteY = CanvasPadding;
        }
    }

    private void OnPreferencesChanged()
    {
        OnPropertyChanged(nameof(WorkspaceViewportBackground));
        OnPropertyChanged(nameof(WorkspaceBoardBackground));
        OnPropertyChanged(nameof(WorkspaceBoardBorderBrush));
        OnPropertyChanged(nameof(InitialZoom));
        OnPropertyChanged(nameof(PasteMode));
        WorkspacePreferencesChanged?.Invoke();
    }

    private (string Viewport, string Board, string Border) GetWorkspacePalette()
    {
        var light = _preferencesService.Current.Theme == AppThemePreference.Light;

        if (light)
        {
            return _preferencesService.Current.WorkspaceBackground switch
            {
                WorkspaceBackgroundPreference.Neutral => ("#e5e7eb", "#f8fafc", "#cbd5e1"),
                WorkspaceBackgroundPreference.Black => ("#171717", "#262626", "#525252"),
                _ => ("#dbeafe", "#eff6ff", "#93c5fd")
            };
        }

        return _preferencesService.Current.WorkspaceBackground switch
        {
            WorkspaceBackgroundPreference.Neutral => ("#111827", "#1f2937", "#374151"),
            WorkspaceBackgroundPreference.Black => ("#000000", "#171717", "#404040"),
            _ => ("#06152f", "#0b2142", "#1d4ed8")
        };
    }

    private static IBrush BrushFromHex(string color) => new SolidColorBrush(Color.Parse(color));

    private static void QueueLargeImageMemoryCompaction()
    {
        if (_isMemoryCompactionQueued)
            return;

        _isMemoryCompactionQueued = true;

        Dispatcher.UIThread.Post(() =>
        {
            _isMemoryCompactionQueued = false;
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        }, DispatcherPriority.Background);
    }
}
