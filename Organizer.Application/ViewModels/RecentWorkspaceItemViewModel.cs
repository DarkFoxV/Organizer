using System;
using System.Globalization;

namespace Organizer.Application.ViewModels;

public sealed class RecentWorkspaceItemViewModel
{
    public RecentWorkspaceItemViewModel(string name, string localPath, DateTimeOffset? lastUsedAt)
    {
        Name = name;
        LocalPath = localPath;
        LastUsedAt = lastUsedAt;
        LastUsedText = lastUsedAt?.ToLocalTime().ToString("g", CultureInfo.CurrentCulture) ?? string.Empty;
    }

    public string Name { get; }
    public string LocalPath { get; }
    public DateTimeOffset? LastUsedAt { get; }
    public string LastUsedText { get; }
    public bool HasLastUsedText => !string.IsNullOrWhiteSpace(LastUsedText);
}
