using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Organizer.Application.Services;

namespace Organizer.Application.ViewModels.Components;

public partial class ImageOrderItemViewModel : ObservableObject, IDisposable
{
    private bool _isDisposed;

    [ObservableProperty] private bool _isDragging;

    [ObservableProperty] private bool _isDropTarget;

    [ObservableProperty] private Bitmap? _thumbnail;

    public string Filename { get; init; } = string.Empty;

    public string MimeType { get; init; } = "application/octet-stream";

    public IStorageFile? SourceFile { get; set; }

    public byte[]? SourceData { get; set; }

    public byte[]? ThumbnailData { get; private set; }

    public bool HasFileSource => SourceFile is not null;

    public async Task LoadThumbnailAsync()
    {
        if (_isDisposed)
            return;

        await using var sourceStream = await OpenReadAsync();

        byte[] thumbnailData;
        try
        {
            thumbnailData = await Task.Run(() =>
                ImageThumbnailService.CreateThumbnail(sourceStream));
        }
        catch
        {
            if (!_isDisposed)
                Dispose();

            throw;
        }

        if (_isDisposed)
            return;

        using var thumbnailStream = new MemoryStream(thumbnailData, writable: false);
        var thumbnail = new Bitmap(thumbnailStream);

        if (_isDisposed)
        {
            thumbnail.Dispose();
            return;
        }

        var previousThumbnail = Thumbnail;
        ThumbnailData = thumbnailData;
        Thumbnail = thumbnail;
        DisposeBitmap(previousThumbnail);
    }

    public async Task<byte[]> ReadDataAsync()
    {
        if (SourceData is not null)
            return SourceData;

        await using var stream = await OpenReadAsync();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        return ms.ToArray();
    }

    public async Task<Stream> OpenReadAsync()
    {
        if (SourceData is not null)
            return new MemoryStream(SourceData, writable: false);

        if (SourceFile is not null)
            return await SourceFile.OpenReadAsync();

        throw new InvalidOperationException("Nenhuma origem de imagem foi definida.");
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        var thumbnail = Thumbnail;
        Thumbnail = null;
        ThumbnailData = null;
        DisposeBitmap(thumbnail);

        SourceData = null;
        SourceFile?.Dispose();
        SourceFile = null;
        RemoveRequested = null;
    }

    public event Action<ImageOrderItemViewModel>? RemoveRequested;

    [RelayCommand]
    private void Remove()
    {
        var handler = RemoveRequested;
        if (handler is null)
        {
            Dispose();
            return;
        }

        handler.Invoke(this);
    }

    private static void DisposeBitmap(Bitmap? bitmap)
    {
        bitmap?.Dispose();
    }
}
