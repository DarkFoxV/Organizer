using System;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Media.Imaging;
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
    private WorkspaceCanvasItemViewModel? _selectedItem;
    private double _nextFallbackPasteX = CanvasPadding;
    private double _nextFallbackPasteY = CanvasPadding;
    private int _nextZIndex;

    [ObservableProperty] private bool _isPasting;

    [ObservableProperty] private string? _errorMessage;

    [ObservableProperty] private double _boardStartX;

    [ObservableProperty] private double _boardStartY;

    public event Action<double, double>? BoardOriginShifted;
    public event Action? WorkspacePreferencesChanged;

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

    public string CounterText => Items.Count == 1 ? "1 imagem no canvas" : $"{Items.Count} imagens no canvas";

    public double BoardWidth
    {
        get
        {
            if (Items.Count == 0)
                return MinimumBoardWidth;

            var minX = Math.Min(0, Items.Min(item => item.X) - CanvasPadding);
            var maxRight = Math.Max(
                MinimumBoardWidth,
                Items.Max(item => item.X + item.Width) + CanvasPadding);

            return maxRight - minX;
        }
    }

    public double BoardHeight
    {
        get
        {
            if (Items.Count == 0)
                return MinimumBoardHeight;

            var minY = Math.Min(0, Items.Min(item => item.Y) - CanvasPadding);
            var maxBottom = Math.Max(
                MinimumBoardHeight,
                Items.Max(item => item.Y + item.Height) + CanvasPadding);

            return maxBottom - minY;
        }
    }

    public WorkspaceViewModel(
        IClipboardService clipboardService,
        AppPreferencesService preferencesService)
    {
        _clipboardService = clipboardService;
        _preferencesService = preferencesService;
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

    [RelayCommand]
    private void ClearAll()
    {
        foreach (var item in Items.ToList())
        {
            item.RemoveRequested -= OnRemoveRequested;
            item.PropertyChanged -= OnItemPropertyChanged;
            item.Dispose();
        }

        Items.Clear();
        _selectedItem = null;
        _nextFallbackPasteX = CanvasPadding;
        _nextFallbackPasteY = CanvasPadding;
        _nextZIndex = 0;
        ErrorMessage = null;
        NotifyBoardStateChanged();
    }

    public void SelectItem(WorkspaceCanvasItemViewModel item)
    {
        if (_selectedItem == item)
        {
            item.IsSelected = true;
            return;
        }

        if (_selectedItem is not null)
            _selectedItem.IsSelected = false;

        _selectedItem = item;
        _selectedItem.IsSelected = true;
    }

    public void ClearSelection()
    {
        if (_selectedItem is null)
            return;

        _selectedItem.IsSelected = false;
        _selectedItem = null;
    }

    public bool RemoveSelectedItem()
    {
        if (_selectedItem is null)
            return false;

        OnRemoveRequested(_selectedItem);
        return true;
    }

    private void AddImage(ClipboardImageData image, Point? pasteAnchor, int pasteIndex)
    {
        using var stream = new MemoryStream(image.Data, writable: false);
        AddBitmap(new Bitmap(stream), image.Filename, pasteAnchor, pasteIndex);
    }

    private void AddBitmap(Bitmap bitmap, string sourceName, Point? pasteAnchor, int pasteIndex)
    {
        var width = bitmap.PixelSize.Width;
        var height = bitmap.PixelSize.Height;
        var position = GetPastePosition(width, height, pasteAnchor, pasteIndex);

        var item = new WorkspaceCanvasItemViewModel
        {
            Bitmap = bitmap,
            Label = sourceName,
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

    private void OnRemoveRequested(WorkspaceCanvasItemViewModel item)
    {
        item.RemoveRequested -= OnRemoveRequested;
        item.PropertyChanged -= OnItemPropertyChanged;

        if (_selectedItem == item)
            _selectedItem = null;

        Items.Remove(item);
        item.Dispose();

        if (Items.Count == 0)
        {
            _nextFallbackPasteX = CanvasPadding;
            _nextFallbackPasteY = CanvasPadding;
            _nextZIndex = 0;
        }

        NotifyBoardStateChanged();
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(CounterText));
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WorkspaceCanvasItemViewModel.X)
            or nameof(WorkspaceCanvasItemViewModel.Y)
            or nameof(WorkspaceCanvasItemViewModel.Width)
            or nameof(WorkspaceCanvasItemViewModel.Height))
        {
            NotifyBoardStateChanged();
        }
    }

    private void NotifyBoardStateChanged()
    {
        UpdateBoardLayout();
        OnPropertyChanged(nameof(BoardWidth));
        OnPropertyChanged(nameof(BoardHeight));
    }

    private void UpdateBoardLayout()
    {
        var previousStartX = BoardStartX;
        var previousStartY = BoardStartY;

        if (Items.Count == 0)
        {
            BoardStartX = 0;
            BoardStartY = 0;
            NotifyBoardOriginShifted(previousStartX, previousStartY);
            return;
        }

        var minX = Items.Min(item => item.X);
        var minY = Items.Min(item => item.Y);

        BoardStartX = Math.Min(0, minX - CanvasPadding);
        BoardStartY = Math.Min(0, minY - CanvasPadding);

        foreach (var item in Items)
        {
            item.DisplayX = item.X - BoardStartX;
            item.DisplayY = item.Y - BoardStartY;
        }

        NotifyBoardOriginShifted(previousStartX, previousStartY);
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
}
