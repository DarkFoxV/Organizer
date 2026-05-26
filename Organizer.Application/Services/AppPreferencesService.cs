using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;

namespace Organizer.Application.Services;

public sealed class AppPreferencesService
{
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

    public event Action? PreferencesChanged;

    public AppPreferences Current => _preferences;

    public void Update(Action<AppPreferences> update)
    {
        update(_preferences);
        Save();
        ApplyTheme();
        ApplyLanguage();
        PreferencesChanged?.Invoke();
    }

    public string T(string key, params object[] args)
    {
        return Translate(key, args);
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
            return JsonSerializer.Deserialize<AppPreferences>(json) ?? new AppPreferences();
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

        app.RequestedThemeVariant = _preferences.Theme switch
        {
            AppThemePreference.Light => ThemeVariant.Light,
            AppThemePreference.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };

        ApplyPalette(app.Resources, useLightPalette, _preferences.WorkspaceBackground);
    }

    private static void ApplyPalette(
        IResourceDictionary resources,
        bool light,
        WorkspaceBackgroundPreference workspaceBackground)
    {
        if (light)
        {
            SetBrush(resources, "AppBackgroundBrush", "#f4f7fb");
            SetBrush(resources, "SurfaceBrush", "#ffffff");
            SetBrush(resources, "SurfaceAltBrush", "#eef3f8");
            SetBrush(resources, "SurfaceSoftBrush", "#f4ffffff");
            SetBrush(resources, "SurfaceSoftAltBrush", "#dcecf2f8");
            SetBrush(resources, "BorderBrushColor", "#cbd5e1");
            SetBrush(resources, "FloatingBorderBrush", "#c7d4e4");
            SetBrush(resources, "TextPrimaryBrush", "#111827");
            SetBrush(resources, "TextMutedBrush", "#526173");
            SetBrush(resources, "TextSubtleBrush", "#8a97a8");
            SetBrush(resources, "PrimaryBrush", "#2563eb");
            SetBrush(resources, "PrimaryHoverBrush", "#1d4ed8");
            SetBrush(resources, "SuccessBrush", "#16a34a");
            SetBrush(resources, "SuccessHoverBrush", "#15803d");
            SetBrush(resources, "DangerBrush", "#dc2626");
            SetBrush(resources, "DangerHoverBrush", "#b91c1c");
            SetBrush(resources, "DangerSoftBrush", "#fff1f2");
            SetBrush(resources, "DangerSoftTextBrush", "#b91c1c");
            SetBrush(resources, "WarningTextBrush", "#b45309");
            SetBrush(resources, "SuccessTextBrush", "#15803d");
            SetBrush(resources, "AccentSoftBrush", "#dbeafe");
            SetBrush(resources, "AccentSoftTextBrush", "#1d4ed8");
            ApplyWorkspacePalette(resources, workspaceBackground, light);
            SetBrush(resources, "OverlayBrush", "#660f172a");
            SetBrush(resources, "OverlayCardBrush", "#fffafcff");
            return;
        }

        SetBrush(resources, "AppBackgroundBrush", "#0d1117");
        SetBrush(resources, "SurfaceBrush", "#161b22");
        SetBrush(resources, "SurfaceAltBrush", "#1c2230");
        SetBrush(resources, "SurfaceSoftBrush", "#E6121824");
        SetBrush(resources, "SurfaceSoftAltBrush", "#99161d2a");
        SetBrush(resources, "BorderBrushColor", "#2a3347");
        SetBrush(resources, "FloatingBorderBrush", "#3A50677D");
        SetBrush(resources, "TextPrimaryBrush", "#e6edf3");
        SetBrush(resources, "TextMutedBrush", "#8b949e");
        SetBrush(resources, "TextSubtleBrush", "#484f58");
        SetBrush(resources, "PrimaryBrush", "#3b82f6");
        SetBrush(resources, "PrimaryHoverBrush", "#1d4ed8");
        SetBrush(resources, "SuccessBrush", "#16a34a");
        SetBrush(resources, "SuccessHoverBrush", "#15803d");
        SetBrush(resources, "DangerBrush", "#dc2626");
        SetBrush(resources, "DangerHoverBrush", "#b91c1c");
        SetBrush(resources, "DangerSoftBrush", "#2d1515");
        SetBrush(resources, "DangerSoftTextBrush", "#f87171");
        SetBrush(resources, "WarningTextBrush", "#fb923c");
        SetBrush(resources, "SuccessTextBrush", "#4ade80");
        SetBrush(resources, "AccentSoftBrush", "#263b82f6");
        SetBrush(resources, "AccentSoftTextBrush", "#60a5fa");
        ApplyWorkspacePalette(resources, workspaceBackground, light);
        SetBrush(resources, "OverlayBrush", "#99000000");
        SetBrush(resources, "OverlayCardBrush", "#f0121824");
    }

    private static void ApplyWorkspacePalette(
        IResourceDictionary resources,
        WorkspaceBackgroundPreference workspaceBackground,
        bool light)
    {
        if (light)
        {
            switch (workspaceBackground)
            {
                case WorkspaceBackgroundPreference.Neutral:
                    SetBrush(resources, "WorkspaceViewportBrush", "#e5e7eb");
                    SetBrush(resources, "WorkspaceBoardBrush", "#f8fafc");
                    SetBrush(resources, "WorkspaceBoardBorderBrush", "#cbd5e1");
                    return;
                case WorkspaceBackgroundPreference.Black:
                    SetBrush(resources, "WorkspaceViewportBrush", "#171717");
                    SetBrush(resources, "WorkspaceBoardBrush", "#262626");
                    SetBrush(resources, "WorkspaceBoardBorderBrush", "#525252");
                    return;
                default:
                    SetBrush(resources, "WorkspaceViewportBrush", "#dbeafe");
                    SetBrush(resources, "WorkspaceBoardBrush", "#eff6ff");
                    SetBrush(resources, "WorkspaceBoardBorderBrush", "#93c5fd");
                    return;
            }
        }

        switch (workspaceBackground)
        {
            case WorkspaceBackgroundPreference.Neutral:
                SetBrush(resources, "WorkspaceViewportBrush", "#111827");
                SetBrush(resources, "WorkspaceBoardBrush", "#1f2937");
                SetBrush(resources, "WorkspaceBoardBorderBrush", "#374151");
                return;
            case WorkspaceBackgroundPreference.Black:
                SetBrush(resources, "WorkspaceViewportBrush", "#000000");
                SetBrush(resources, "WorkspaceBoardBrush", "#171717");
                SetBrush(resources, "WorkspaceBoardBorderBrush", "#404040");
                return;
            default:
                SetBrush(resources, "WorkspaceViewportBrush", "#06152f");
                SetBrush(resources, "WorkspaceBoardBrush", "#0b2142");
                SetBrush(resources, "WorkspaceBoardBorderBrush", "#1d4ed8");
                return;
        }
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
}

public sealed class AppPreferences
{
    public const int MinWorkspaceHistoryLimit = 0;
    public const int MaxWorkspaceHistoryLimit = 200;
    public const int DefaultWorkspaceHistoryLimit = 100;

    public AppThemePreference Theme { get; set; } = AppThemePreference.System;
    public int SearchItemsPerPage { get; set; } = 20;
    public AppLanguagePreference Language { get; set; } = AppLanguagePreference.PortugueseBrazil;
    public bool ConfirmDeletion { get; set; } = true;
    public WorkspacePastePreference WorkspacePasteMode { get; set; } = WorkspacePastePreference.Pointer;
    public WorkspaceBackgroundPreference WorkspaceBackground { get; set; } = WorkspaceBackgroundPreference.Dark;
    public int WorkspaceDefaultZoomPercent { get; set; } = 100;
    public int WorkspaceHistoryLimit { get; set; } = DefaultWorkspaceHistoryLimit;
}

public enum AppThemePreference
{
    System,
    Dark,
    Light
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
    Neutral,
    Black
}
