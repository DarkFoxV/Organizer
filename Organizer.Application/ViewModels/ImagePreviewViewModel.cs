using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Organizer.Application.Services;

namespace Organizer.Application.ViewModels.Components;

public partial class ImagePreviewViewModel : ObservableObject, IDisposable
{
    private int _loadVersion;
    private Func<int, Task<Stream?>>? _openImageDataStreamAsync;
    private bool _isDisposed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Counter))]
    [NotifyPropertyChangedFor(nameof(CanGoPrevious))]
    [NotifyPropertyChangedFor(nameof(CanGoNext))]
    private int _currentIndex;

    [ObservableProperty] private bool _isVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoPrevious))]
    [NotifyPropertyChangedFor(nameof(CanGoNext))]
    private bool _isLoading;

    [ObservableProperty] private Bitmap? _currentBitmap;

    public IReadOnlyList<int> ImageIds { get; private set; } = [];

    public string Counter => ImageIds.Count == 0
        ? ""
        : $"{CurrentIndex + 1} / {ImageIds.Count}";

    public bool CanGoPrevious => !IsLoading && CurrentIndex > 0;
    public bool CanGoNext => !IsLoading && CurrentIndex < ImageIds.Count - 1;

    public async Task OpenAsync(
        IEnumerable<int> imageIds,
        Func<int, Task<Stream?>> openImageDataStreamAsync,
        int startIndex = 0)
    {
        if (_isDisposed)
            return;

        var list = imageIds.ToList();
        Console.WriteLine($"[Preview] Open chamado com {list.Count} imagens");

        ImageIds = list;
        _openImageDataStreamAsync = openImageDataStreamAsync;

        CurrentIndex = Math.Clamp(startIndex, 0, Math.Max(0, ImageIds.Count - 1));
        RefreshNavigationState();
        IsVisible = true;

        await LoadCurrentBitmapAsync();
    }

    [RelayCommand(CanExecute = nameof(CanGoPrevious))]
    private async Task Previous()
    {
        if (_isDisposed)
            return;

        if (IsLoading)
            return;

        if (!CanGoPrevious)
            return;

        CurrentIndex--;
        PreviousCommand.NotifyCanExecuteChanged();
        NextCommand.NotifyCanExecuteChanged();
        await LoadCurrentBitmapAsync();
    }

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private async Task Next()
    {
        if (_isDisposed)
            return;

        if (IsLoading)
            return;

        if (!CanGoNext)
            return;

        CurrentIndex++;
        PreviousCommand.NotifyCanExecuteChanged();
        NextCommand.NotifyCanExecuteChanged();
        await LoadCurrentBitmapAsync();
    }

    [RelayCommand]
    public void Close()
    {
        CloseCore(queueMemoryCompaction: true);
    }

    public void CloseWithoutMemoryCompaction()
    {
        CloseCore(queueMemoryCompaction: false);
    }

    private void CloseCore(bool queueMemoryCompaction)
    {
        var hadImageResources = IsVisible || CurrentBitmap is not null || ImageIds.Count > 0;

        _loadVersion++;
        IsVisible = false;
        IsLoading = false;

        var previousBitmap = CurrentBitmap;
        CurrentBitmap = null;
        ScheduleDispose(previousBitmap);

        ImageIds = [];
        _openImageDataStreamAsync = null;
        CurrentIndex = 0;
        RefreshNavigationState();

        if (queueMemoryCompaction && hadImageResources)
            MemoryCleanupService.QueueLargeImageMemoryCompaction();
    }

    private async Task LoadCurrentBitmapAsync()
    {
        if (_isDisposed)
            return;

        var version = ++_loadVersion;
        IsLoading = true;

        try
        {
            await ReleaseCurrentBitmapAsync();

            if (version != _loadVersion || _isDisposed)
                return;

            if (_openImageDataStreamAsync is null || ImageIds.Count == 0 || CurrentIndex < 0 || CurrentIndex >= ImageIds.Count)
                return;

            var imageId = ImageIds[CurrentIndex];

            await using var stream = await _openImageDataStreamAsync(imageId);

            if (stream is null)
                return;

            var bitmap = new Bitmap(stream);

            if (version != _loadVersion)
            {
                bitmap.Dispose();
                return;
            }

            CurrentBitmap = bitmap;

            RefreshNavigationState();
        }
        finally
        {
            if (version == _loadVersion && !_isDisposed)
                IsLoading = false;
        }
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

    private async Task ReleaseCurrentBitmapAsync()
    {
        var previousBitmap = CurrentBitmap;
        if (previousBitmap is null)
            return;

        CurrentBitmap = null;
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Render);
        previousBitmap.Dispose();
    }

    partial void OnIsLoadingChanged(bool value)
    {
        PreviousCommand.NotifyCanExecuteChanged();
        NextCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        Close();
        _isDisposed = true;
    }
}
