using System;
using System.IO;
using Avalonia;
using Avalonia.Media.Imaging;

namespace Organizer.Application.Services;

public static class ImageThumbnailService
{
    private const int ThumbnailWidth = 220;
    private const int ThumbnailHeight = 300;

    public static byte[] CreateThumbnail(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        using var input = new MemoryStream(data);
        return CreateThumbnail(input);
    }

    public static byte[] CreateThumbnail(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (!stream.CanRead)
            throw new ArgumentException("Stream must be readable.", nameof(stream));

        if (stream.CanSeek)
            stream.Position = 0;

        using var full = new Bitmap(stream);
        return CreateThumbnail(full);
    }

    private static byte[] CreateThumbnail(Bitmap full)
    {
        var ratio = Math.Min(
            ThumbnailWidth / (double)full.PixelSize.Width,
            ThumbnailHeight / (double)full.PixelSize.Height);

        var width = Math.Max(1, (int)Math.Round(full.PixelSize.Width * ratio));
        var height = Math.Max(1, (int)Math.Round(full.PixelSize.Height * ratio));

        using var thumb = full.CreateScaledBitmap(
            new PixelSize(width, height));

        using var output = new MemoryStream();

        thumb.Save(output);

        return output.ToArray();
    }
}
