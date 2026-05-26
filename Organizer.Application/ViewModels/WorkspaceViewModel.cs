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
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Organizer.Application.Services;

namespace Organizer.Application.ViewModels;

public partial class WorkspaceViewModel : ObservableObject, IDisposable
{
    private const double CanvasPadding = 200;
    private const double PasteOffsetStep = 48;
    private const double BatchPasteGap = 36;
    private const double MinimumBoardWidth = 4000;
    private const double MinimumBoardHeight = 2500;
    private const int ThumbnailMaxDimension = 400;
    private const int HalfLodMinDimension = 1024;
    private const int QuarterLodMinDimension = 2048;

    private readonly IClipboardService _clipboardService;
    private readonly AppPreferencesService _preferencesService;
    private readonly WorkspaceArchiveService _workspaceArchiveService;
    private readonly Stack<WorkspaceSnapshot> _undoStack = [];
    private readonly Stack<WorkspaceSnapshot> _redoStack = [];
    private WorkspaceCanvasItemViewModel? _selectedItem;
    private readonly Dictionary<WorkspaceCanvasItemViewModel, Point> _moveStartPositions = [];
    private bool _isInteractiveItemUpdate;
    private bool _hasPendingInteractiveItemChange;
    private WorkspaceSnapshot? _interactiveStartSnapshot;
    private double _boardWidth = MinimumBoardWidth;
    private double _boardHeight = MinimumBoardHeight;
    private double _nextFallbackPasteX = CanvasPadding;
    private double _nextFallbackPasteY = CanvasPadding;
    private int _nextZIndex;
    private IStorageFile? _workspaceFile;
    private static bool _isMemoryCompactionQueued;

    private sealed record PendingWorkspaceImage(
        Bitmap Bitmap,
        string SourceName,
        string MimeType,
        byte[] ImageData,
        double Width,
        double Height);

    private sealed record WorkspaceSnapshot(
        IReadOnlyList<WorkspaceSnapshotItem> Items,
        double NextFallbackPasteX,
        double NextFallbackPasteY,
        int NextZIndex);

    private sealed record WorkspaceSnapshotItem(
        string Label,
        string MimeType,
        byte[] ImageData,
        double X,
        double Y,
        double Width,
        double Height,
        double OriginalWidth,
        double OriginalHeight,
        int ZIndex,
        bool IsSelected);

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

    public bool HasWorkspaceFile => _workspaceFile is not null;

    public bool CanUndo => _undoStack.Count > 0;

    public bool CanRedo => _redoStack.Count > 0;

    private int HistoryLimit => Math.Clamp(
        _preferencesService.Current.WorkspaceHistoryLimit,
        AppPreferences.MinWorkspaceHistoryLimit,
        AppPreferences.MaxWorkspaceHistoryLimit);

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

            if (images.Count == 1)
            {
                var undoSnapshot = CaptureWorkspaceSnapshot();
                AddImage(images[0], pasteAnchor, 0);
                PushUndoSnapshot(undoSnapshot);
            }
            else
            {
                var undoSnapshot = CaptureWorkspaceSnapshot();
                AddImageBatch(images, pasteAnchor);
                PushUndoSnapshot(undoSnapshot);
            }

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
        ClearAllCore();
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

    public async Task<bool> SaveToCurrentFileAsync()
    {
        if (_workspaceFile is null)
        {
            ErrorMessage = "Workspace atual nao tem arquivo de destino.";
            return false;
        }

        return await SaveToFileCoreAsync(_workspaceFile, rememberFile: false);
    }

    public async Task<bool> SaveToFileAsync(IStorageFile file)
    {
        return await SaveToFileCoreAsync(file, rememberFile: true);
    }

    private async Task<bool> SaveToFileCoreAsync(IStorageFile file, bool rememberFile)
    {
        try
        {
            await using var stream = await file.OpenWriteAsync();
            if (!await SaveAsync(stream))
                return false;

            if (rememberFile)
                SetWorkspaceFile(file);

            return true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Erro ao salvar workspace: {ex.Message}";
            return false;
        }
    }

    public void SetWorkspaceFile(IStorageFile file)
    {
        _workspaceFile = file;
        OnPropertyChanged(nameof(HasWorkspaceFile));
    }

    public void ClearWorkspaceFile()
    {
        if (_workspaceFile is null)
            return;

        _workspaceFile = null;
        OnPropertyChanged(nameof(HasWorkspaceFile));
    }

    public async Task<bool> LoadAsync(Stream input)
    {
        try
        {
            ErrorMessage = null;
            var archiveItems = await _workspaceArchiveService.LoadAsync(input);

            ClearAllCore();
            ClearHistory();

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
        ClearAllCore();
        ClearHistory();
        ClearWorkspaceFile();
        HasUnsavedChanges = false;
    }

    public async Task<bool> CopySelectedImageAsync(IClipboard clipboard)
    {
        var item = Items
            .Where(i => i.IsSelected)
            .OrderByDescending(i => i.ZIndex)
            .FirstOrDefault();

        if (item is null)
            return false;

        var imageData = item.ImageData.Length > 0
            ? item.ImageData
            : EncodeBitmap(item.Bitmap);

        if (imageData.Length == 0)
        {
            ErrorMessage = "Imagem selecionada nao tem dados para copiar.";
            return false;
        }

        ErrorMessage = null;
        return await _clipboardService.SetImageAsync(clipboard, imageData, item.MimeType);
    }

    [RelayCommand]
    private void ClearAll()
    {
        if (Items.Count == 0)
            return;

        var undoSnapshot = CaptureWorkspaceSnapshot();
        ClearAllCore();
        PushUndoSnapshot(undoSnapshot);
        MarkDirty();
    }

    private void ClearAllCore()
    {
        var hadImages = Items.Count > 0;

        foreach (var item in Items.ToList())
        {
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

    public bool Undo()
    {
        if (_undoStack.Count == 0)
            return false;

        var redoSnapshot = CaptureWorkspaceSnapshot();
        var undoSnapshot = _undoStack.Pop();
        RestoreWorkspaceSnapshot(undoSnapshot);
        PushRedoSnapshot(redoSnapshot);
        MarkDirty();
        NotifyHistoryStateChanged();
        return true;
    }

    public bool Redo()
    {
        if (_redoStack.Count == 0)
            return false;

        var undoSnapshot = CaptureWorkspaceSnapshot();
        var redoSnapshot = _redoStack.Pop();
        RestoreWorkspaceSnapshot(redoSnapshot);
        PushUndoSnapshot(undoSnapshot, clearRedo: false);
        MarkDirty();
        NotifyHistoryStateChanged();
        return true;
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

    public void SelectItemsInBounds(Rect bounds, bool additive)
    {
        var selectedItem = _selectedItem;

        foreach (var item in Items)
        {
            if (Intersects(item.Bounds, bounds))
            {
                item.IsSelected = true;
                selectedItem = item;
            }
            else if (!additive)
            {
                item.IsSelected = false;
            }
        }

        _selectedItem = Items.LastOrDefault(i => i.IsSelected && ReferenceEquals(i, selectedItem))
            ?? Items.LastOrDefault(i => i.IsSelected);
    }

    private void SelectItems(IReadOnlyList<WorkspaceCanvasItemViewModel> items)
    {
        ClearSelection();

        foreach (var item in items)
            item.IsSelected = true;

        _selectedItem = items.LastOrDefault();
    }

    public bool RemoveSelectedItems()
    {
        var selectedItems = Items.Where(i => i.IsSelected).ToList();
        if (selectedItems.Count == 0)
            return false;

        var undoSnapshot = CaptureWorkspaceSnapshot();

        foreach (var item in selectedItems)
            RemoveItemCore(item, notifyBoard: false, markDirty: false);

        NotifyBoardStateChanged();
        PushUndoSnapshot(undoSnapshot);
        MarkDirty();
        return true;
    }

    public void RemoveItem(WorkspaceCanvasItemViewModel item)
    {
        if (!Items.Contains(item))
            return;

        var undoSnapshot = CaptureWorkspaceSnapshot();
        RemoveItemCore(item, markDirty: false);
        PushUndoSnapshot(undoSnapshot);
        MarkDirty();
    }

    public void BeginMoveSelectedItems(WorkspaceCanvasItemViewModel anchorItem)
    {
        if (!anchorItem.IsSelected)
            SelectItem(anchorItem);

        _moveStartPositions.Clear();
        _interactiveStartSnapshot = CaptureWorkspaceSnapshot();

        foreach (var item in Items.Where(i => i.IsSelected))
            _moveStartPositions[item] = new Point(item.X, item.Y);
    }

    public void MoveSelectedItems(double deltaX, double deltaY)
    {
        if (_moveStartPositions.Count == 0)
            return;

        _isInteractiveItemUpdate = true;

        try
        {
            foreach (var (item, startPosition) in _moveStartPositions)
            {
                item.X = startPosition.X + deltaX;
                item.Y = startPosition.Y + deltaY;
                UpdateDisplayPosition(item);
            }
        }
        finally
        {
            _isInteractiveItemUpdate = false;
        }

        _hasPendingInteractiveItemChange = true;
    }

    public void EndMoveSelectedItems()
    {
        _moveStartPositions.Clear();
        CommitInteractiveItemChanges();
    }

    public void ResizeItem(
        WorkspaceCanvasItemViewModel item,
        double x,
        double y,
        double width,
        double height)
    {
        if (!Items.Contains(item))
            return;

        _interactiveStartSnapshot ??= CaptureWorkspaceSnapshot();
        _isInteractiveItemUpdate = true;

        try
        {
            item.X = x;
            item.Y = y;
            item.Width = width;
            item.Height = height;
            UpdateDisplayPosition(item);
        }
        finally
        {
            _isInteractiveItemUpdate = false;
        }

        _hasPendingInteractiveItemChange = true;
    }

    public void CommitInteractiveItemChanges()
    {
        if (!_hasPendingInteractiveItemChange)
        {
            _interactiveStartSnapshot = null;
            return;
        }

        _hasPendingInteractiveItemChange = false;
        var undoSnapshot = _interactiveStartSnapshot;
        _interactiveStartSnapshot = null;

        if (undoSnapshot is not null && WorkspaceSnapshotsEqual(undoSnapshot, CaptureWorkspaceSnapshot()))
            return;

        if (undoSnapshot is not null)
            PushUndoSnapshot(undoSnapshot);

        NotifyBoardStateChanged();
        MarkDirty();
    }

    private void AddImage(ClipboardImageData image, Point? pasteAnchor, int pasteIndex)
    {
        using var stream = new MemoryStream(image.Data, writable: false);
        var bitmap = new Bitmap(stream);

        try
        {
            var position = GetPastePosition(bitmap.PixelSize.Width, bitmap.PixelSize.Height, pasteAnchor, pasteIndex);
            AddBitmap(bitmap, image.Filename, image.MimeType, image.Data, position);
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    private void AddImageBatch(IReadOnlyList<ClipboardImageData> images, Point? pasteAnchor)
    {
        var pendingImages = new List<PendingWorkspaceImage>(images.Count);
        var addedViewModels = new List<WorkspaceCanvasItemViewModel>(images.Count);
        var addedItems = 0;

        try
        {
            foreach (var image in images)
            {
                using var stream = new MemoryStream(image.Data, writable: false);
                var bitmap = new Bitmap(stream);
                pendingImages.Add(new PendingWorkspaceImage(
                    bitmap,
                    image.Filename,
                    image.MimeType,
                    image.Data,
                    bitmap.PixelSize.Width,
                    bitmap.PixelSize.Height));
            }

            pendingImages.Sort(static (a, b) =>
                (b.Width * b.Height).CompareTo(a.Width * a.Height));

            var positions = GetBatchPastePositions(pendingImages, pasteAnchor);

            for (var i = 0; i < pendingImages.Count; i++)
            {
                var pending = pendingImages[i];
                var item = AddBitmap(
                    pending.Bitmap,
                    pending.SourceName,
                    pending.MimeType,
                    pending.ImageData,
                    positions[i],
                    selectItem: false,
                    notifyBoard: false);
                addedViewModels.Add(item);
                addedItems++;
            }

            SelectItems(addedViewModels);
            NotifyBoardStateChanged();
        }
        finally
        {
            for (var i = addedItems; i < pendingImages.Count; i++)
                pendingImages[i].Bitmap.Dispose();
        }
    }

    private WorkspaceCanvasItemViewModel AddBitmap(
        Bitmap bitmap,
        string sourceName,
        string mimeType,
        byte[] imageData,
        Point position,
        bool selectItem = true,
        bool notifyBoard = true)
    {
        var width = bitmap.PixelSize.Width;
        var height = bitmap.PixelSize.Height;

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

        GenerateLods(item);
        item.PropertyChanged += OnItemPropertyChanged;
        Items.Add(item);

        if (selectItem)
            SelectItem(item);

        if (notifyBoard)
            NotifyBoardStateChanged();

        return item;
    }

    private void AddArchiveItem(WorkspaceArchiveItem archiveItem)
    {
        using var stream = new MemoryStream(archiveItem.ImageData, writable: false);
        var bitmap = new Bitmap(stream);
        WorkspaceCanvasItemViewModel? item = null;

        try
        {
            item = new WorkspaceCanvasItemViewModel
            {
                Bitmap = bitmap,
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

            GenerateLods(item);
            item.PropertyChanged += OnItemPropertyChanged;
            Items.Add(item);
            _nextZIndex = Math.Max(_nextZIndex, item.ZIndex + 1);
        }
        catch
        {
            item?.Dispose();
            if (item is null)
                bitmap.Dispose();

            throw;
        }
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

    private WorkspaceSnapshot CaptureWorkspaceSnapshot()
    {
        return new WorkspaceSnapshot(
            Items.Select(ToSnapshotItem).ToList(),
            _nextFallbackPasteX,
            _nextFallbackPasteY,
            _nextZIndex);
    }

    private static WorkspaceSnapshotItem ToSnapshotItem(WorkspaceCanvasItemViewModel item)
    {
        var imageData = item.ImageData.Length > 0
            ? item.ImageData
            : EncodeBitmap(item.Bitmap);

        return new WorkspaceSnapshotItem(
            Label: item.Label,
            MimeType: item.MimeType,
            ImageData: imageData,
            X: item.X,
            Y: item.Y,
            Width: item.Width,
            Height: item.Height,
            OriginalWidth: item.OriginalWidth,
            OriginalHeight: item.OriginalHeight,
            ZIndex: item.ZIndex,
            IsSelected: item.IsSelected);
    }

    private void RestoreWorkspaceSnapshot(WorkspaceSnapshot snapshot)
    {
        ClearAllCore();

        _nextFallbackPasteX = snapshot.NextFallbackPasteX;
        _nextFallbackPasteY = snapshot.NextFallbackPasteY;
        _nextZIndex = snapshot.NextZIndex;

        foreach (var snapshotItem in snapshot.Items)
            AddSnapshotItem(snapshotItem);

        _selectedItem = Items.LastOrDefault(item => item.IsSelected);
        NotifyBoardStateChanged();
    }

    private void AddSnapshotItem(WorkspaceSnapshotItem snapshotItem)
    {
        if (snapshotItem.ImageData.Length == 0)
            throw new InvalidDataException("Workspace history item has no image data.");

        using var stream = new MemoryStream(snapshotItem.ImageData, writable: false);
        var bitmap = new Bitmap(stream);
        WorkspaceCanvasItemViewModel? item = null;

        try
        {
            item = new WorkspaceCanvasItemViewModel
            {
                Bitmap = bitmap,
                Label = snapshotItem.Label,
                MimeType = snapshotItem.MimeType,
                ImageData = snapshotItem.ImageData,
                OriginalWidth = snapshotItem.OriginalWidth,
                OriginalHeight = snapshotItem.OriginalHeight,
                Width = snapshotItem.Width,
                Height = snapshotItem.Height,
                X = snapshotItem.X,
                Y = snapshotItem.Y,
                ZIndex = snapshotItem.ZIndex,
                IsSelected = snapshotItem.IsSelected
            };

            GenerateLods(item);
            item.PropertyChanged += OnItemPropertyChanged;
            Items.Add(item);
        }
        catch
        {
            item?.Dispose();
            if (item is null)
                bitmap.Dispose();

            throw;
        }
    }

    private void PushUndoSnapshot(WorkspaceSnapshot snapshot, bool clearRedo = true)
    {
        var historyLimit = HistoryLimit;
        if (historyLimit == 0)
        {
            ClearHistory();
            return;
        }

        _undoStack.Push(snapshot);
        TrimHistory(_undoStack, historyLimit);

        if (clearRedo)
        {
            var hadRedoSnapshots = _redoStack.Count > 0;
            _redoStack.Clear();

            if (hadRedoSnapshots)
                QueueLargeImageMemoryCompaction();
        }

        NotifyHistoryStateChanged();
    }

    private void PushRedoSnapshot(WorkspaceSnapshot snapshot)
    {
        var historyLimit = HistoryLimit;
        if (historyLimit == 0)
        {
            ClearHistory();
            return;
        }

        _redoStack.Push(snapshot);
        TrimHistory(_redoStack, historyLimit);
        NotifyHistoryStateChanged();
    }

    private static void TrimHistory(Stack<WorkspaceSnapshot> stack, int historyLimit)
    {
        if (stack.Count <= historyLimit)
            return;

        var entries = stack
            .Take(historyLimit)
            .Reverse()
            .ToList();

        stack.Clear();

        foreach (var entry in entries)
            stack.Push(entry);
    }

    private void ClearHistory()
    {
        var hadSnapshots = _undoStack.Count > 0 || _redoStack.Count > 0;
        _undoStack.Clear();
        _redoStack.Clear();
        _interactiveStartSnapshot = null;
        NotifyHistoryStateChanged();

        if (hadSnapshots)
            QueueLargeImageMemoryCompaction();
    }

    private void NotifyHistoryStateChanged()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    private static bool WorkspaceSnapshotsEqual(WorkspaceSnapshot left, WorkspaceSnapshot right)
    {
        if (left.NextZIndex != right.NextZIndex
            || !DoubleEquals(left.NextFallbackPasteX, right.NextFallbackPasteX)
            || !DoubleEquals(left.NextFallbackPasteY, right.NextFallbackPasteY)
            || left.Items.Count != right.Items.Count)
        {
            return false;
        }

        for (var i = 0; i < left.Items.Count; i++)
        {
            if (!WorkspaceSnapshotItemsEqual(left.Items[i], right.Items[i]))
                return false;
        }

        return true;
    }

    private static bool WorkspaceSnapshotItemsEqual(WorkspaceSnapshotItem left, WorkspaceSnapshotItem right)
    {
        return left.Label == right.Label
            && left.MimeType == right.MimeType
            && (ReferenceEquals(left.ImageData, right.ImageData) || left.ImageData.SequenceEqual(right.ImageData))
            && DoubleEquals(left.X, right.X)
            && DoubleEquals(left.Y, right.Y)
            && DoubleEquals(left.Width, right.Width)
            && DoubleEquals(left.Height, right.Height)
            && DoubleEquals(left.OriginalWidth, right.OriginalWidth)
            && DoubleEquals(left.OriginalHeight, right.OriginalHeight)
            && left.ZIndex == right.ZIndex
            && left.IsSelected == right.IsSelected;
    }

    private static bool Intersects(Rect a, Rect b)
    {
        return a.Right >= b.Left
            && a.Left <= b.Right
            && a.Bottom >= b.Top
            && a.Top <= b.Bottom;
    }

    private static bool DoubleEquals(double left, double right)
    {
        return Math.Abs(left - right) < 0.001;
    }

    private void RemoveItemCore(
        WorkspaceCanvasItemViewModel item,
        bool notifyBoard = true,
        bool markDirty = true)
    {
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

        if (notifyBoard)
            NotifyBoardStateChanged();

        if (markDirty)
            MarkDirty();
    }

    private void MarkDirty()
    {
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
            if (_isInteractiveItemUpdate)
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

    private Point[] GetBatchPastePositions(
        IReadOnlyList<PendingWorkspaceImage> images,
        Point? pasteAnchor)
    {
        var columns = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(images.Count)));
        var rowMetrics = new List<(int Start, int Count, double Width, double Height)>();

        for (var start = 0; start < images.Count; start += columns)
        {
            var count = Math.Min(columns, images.Count - start);
            var rowWidth = 0d;
            var rowHeight = 0d;

            for (var i = start; i < start + count; i++)
            {
                rowWidth += images[i].Width;
                rowHeight = Math.Max(rowHeight, images[i].Height);
            }

            rowWidth += BatchPasteGap * Math.Max(0, count - 1);
            rowMetrics.Add((start, count, rowWidth, rowHeight));
        }

        var layoutWidth = rowMetrics.Max(row => row.Width);
        var layoutHeight = rowMetrics.Sum(row => row.Height)
            + BatchPasteGap * Math.Max(0, rowMetrics.Count - 1);

        var topLeft = pasteAnchor is { } anchor
            ? new Point(anchor.X - layoutWidth / 2, anchor.Y - layoutHeight / 2)
            : new Point(_nextFallbackPasteX, _nextFallbackPasteY);

        var positions = new Point[images.Count];
        var y = topLeft.Y;

        foreach (var row in rowMetrics)
        {
            var x = topLeft.X + (layoutWidth - row.Width) / 2;

            for (var i = row.Start; i < row.Start + row.Count; i++)
            {
                var image = images[i];
                positions[i] = new Point(x, y + (row.Height - image.Height) / 2);
                x += image.Width + BatchPasteGap;
            }

            y += row.Height + BatchPasteGap;
        }

        if (pasteAnchor is null)
            AdvanceFallbackPastePosition(layoutWidth, layoutHeight);

        return positions;
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
        var historyLimit = HistoryLimit;
        var undoCount = _undoStack.Count;
        var redoCount = _redoStack.Count;

        if (historyLimit == 0)
        {
            ClearHistory();
        }
        else
        {
            TrimHistory(_undoStack, historyLimit);
            TrimHistory(_redoStack, historyLimit);

            if (_undoStack.Count != undoCount || _redoStack.Count != redoCount)
            {
                NotifyHistoryStateChanged();
                QueueLargeImageMemoryCompaction();
            }
        }

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

    private static void GenerateLods(WorkspaceCanvasItemViewModel item)
    {
        if (item.Bitmap is not { } bmp)
            return;

        var w = bmp.PixelSize.Width;
        var h = bmp.PixelSize.Height;
        var maxDimension = Math.Max(w, h);

        if (maxDimension >= HalfLodMinDimension)
            item.HalfBitmap = bmp.CreateScaledBitmap(
                new PixelSize(Math.Max(1, w / 2), Math.Max(1, h / 2)),
                BitmapInterpolationMode.LowQuality);

        if (maxDimension >= QuarterLodMinDimension)
            item.QuarterBitmap = bmp.CreateScaledBitmap(
                new PixelSize(Math.Max(1, w / 4), Math.Max(1, h / 4)),
                BitmapInterpolationMode.LowQuality);

        if (w <= ThumbnailMaxDimension && h <= ThumbnailMaxDimension)
            return;

        var scale = Math.Min(
            (double)ThumbnailMaxDimension / w,
            (double)ThumbnailMaxDimension / h);

        var thumbSize = new PixelSize((int)(w * scale), (int)(h * scale));
        item.ThumbnailBitmap = bmp.CreateScaledBitmap(thumbSize, BitmapInterpolationMode.LowQuality);
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
