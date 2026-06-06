using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Organize.Organizer.Core.Enums;

namespace Organizer.Application.Services;

public sealed class AppPreferencesService
{
    private readonly object _preferencesLock = new();
    private readonly string _settingsPath;
    private AppPreferences _preferences;

    public AppPreferencesService()
    {
        _settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Organizer",
            "settings.json");

        _preferences = Load();
        ApplyTheme();
        ApplyLanguage();
    }

    public event EventHandler<AppPreferencesChangedEventArgs>? PreferencesChanged;
    public event Action? RecentWorkspacesChanged;

    public AppPreferences Current => _preferences;

    public void Update(Action<AppPreferences> update)
    {
        AppPreferencesChangedEventArgs changes;

        lock (_preferencesLock)
        {
            var previous = AppPreferencesSnapshot.Create(_preferences);
            update(_preferences);
            changes = previous.CompareTo(_preferences);

            if (!changes.HasAnyChange())
                return;

            Save();
        }

        if (changes.ThemeChanged || changes.WorkspaceBackgroundChanged)
            ApplyTheme();

        if (changes.LanguageChanged)
            ApplyLanguage();

        PreferencesChanged?.Invoke(this, changes);
    }

    public void UpdateStoredData(Action<AppPreferences> update)
    {
        lock (_preferencesLock)
        {
            update(_preferences);
            Save();
        }
    }

    public TResult ReadStoredData<TResult>(Func<AppPreferences, TResult> read)
    {
        lock (_preferencesLock)
            return read(_preferences);
    }

    public string T(string key, params object[] args)
    {
        return Translate(key, args);
    }

    public void RememberRecentWorkspace(IStorageFile file)
    {
        var localPath = file.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(localPath))
            return;

        var fullPath = Path.GetFullPath(localPath);
        _preferences.RecentWorkspaces.RemoveAll(workspace =>
            string.Equals(workspace.LocalPath, fullPath, StringComparison.OrdinalIgnoreCase));

        _preferences.RecentWorkspaces.Insert(0, new RecentWorkspacePreference
        {
            Name = string.IsNullOrWhiteSpace(file.Name)
                ? Path.GetFileNameWithoutExtension(fullPath)
                : file.Name,
            LocalPath = fullPath,
            LastUsedAt = DateTimeOffset.Now
        });

        if (_preferences.RecentWorkspaces.Count > AppPreferences.MaxRecentWorkspaces)
        {
            _preferences.RecentWorkspaces.RemoveRange(
                AppPreferences.MaxRecentWorkspaces,
                _preferences.RecentWorkspaces.Count - AppPreferences.MaxRecentWorkspaces);
        }

        Save();
        RecentWorkspacesChanged?.Invoke();
    }

    public void ForgetRecentWorkspace(string localPath)
    {
        if (string.IsNullOrWhiteSpace(localPath))
            return;

        var fullPath = Path.GetFullPath(localPath);
        var removed = _preferences.RecentWorkspaces.RemoveAll(workspace =>
            string.Equals(workspace.LocalPath, fullPath, StringComparison.OrdinalIgnoreCase));

        if (removed == 0)
            return;

        Save();
        RecentWorkspacesChanged?.Invoke();
    }

    public static string Translate(string key, params object[] args)
    {
        var value = Avalonia.Application.Current?.Resources.TryGetResource(
            key,
            null,
            out var resource) == true
            ? resource?.ToString() ?? key
            : key;

        return args.Length == 0
            ? value
            : string.Format(value, args);
    }

    private AppPreferences Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
                return new AppPreferences();

            var json = File.ReadAllText(_settingsPath);
            var preferences = JsonSerializer.Deserialize<AppPreferences>(json) ?? new AppPreferences();
            preferences.RecentWorkspaces ??= [];
            preferences.GoogleDriveTokenStore ??= [];
            return preferences;
        }
        catch
        {
            return new AppPreferences();
        }
    }

    private void Save()
    {
        var directory = Path.GetDirectoryName(_settingsPath);

        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(_preferences, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(_settingsPath, json);
    }

    private void ApplyTheme()
    {
        if (Avalonia.Application.Current is not { } app)
            return;

        var useLightPalette = _preferences.Theme == AppThemePreference.Light;
        var useGrayPalette = _preferences.Theme == AppThemePreference.Gray;

        app.RequestedThemeVariant = _preferences.Theme switch
        {
            AppThemePreference.Light => ThemeVariant.Light,
            AppThemePreference.Dark or AppThemePreference.Gray => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };

        ApplyPalette(app.Resources, useLightPalette, useGrayPalette, _preferences.WorkspaceBackground);
    }

    private static void ApplyPalette(
        IResourceDictionary resources,
        bool light,
        bool gray,
        WorkspaceBackgroundPreference workspaceBackground)
    {
        if (light)
        {
            SetBrush(resources, "AppBackgroundBrush", "#F5F7FA");
            SetBrush(resources, "SurfaceBrush", "#ffffff");
            SetBrush(resources, "SurfaceAltBrush", "#F8FAFC");
            SetBrush(resources, "SurfaceSoftBrush", "#ffffff");
            SetBrush(resources, "SurfaceSoftAltBrush", "#F3F6FA");
            SetBrush(resources, "BorderBrushColor", "#E4E8EF");
            SetBrush(resources, "FloatingBorderBrush", "#E4E8EF");
            SetBrush(resources, "TextPrimaryBrush", "#111827");
            SetBrush(resources, "TextMutedBrush", "#6B7280");
            SetBrush(resources, "TextSubtleBrush", "#9CA3AF");
            SetBrush(resources, "PrimaryBrush", "#2563EB");
            SetBrush(resources, "PrimaryHoverBrush", "#1D4ED8");
            SetBrush(resources, "SuccessBrush", "#16a34a");
            SetBrush(resources, "SuccessHoverBrush", "#15803d");
            SetBrush(resources, "DangerBrush", "#dc2626");
            SetBrush(resources, "DangerHoverBrush", "#b91c1c");
            SetBrush(resources, "DangerSoftBrush", "#fff1f2");
            SetBrush(resources, "DangerSoftTextBrush", "#b91c1c");
            SetBrush(resources, "WarningTextBrush", "#b45309");
            SetBrush(resources, "SuccessTextBrush", "#15803d");
            SetBrush(resources, "AccentSoftBrush", "#EEF4FF");
            SetBrush(resources, "AccentSoftTextBrush", "#2563EB");
            SetImageBadgePalette(resources, light, gray);
            SetTagPalette(resources, light);
            SetSearchPalette(resources, light, gray);
            ApplyWorkspacePalette(resources, workspaceBackground, light, gray);
            SetBrush(resources, "OverlayBrush", "#660f172a");
            SetBrush(resources, "OverlayCardBrush", "#fffafcff");
            return;
        }

        if (gray)
        {
            SetBrush(resources, "AppBackgroundBrush", "#1c1c1e");
            SetBrush(resources, "SurfaceBrush", "#242426");
            SetBrush(resources, "SurfaceAltBrush", "#1c1c1e");
            SetBrush(resources, "SurfaceSoftBrush", "#242426");
            SetBrush(resources, "SurfaceSoftAltBrush", "#2e2e32");
            SetBrush(resources, "BorderBrushColor", "#333336");
            SetBrush(resources, "FloatingBorderBrush", "#333336");
            SetBrush(resources, "TextPrimaryBrush", "#e8e8ea");
            SetBrush(resources, "TextMutedBrush", "#8a8a92");
            SetBrush(resources, "TextSubtleBrush", "#52525a");
            SetBrush(resources, "PrimaryBrush", "#1544a8");
            SetBrush(resources, "PrimaryHoverBrush", "#1a52c8");
            SetBrush(resources, "SuccessBrush", "#2a7a50");
            SetBrush(resources, "SuccessHoverBrush", "#338c5d");
            SetBrush(resources, "DangerBrush", "#8a1820");
            SetBrush(resources, "DangerHoverBrush", "#a61d27");
            SetBrush(resources, "DangerSoftBrush", "#321f22");
            SetBrush(resources, "DangerSoftTextBrush", "#d89098");
            SetBrush(resources, "WarningTextBrush", "#c8a060");
            SetBrush(resources, "SuccessTextBrush", "#8fc69d");
            SetBrush(resources, "AccentSoftBrush", "#2e2e32");
            SetBrush(resources, "AccentSoftTextBrush", "#c8c8ce");
            SetImageBadgePalette(resources, light, gray);
            SetTagPalette(resources, light);
            SetSearchPalette(resources, light, gray);
            ApplyWorkspacePalette(resources, workspaceBackground, light, gray);
            SetBrush(resources, "OverlayBrush", "#99000000");
            SetBrush(resources, "OverlayCardBrush", "#f0242426");
            return;
        }

        SetBrush(resources, "AppBackgroundBrush", "#080b10");
        SetBrush(resources, "SurfaceBrush", "#0d1219");
        SetBrush(resources, "SurfaceAltBrush", "#080b10");
        SetBrush(resources, "SurfaceSoftBrush", "#0d1219");
        SetBrush(resources, "SurfaceSoftAltBrush", "#111822");
        SetBrush(resources, "BorderBrushColor", "#1a2030");
        SetBrush(resources, "FloatingBorderBrush", "#1a2030");
        SetBrush(resources, "TextPrimaryBrush", "#dde3ed");
        SetBrush(resources, "TextMutedBrush", "#7a8ca8");
        SetBrush(resources, "TextSubtleBrush", "#3e4b61");
        SetBrush(resources, "PrimaryBrush", "#1544a8");
        SetBrush(resources, "PrimaryHoverBrush", "#1a52c8");
        SetBrush(resources, "SuccessBrush", "#16a34a");
        SetBrush(resources, "SuccessHoverBrush", "#15803d");
        SetBrush(resources, "DangerBrush", "#dc2626");
        SetBrush(resources, "DangerHoverBrush", "#b91c1c");
        SetBrush(resources, "DangerSoftBrush", "#2d1515");
        SetBrush(resources, "DangerSoftTextBrush", "#f87171");
        SetBrush(resources, "WarningTextBrush", "#fb923c");
        SetBrush(resources, "SuccessTextBrush", "#4ade80");
        SetBrush(resources, "AccentSoftBrush", "#10151e");
        SetBrush(resources, "AccentSoftTextBrush", "#c8d0df");
        SetImageBadgePalette(resources, light, gray);
        SetTagPalette(resources, light);
        SetSearchPalette(resources, light, gray);
        ApplyWorkspacePalette(resources, workspaceBackground, light, gray);
        SetBrush(resources, "OverlayBrush", "#99000000");
        SetBrush(resources, "OverlayCardBrush", "#f0121824");
    }

    private static void SetSearchPalette(IResourceDictionary resources, bool light, bool gray)
    {
        if (light)
        {
            SetBrush(resources, "SearchBackgroundBrush", "#F5F7FA");
            SetBrush(resources, "SearchControlBrush", "#ffffff");
            SetBrush(resources, "SearchControlBorderBrush", "#E4E8EF");
            SetBrush(resources, "SearchMutedBrush", "#6B7280");
            SetBrush(resources, "SearchIconBrush", "#9CA3AF");
            SetBrush(resources, "SearchRegisterBrush", "#2563EB");
            SetBrush(resources, "SearchRegisterHoverBrush", "#1D4ED8");
            SetBrush(resources, "SearchRegisterTextBrush", "#ffffff");
            return;
        }

        if (gray)
        {
            SetBrush(resources, "SearchBackgroundBrush", "#1c1c1e");
            SetBrush(resources, "SearchControlBrush", "#242426");
            SetBrush(resources, "SearchControlBorderBrush", "#333336");
            SetBrush(resources, "SearchMutedBrush", "#8a8a92");
            SetBrush(resources, "SearchIconBrush", "#52525a");
            SetBrush(resources, "SearchRegisterBrush", "#1544a8");
            SetBrush(resources, "SearchRegisterHoverBrush", "#1a52c8");
            SetBrush(resources, "SearchRegisterTextBrush", "#c8dcff");
            return;
        }

        SetBrush(resources, "SearchBackgroundBrush", "#080b10");
        SetBrush(resources, "SearchControlBrush", "#0d1219");
        SetBrush(resources, "SearchControlBorderBrush", "#1a2030");
        SetBrush(resources, "SearchMutedBrush", "#7a8ca8");
        SetBrush(resources, "SearchIconBrush", "#3a4255");
        SetBrush(resources, "SearchRegisterBrush", "#1544a8");
        SetBrush(resources, "SearchRegisterHoverBrush", "#1a52c8");
        SetBrush(resources, "SearchRegisterTextBrush", "#c8dcff");
    }

    private static void SetImageBadgePalette(IResourceDictionary resources, bool light, bool gray)
    {
        if (light)
        {
            SetBrush(resources, "ImageBadgeBackgroundBrush", "#E0FFFFFF");
            SetBrush(resources, "ImageBadgeBorderBrush", "#BFCBD5E1");
            SetBrush(resources, "ImageBadgeTextBrush", "#64748B");
            return;
        }

        if (gray)
        {
            SetBrush(resources, "ImageBadgeBackgroundBrush", "#CC242426");
            SetBrush(resources, "ImageBadgeBorderBrush", "#14FFFFFF");
            SetBrush(resources, "ImageBadgeTextBrush", "#9a9aa2");
            return;
        }

        SetBrush(resources, "ImageBadgeBackgroundBrush", "#B30C121C");
        SetBrush(resources, "ImageBadgeBorderBrush", "#14FFFFFF");
        SetBrush(resources, "ImageBadgeTextBrush", "#dde3ed");
    }

    private static void SetTagPalette(IResourceDictionary resources, bool light)
    {
        foreach (var (color, entry) in TagColorPalette.All)
        {
            var name = Enum.GetName(color) ?? nameof(TagColor.Blue);
            SetBrush(resources, $"Tag{name}SelectedBrush", entry.SelectedBackground);
            SetBrush(resources, $"Tag{name}DimBackgroundBrush", light ? entry.LightDimBackground : entry.DarkDimBackground);
            SetBrush(resources, $"Tag{name}DimForegroundBrush", light ? entry.LightDimForeground : entry.DarkDimForeground);
            SetBrush(resources, $"Tag{name}DimBorderBrush", light ? entry.LightDimBorder : entry.DarkDimBackground);
        }
    }

    private static void ApplyWorkspacePalette(
        IResourceDictionary resources,
        WorkspaceBackgroundPreference workspaceBackground,
        bool light,
        bool gray)
    {
        workspaceBackground = ResolveWorkspaceBackground(workspaceBackground, light, gray);

        switch (workspaceBackground)
        {
            case WorkspaceBackgroundPreference.Light:
                SetBrush(resources, "WorkspaceViewportBrush", "#F5F7FA");
                SetBrush(resources, "WorkspaceBoardBrush", "#FFFFFF");
                SetBrush(resources, "WorkspaceBoardBorderBrush", "#E4E8EF");
                return;
            case WorkspaceBackgroundPreference.Gray:
                SetBrush(resources, "WorkspaceViewportBrush", "#1c1c1e");
                SetBrush(resources, "WorkspaceBoardBrush", "#242426");
                SetBrush(resources, "WorkspaceBoardBorderBrush", "#333336");
                return;
            default:
                SetBrush(resources, "WorkspaceViewportBrush", "#080b10");
                SetBrush(resources, "WorkspaceBoardBrush", "#0d1219");
                SetBrush(resources, "WorkspaceBoardBorderBrush", "#1a2030");
                return;
        }
    }

    public WorkspaceBackgroundPreference ResolveWorkspaceBackground()
    {
        var light = _preferences.Theme == AppThemePreference.Light;
        var gray = _preferences.Theme == AppThemePreference.Gray;
        return ResolveWorkspaceBackground(_preferences.WorkspaceBackground, light, gray);
    }

    private static WorkspaceBackgroundPreference ResolveWorkspaceBackground(
        WorkspaceBackgroundPreference workspaceBackground,
        bool light,
        bool gray)
    {
        if (workspaceBackground != WorkspaceBackgroundPreference.Theme)
            return workspaceBackground;

        if (light)
            return WorkspaceBackgroundPreference.Light;

        if (gray)
            return WorkspaceBackgroundPreference.Gray;

        return WorkspaceBackgroundPreference.Dark;
    }

    private static void SetBrush(IResourceDictionary resources, string key, string color)
    {
        resources[key] = new SolidColorBrush(Color.Parse(color));
    }

    private void ApplyLanguage()
    {
        if (Avalonia.Application.Current is not { } app)
            return;

        var locale = _preferences.Language == AppLanguagePreference.English
            ? "en-US"
            : "pt-BR";

        foreach (var (key, value) in LoadLocale(locale))
            app.Resources[key] = value;
    }

    private static Dictionary<string, string> LoadLocale(string locale)
    {
        var uri = new Uri($"avares://Organizer/Organizer.Application/Assets/Locales/{locale}.json");

        using var stream = AssetLoader.Open(uri);
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();

        return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];
    }

    private sealed record AppPreferencesSnapshot(
        AppThemePreference Theme,
        int SearchItemsPerPage,
        AppLanguagePreference Language,
        bool ConfirmDeletion,
        WorkspacePastePreference WorkspacePasteMode,
        WorkspaceBackgroundPreference WorkspaceBackground,
        HomeWorkspaceViewPreference HomeWorkspaceViewMode,
        int WorkspaceDefaultZoomPercent,
        int WorkspaceHistoryLimit,
        DateTimeOffset? LastLocalBackupAt,
        DateTimeOffset? LastCloudBackupAt,
        string? GoogleDriveClientId,
        string? GoogleDriveClientSecret)
    {
        public static AppPreferencesSnapshot Create(AppPreferences preferences) =>
            new(
                preferences.Theme,
                preferences.SearchItemsPerPage,
                preferences.Language,
                preferences.ConfirmDeletion,
                preferences.WorkspacePasteMode,
                preferences.WorkspaceBackground,
                preferences.HomeWorkspaceViewMode,
                preferences.WorkspaceDefaultZoomPercent,
                preferences.WorkspaceHistoryLimit,
                preferences.LastLocalBackupAt,
                preferences.LastCloudBackupAt,
                preferences.GoogleDriveClientId,
                preferences.GoogleDriveClientSecret);

        public AppPreferencesChangedEventArgs CompareTo(AppPreferences current) =>
            new()
            {
                ThemeChanged = Theme != current.Theme,
                SearchItemsPerPageChanged = SearchItemsPerPage != current.SearchItemsPerPage,
                LanguageChanged = Language != current.Language,
                ConfirmDeletionChanged = ConfirmDeletion != current.ConfirmDeletion,
                WorkspacePasteModeChanged = WorkspacePasteMode != current.WorkspacePasteMode,
                WorkspaceBackgroundChanged = WorkspaceBackground != current.WorkspaceBackground,
                HomeWorkspaceViewModeChanged = HomeWorkspaceViewMode != current.HomeWorkspaceViewMode,
                WorkspaceDefaultZoomChanged =
                    WorkspaceDefaultZoomPercent != current.WorkspaceDefaultZoomPercent,
                WorkspaceHistoryLimitChanged = WorkspaceHistoryLimit != current.WorkspaceHistoryLimit,
                BackupPreferencesChanged =
                    LastLocalBackupAt != current.LastLocalBackupAt ||
                    LastCloudBackupAt != current.LastCloudBackupAt ||
                    !string.Equals(GoogleDriveClientId, current.GoogleDriveClientId, StringComparison.Ordinal) ||
                    !string.Equals(GoogleDriveClientSecret, current.GoogleDriveClientSecret, StringComparison.Ordinal)
            };
    }
}

public sealed class AppPreferences
{
    public const int MinWorkspaceHistoryLimit = 0;
    public const int MaxWorkspaceHistoryLimit = 200;
    public const int DefaultWorkspaceHistoryLimit = 100;
    public const int MaxRecentWorkspaces = 5;

    public AppThemePreference Theme { get; set; } = AppThemePreference.System;
    public int SearchItemsPerPage { get; set; } = 20;
    public AppLanguagePreference Language { get; set; } = AppLanguagePreference.PortugueseBrazil;
    public bool ConfirmDeletion { get; set; } = true;
    public WorkspacePastePreference WorkspacePasteMode { get; set; } = WorkspacePastePreference.Pointer;
    public WorkspaceBackgroundPreference WorkspaceBackground { get; set; } = WorkspaceBackgroundPreference.Dark;
    public HomeWorkspaceViewPreference HomeWorkspaceViewMode { get; set; } = HomeWorkspaceViewPreference.Grid;
    public int WorkspaceDefaultZoomPercent { get; set; } = 100;
    public int WorkspaceHistoryLimit { get; set; } = DefaultWorkspaceHistoryLimit;
    public DateTimeOffset? LastLocalBackupAt { get; set; }
    public DateTimeOffset? LastCloudBackupAt { get; set; }
    public string? GoogleDriveClientId { get; set; }
    public string? GoogleDriveClientSecret { get; set; }
    public string? GoogleDriveRefreshToken { get; set; }
    public Dictionary<string, string> GoogleDriveTokenStore { get; set; } = [];
    public List<RecentWorkspacePreference> RecentWorkspaces { get; set; } = [];
}

public sealed class RecentWorkspacePreference
{
    public string Name { get; set; } = string.Empty;
    public string LocalPath { get; set; } = string.Empty;
    public DateTimeOffset? LastUsedAt { get; set; }
}

public enum AppThemePreference
{
    System,
    Dark,
    Light,
    Gray
}

public enum AppLanguagePreference
{
    PortugueseBrazil,
    English
}

public enum WorkspacePastePreference
{
    Pointer,
    Center,
    Cascade
}

public enum WorkspaceBackgroundPreference
{
    Dark,
    Gray,
    Light,
    Theme
}

public enum HomeWorkspaceViewPreference
{
    Grid,
    List
}
