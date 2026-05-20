using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Organizer.Application.ViewModels;

public partial class GroupCopyPickerViewModel : ObservableObject
{
    private int _loadVersion;
    private Func<int, Task<byte[]?>>? _loadImageDataAsync;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Counter))]
    [NotifyPropertyChangedFor(nameof(CanGoPrevious))]
    [NotifyPropertyChangedFor(nameof(CanGoNext))]
    private int _currentIndex;

    [ObservableProperty] private bool _isVisible;

    [ObservableProperty] private Bitmap? _currentBitmap;

    [ObservableProperty] private string _title = "Escolha a imagem para copiar";

    public IReadOnlyList<int> ImageIds { get; private set; } = [];

    public string Counter => ImageIds.Count == 0
        ? ""
        : $"{CurrentIndex + 1} / {ImageIds.Count}";

    public bool CanGoPrevious => CurrentIndex > 0;
    public bool CanGoNext => CurrentIndex < ImageIds.Count - 1;
    public int CurrentImageId => ImageIds.Count == 0 ? 0 : ImageIds[CurrentIndex];

    public async Task OpenAsync(
        IEnumerable<int> imageIds,
        Func<int, Task<byte[]?>> loadImageDataAsync,
        int startIndex = 0)
    {
        ImageIds = imageIds.ToList();
        _loadImageDataAsync = loadImageDataAsync;
        CurrentIndex = Math.Clamp(startIndex, 0, Math.Max(0, ImageIds.Count - 1));
        RefreshNavigationState();
        IsVisible = true;

        await LoadCurrentBitmapAsync();
    }

    public async Task<byte[]?> LoadCurrentImageDataAsync()
    {
        if (_loadImageDataAsync is null || ImageIds.Count == 0)
            return null;

        return await _loadImageDataAsync(CurrentImageId);
    }

    [RelayCommand(CanExecute = nameof(CanGoPrevious))]
    private async Task Previous()
    {
        if (!CanGoPrevious)
            return;

        CurrentIndex--;
        RefreshNavigationState();
        await LoadCurrentBitmapAsync();
    }

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private async Task Next()
    {
        if (!CanGoNext)
            return;

        CurrentIndex++;
        RefreshNavigationState();
        await LoadCurrentBitmapAsync();
    }

    [RelayCommand]
    private void Close()
    {
        IsVisible = false;

        var previousBitmap = CurrentBitmap;
        CurrentBitmap = null;
        ScheduleDispose(previousBitmap);

        ImageIds = [];
        _loadImageDataAsync = null;
        CurrentIndex = 0;
        RefreshNavigationState();
    }

    private async Task LoadCurrentBitmapAsync()
    {
        if (_loadImageDataAsync is null || ImageIds.Count == 0 || CurrentIndex < 0 || CurrentIndex >= ImageIds.Count)
        {
            var emptyPrevious = CurrentBitmap;
            CurrentBitmap = null;
            ScheduleDispose(emptyPrevious);
            return;
        }

        var version = ++_loadVersion;
        var data = await _loadImageDataAsync(CurrentImageId);

        if (version != _loadVersion)
            return;

        if (data is null || data.Length == 0)
        {
            var emptyPrevious = CurrentBitmap;
            CurrentBitmap = null;
            ScheduleDispose(emptyPrevious);
            return;
        }

        using var stream = new MemoryStream(data);
        var bitmap = new Bitmap(stream);

        var previousBitmap = CurrentBitmap;
        CurrentBitmap = bitmap;
        RefreshNavigationState();
        ScheduleDispose(previousBitmap);
    }

    private void RefreshNavigationState()
    {
        OnPropertyChanged(nameof(Counter));
        OnPropertyChanged(nameof(CanGoPrevious));
        OnPropertyChanged(nameof(CanGoNext));
        PreviousCommand.NotifyCanExecuteChanged();
        NextCommand.NotifyCanExecuteChanged();
    }

    private void ScheduleDispose(Bitmap? bitmap)
    {
        if (bitmap is null)
            return;

        Dispatcher.UIThread.Post(() => bitmap.Dispose(), DispatcherPriority.Background);
    }
}