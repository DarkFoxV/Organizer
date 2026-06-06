using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Organizer.Application.ViewModels.Components;

namespace Organizer.Application.Services;

public sealed class RegisterImageItemFactory
{
    public async Task<ImageOrderItemViewModel> CreateAsync(IStorageFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        Bitmap? thumbnail = null;

        try
        {
            await using var sourceStream = await file.OpenReadAsync();
            var thumbnailData = await Task.Run(() =>
                ImageThumbnailService.CreateThumbnail(sourceStream));

            thumbnail = CreateBitmap(thumbnailData);

            var item = new ImageOrderItemViewModel(
                filename: file.Name,
                mimeType: DetectMime(file.Name),
                sourceFile: file,
                sourceData: null,
                thumbnail: thumbnail,
                thumbnailData: thumbnailData);

            thumbnail = null;
            return item;
        }
        catch
        {
            thumbnail?.Dispose();
            file.Dispose();
            throw;
        }
    }

    public async Task<ImageOrderItemViewModel> CreateAsync(
        string filename,
        string? mimeType,
        byte[] data)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filename);
        ArgumentNullException.ThrowIfNull(data);

        Bitmap? thumbnail = null;

        try
        {
            var thumbnailData = await Task.Run(() =>
                ImageThumbnailService.CreateThumbnail(data));

            thumbnail = CreateBitmap(thumbnailData);

            var item = new ImageOrderItemViewModel(
                filename: filename,
                mimeType: DetectMime(data) ?? NormalizeMime(mimeType) ?? DetectMime(filename),
                sourceFile: null,
                sourceData: data,
                thumbnail: thumbnail,
                thumbnailData: thumbnailData);

            thumbnail = null;
            return item;
        }
        catch
        {
            thumbnail?.Dispose();
            throw;
        }
    }

    private static Bitmap CreateBitmap(byte[] data)
    {
        using var stream = new MemoryStream(data, writable: false);
        return new Bitmap(stream);
    }

    private static string DetectMime(string filename) =>
        Path.GetExtension(filename).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            _ => "application/octet-stream"
        };

    private static string? NormalizeMime(string? mimeType)
    {
        if (string.IsNullOrWhiteSpace(mimeType))
            return null;

        var normalized = mimeType.Trim().ToLowerInvariant();
        return normalized == "image/jpg" ? "image/jpeg" : normalized;
    }

    private static string? DetectMime(byte[] data)
    {
        if (data.Length >= 8
            && data[0] == 0x89
            && data[1] == 0x50
            && data[2] == 0x4E
            && data[3] == 0x47
            && data[4] == 0x0D
            && data[5] == 0x0A
            && data[6] == 0x1A
            && data[7] == 0x0A)
        {
            return "image/png";
        }

        if (data.Length >= 3
            && data[0] == 0xFF
            && data[1] == 0xD8
            && data[2] == 0xFF)
        {
            return "image/jpeg";
        }

        if (data.Length >= 6
            && data[0] == 0x47
            && data[1] == 0x49
            && data[2] == 0x46
            && data[3] == 0x38
            && (data[4] == 0x37 || data[4] == 0x39)
            && data[5] == 0x61)
        {
            return "image/gif";
        }

        if (data.Length >= 2 && data[0] == 0x42 && data[1] == 0x4D)
            return "image/bmp";

        if (data.Length >= 12
            && data[0] == 0x52
            && data[1] == 0x49
            && data[2] == 0x46
            && data[3] == 0x46
            && data[8] == 0x57
            && data[9] == 0x45
            && data[10] == 0x42
            && data[11] == 0x50)
        {
            return "image/webp";
        }

        return null;
    }
}
