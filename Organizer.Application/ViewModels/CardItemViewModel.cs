using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Organizer.Application.ViewModels.Components;

public partial class CardItemViewModel : ObservableObject, IDisposable
{
    [ObservableProperty] private bool _isLoaded;

    [ObservableProperty] private Bitmap? _thumbnail;

    public int Id { get; init; }
    public int CardId { get; init; }
    public string Filename { get; init; } = string.Empty;
    public string MimeType { get; init; } = "application/octet-stream";
    public string Description { get; init; } = string.Empty;
    public string CreatedAt { get; init; } = string.Empty;
    public Func<Task<Stream?>>? LoadImageDataStreamAsync { get; init; }
    public bool IsGroup { get; init; }
    public int ImageCount { get; init; }

    [ObservableProperty] private byte[]? _imageData;

    public void ReleaseResources()
    {
        var thumbnail = Thumbnail;
        Thumbnail = null;
        ImageData = null;
        IsLoaded = false;

        if (thumbnail is not null)
            Dispatcher.UIThread.Post(() => thumbnail.Dispose(), DispatcherPriority.Background);
    }

    public void Dispose() => ReleaseResources();

    public event Action<CardItemViewModel>? ViewRequested;
    public event Action<CardItemViewModel>? EditRequested;
    public event Action<CardItemViewModel>? DeleteRequested;
    public event Action<CardItemViewModel>? CopyRequested;

    [RelayCommand]
    private void View()
    {
        ViewRequested?.Invoke(this);
    }

    [RelayCommand]
    private void Edit()
    {
        EditRequested?.Invoke(this);
    }

    [RelayCommand]
    private void Delete()
    {
        DeleteRequested?.Invoke(this);
    }

    public void RequestCopy()
    {
        CopyRequested?.Invoke(this);
    }
}
