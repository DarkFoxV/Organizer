using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Organizer.Application.ViewModels.Components;

public partial class ImageOrderItemViewModel : ObservableObject, IDisposable
{
    private bool _isDisposed;

    [ObservableProperty] private bool _isDragging;

    [ObservableProperty] private bool _isDropTarget;

    [ObservableProperty] private Bitmap? _thumbnail;

    public string Filename { get; }

    public string MimeType { get; }

    public IStorageFile? SourceFile { get; private set; }

    public byte[]? SourceData { get; private set; }

    public byte[]? ThumbnailData { get; private set; }

    public bool HasFileSource => SourceFile is not null;

    public ImageOrderItemViewModel(
        string filename,
        string mimeType,
        IStorageFile? sourceFile,
        byte[]? sourceData,
        Bitmap thumbnail,
        byte[] thumbnailData)
    {
        if ((sourceFile is null) == (sourceData is null))
            throw new ArgumentException("Exactly one image source must be provided.");

        Filename = filename;
        MimeType = mimeType;
        SourceFile = sourceFile;
        SourceData = sourceData;
        Thumbnail = thumbnail;
        ThumbnailData = thumbnailData;
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
