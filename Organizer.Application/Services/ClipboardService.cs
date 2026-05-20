using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;

namespace Organizer.Application.Services;

public class ClipboardService : IClipboardService
{
    private static readonly HashSet<string> SupportedImageExtensions =
        new([".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp"], StringComparer.OrdinalIgnoreCase);

    private static readonly (string MimeType, string Extension)[] SupportedClipboardMimeTypes =
    [
        ("image/png", ".png"),
        ("image/jpeg", ".jpg"),
        ("image/gif", ".gif"),
        ("image/bmp", ".bmp"),
        ("image/webp", ".webp")
    ];

    public async Task<IReadOnlyList<ClipboardImageData>> GetImagesAsync(IClipboard clipboard)
    {
        var imagesFromFiles = await TryGetImagesFromFilesAsync(clipboard);
        if (imagesFromFiles.Count > 0)
            return imagesFromFiles;

        var rawImage = await TryGetRawImageAsync(clipboard);
        if (rawImage is not null)
            return [rawImage];

        using var bitmap = await clipboard.TryGetBitmapAsync();
        if (bitmap is null)
            return [];

        var bytes = await SerializeBitmapAsync(bitmap);

        return
        [
            new ClipboardImageData(
                Filename: $"clipboard-{DateTime.Now:yyyyMMdd-HHmmss}.png",
                MimeType: "image/png",
                Data: bytes)
        ];
    }

    public async Task<bool> SetImageAsync(IClipboard clipboard, byte[] imageData, string? mimeType = null)
    {
        if (imageData.Length == 0)
            return false;

        var imageMimeType = NormalizeImageMime(mimeType) ?? DetectMime(imageData);
        if (imageMimeType is null)
            return false;

        var item = new DataTransferItem();
        item.Set(DataFormat.CreateBytesPlatformFormat(imageMimeType), imageData);

        var dataTransfer = new DataTransfer();
        dataTransfer.Add(item);
        await clipboard.SetDataAsync(dataTransfer);
        return true;
    }

    private static async Task<List<ClipboardImageData>> TryGetImagesFromFilesAsync(IClipboard clipboard)
    {
        var files = await clipboard.TryGetFilesAsync();
        if (files is null)
            return [];

        var images = new List<ClipboardImageData>();

        foreach (var file in files.OfType<IStorageFile>())
        {
            if (!IsSupportedImageFile(file.Name))
                continue;

            await using var stream = await file.OpenReadAsync();
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);

            images.Add(new ClipboardImageData(
                Filename: file.Name,
                MimeType: DetectMime(file.Name),
                Data: memoryStream.ToArray()));
        }

        return images;
    }

    private static async Task<ClipboardImageData?> TryGetRawImageAsync(IClipboard clipboard)
    {
        using var dataTransfer = await clipboard.TryGetDataAsync();
        if (dataTransfer is null)
            return null;

        foreach (var (mimeType, extension) in SupportedClipboardMimeTypes)
        {
            var format = DataFormat.CreateBytesPlatformFormat(mimeType);
            var bytes = await dataTransfer.TryGetValueAsync(format);
            if (bytes is null || bytes.Length == 0)
                continue;

            return new ClipboardImageData(
                Filename: $"clipboard-{DateTime.Now:yyyyMMdd-HHmmss}{extension}",
                MimeType: mimeType,
                Data: bytes);
        }

        return null;
    }

    private static Task<byte[]> SerializeBitmapAsync(Bitmap bitmap)
    {
        return Task.Run(() =>
        {
            using var stream = new MemoryStream();
            bitmap.Save(stream);
            return stream.ToArray();
        });
    }

    private static bool IsSupportedImageFile(string filename)
    {
        var extension = Path.GetExtension(filename);
        return !string.IsNullOrWhiteSpace(extension) && SupportedImageExtensions.Contains(extension);
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

    private static string? NormalizeImageMime(string? mimeType)
    {
        if (string.IsNullOrWhiteSpace(mimeType))
            return null;

        var normalizedMimeType = mimeType.Trim().ToLowerInvariant();
        if (normalizedMimeType == "image/jpg")
            return "image/jpeg";

        return SupportedClipboardMimeTypes.Any(format => format.MimeType == normalizedMimeType)
            ? normalizedMimeType
            : null;
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
