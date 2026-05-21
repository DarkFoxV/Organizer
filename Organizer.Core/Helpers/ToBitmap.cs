using System;
using System.IO;
using Avalonia;
using Avalonia.Media.Imaging;

namespace Organizer.Core.Helpers;

public static class ImageHelper
{
    public static Bitmap? ToBitmap(byte[]? data)
    {
        return ToBitmap(data, maxWidth: null, maxHeight: null);
    }

    public static Bitmap? ToBitmap(byte[]? data, int? maxWidth, int? maxHeight)
    {
        if (data is null || data.Length == 0)
            return null;

        using var ms = new MemoryStream(data);
        var bitmap = new Bitmap(ms);

        if (maxWidth is null && maxHeight is null)
            return bitmap;

        var widthRatio = maxWidth is null
            ? double.PositiveInfinity
            : maxWidth.Value / (double)bitmap.PixelSize.Width;

        var heightRatio = maxHeight is null
            ? double.PositiveInfinity
            : maxHeight.Value / (double)bitmap.PixelSize.Height;

        var ratio = Math.Min(1, Math.Min(widthRatio, heightRatio));
        if (ratio >= 0.999)
            return bitmap;

        var scaledSize = new PixelSize(
            Math.Max(1, (int)Math.Round(bitmap.PixelSize.Width * ratio)),
            Math.Max(1, (int)Math.Round(bitmap.PixelSize.Height * ratio)));

        var scaled = bitmap.CreateScaledBitmap(scaledSize, BitmapInterpolationMode.MediumQuality);
        bitmap.Dispose();
        return scaled;
    }
}