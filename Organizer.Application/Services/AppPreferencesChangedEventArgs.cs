using System;

namespace Organizer.Application.Services;

public sealed class AppPreferencesChangedEventArgs : EventArgs
{
    public bool LanguageChanged { get; init; }
    public bool ThemeChanged { get; init; }
    public bool SearchItemsPerPageChanged { get; init; }
    public bool ConfirmDeletionChanged { get; init; }
    public bool WorkspacePasteModeChanged { get; init; }
    public bool WorkspaceBackgroundChanged { get; init; }
    public bool HomeWorkspaceViewModeChanged { get; init; }
    public bool WorkspaceDefaultZoomChanged { get; init; }
    public bool WorkspaceHistoryLimitChanged { get; init; }
    public bool BackupPreferencesChanged { get; init; }

    public bool HasAnyChange() =>
        LanguageChanged ||
        ThemeChanged ||
        SearchItemsPerPageChanged ||
        ConfirmDeletionChanged ||
        WorkspacePasteModeChanged ||
        WorkspaceBackgroundChanged ||
        HomeWorkspaceViewModeChanged ||
        WorkspaceDefaultZoomChanged ||
        WorkspaceHistoryLimitChanged ||
        BackupPreferencesChanged;
}
