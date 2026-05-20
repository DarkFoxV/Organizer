using System;
using Avalonia.Media.Imaging;

namespace Organizer.Application.ViewModels;

public class GroupCopyPickerItemViewModel : IDisposable
{
    public int Id { get; init; }
    public int Index { get; init; }
    public Bitmap? Thumbnail { get; set; }
    public string Filename { get; init; } = string.Empty;
    public string MimeType { get; init; } = "application/octet-stream";
    public string Description { get; init; } = string.Empty;

    public bool HasThumbnail => Thumbnail is not null;

    public string NumberText => (Index + 1).ToString("00");

    public string Caption => string.IsNullOrWhiteSpace(Description)
        ? Filename
        : Description;

    public void Dispose()
    {
        Thumbnail?.Dispose();
        Thumbnail = null;
    }
}
