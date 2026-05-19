using System.IO;
using Avalonia.Media.Imaging;

namespace Organizer.Core.Helpers;

public static class ImageHelper
{
    public static Bitmap? ToBitmap(byte[]? data)
    {
        if (data is null || data.Length == 0)
            return null;

        using var ms = new MemoryStream(data);

        return new Bitmap(ms);
    }
}