using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Organizer.Application.ViewModels.Components;

public partial class ImageOrderItemViewModel : ObservableObject, IDisposable
{
    [ObservableProperty] private bool _isDragging;

    [ObservableProperty] private bool _isDropTarget;

    [ObservableProperty] private Bitmap? _thumbnail;

    public string Filename { get; init; } = string.Empty;

    public string MimeType { get; init; } = "application/octet-stream";

    public IStorageFile? SourceFile { get; init; }

    public byte[]? SourceData { get; init; }

    public async Task LoadThumbnailAsync()
    {
        await using var sourceStream = await OpenReadAsync();
        using var full = new Bitmap(sourceStream);

        var ratio = Math.Min(
            80.0 / full.PixelSize.Width,
            80.0 / full.PixelSize.Height);

        var width = (int)(full.PixelSize.Width * ratio);
        var height = (int)(full.PixelSize.Height * ratio);

        Thumbnail = await Task.Run(() =>
        {
            return full.CreateScaledBitmap(
                new PixelSize(width, height));
        });
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

    private async Task<Stream> OpenReadAsync()
    {
        if (SourceData is not null)
            return new MemoryStream(SourceData, writable: false);

        if (SourceFile is not null)
            return await SourceFile.OpenReadAsync();

        throw new InvalidOperationException("Nenhuma origem de imagem foi definida.");
    }

    public void Dispose()
    {
        Thumbnail?.Dispose();
        Thumbnail = null;
    }

    public event Action<ImageOrderItemViewModel>? RemoveRequested;

    [RelayCommand]
    private void Remove()
    {
        Dispose();

        RemoveRequested?.Invoke(this);
    }
}