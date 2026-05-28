using System;
using System.Globalization;
using System.IO;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Organizer.Application.Services;

namespace Organizer.Application.ViewModels;

public sealed partial class HomeWorkspaceItemViewModel : ObservableObject, IDisposable
{
    public HomeWorkspaceItemViewModel(HomeWorkspaceCacheEntry entry, string thumbnailPath)
    {
        Path = entry.Path;
        Name = string.IsNullOrWhiteSpace(entry.Name)
            ? System.IO.Path.GetFileNameWithoutExtension(entry.Path)
            : entry.Name;
        ImageCount = entry.ImageCount;
        LastOpenedAt = entry.LastOpenedAt;
        IsMissing = entry.IsMissing;
        Thumbnail = LoadThumbnail(thumbnailPath);
    }

    public string Path { get; }
    public string Name { get; }
    public int ImageCount { get; }
    public DateTimeOffset? LastOpenedAt { get; }
    public bool IsMissing { get; }

    [ObservableProperty] private Bitmap? _thumbnail;

    public bool HasThumbnail => Thumbnail is not null;
    public bool HasNoThumbnail => Thumbnail is null;
    public string ImageCountText => ImageCount == 1
        ? AppPreferencesService.Translate("Loc.Home.ImageCountOne")
        : AppPreferencesService.Translate("Loc.Home.ImageCountMany", ImageCount);
    public string LastOpenedText => LastOpenedAt is null
        ? AppPreferencesService.Translate("Loc.Home.NeverOpened")
        : FormatRelativeTime(LastOpenedAt.Value);
    public string StatusText => IsMissing
        ? AppPreferencesService.Translate("Loc.Home.FileNotFound")
        : AppPreferencesService.Translate("Loc.Home.WorkspaceStatus", ImageCountText, LastOpenedText);

    public void Dispose()
    {
        var thumbnail = Thumbnail;
        Thumbnail = null;
        thumbnail?.Dispose();
    }

    private static Bitmap? LoadThumbnail(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;

            var data = File.ReadAllBytes(path);
            using var stream = new MemoryStream(data, writable: false);
            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }

    private static string FormatRelativeTime(DateTimeOffset value)
    {
        var elapsed = DateTimeOffset.Now - value.ToLocalTime();

        if (elapsed.TotalMinutes < 1)
            return AppPreferencesService.Translate("Loc.Home.OpenedNow");

        if (elapsed.TotalHours < 1)
            return AppPreferencesService.Translate("Loc.Home.OpenedMinutesAgo", (int)elapsed.TotalMinutes);

        if (elapsed.TotalDays < 1)
            return AppPreferencesService.Translate("Loc.Home.OpenedHoursAgo", (int)elapsed.TotalHours);

        if (elapsed.TotalDays < 2)
            return AppPreferencesService.Translate("Loc.Home.OpenedYesterday");

        if (elapsed.TotalDays < 7)
            return AppPreferencesService.Translate("Loc.Home.OpenedDaysAgo", (int)elapsed.TotalDays);

        return value.ToLocalTime().ToString("d", CultureInfo.CurrentCulture);
    }
}
