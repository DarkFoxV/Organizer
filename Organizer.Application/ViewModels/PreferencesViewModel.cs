using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Organizer.Application.Services;

namespace Organizer.Application.ViewModels;

public partial class PreferencesViewModel : ObservableObject, IDisposable
{
    private readonly AppPreferencesService _preferencesService;
    private bool _isRefreshingOptions;

    public ObservableCollection<PreferenceOption<AppThemePreference>> ThemeOptions { get; } =
        new();

    public ObservableCollection<PreferenceOption<int>> ItemsPerPageOptions { get; } =
        new();

    public ObservableCollection<PreferenceOption<AppLanguagePreference>> LanguageOptions { get; } =
        new();

    public ObservableCollection<PreferenceOption<WorkspacePastePreference>> WorkspacePasteOptions { get; } =
        new();

    public ObservableCollection<PreferenceOption<WorkspaceBackgroundPreference>> WorkspaceBackgroundOptions { get; } =
        new();

    [ObservableProperty] private PreferenceOption<AppThemePreference>? _selectedTheme;
    [ObservableProperty] private PreferenceOption<int>? _selectedItemsPerPage;
    [ObservableProperty] private PreferenceOption<AppLanguagePreference>? _selectedLanguage;
    [ObservableProperty] private bool _confirmDeletion;
    [ObservableProperty] private PreferenceOption<WorkspacePastePreference>? _selectedWorkspacePasteMode;
    [ObservableProperty] private PreferenceOption<WorkspaceBackgroundPreference>? _selectedWorkspaceBackground;
    [ObservableProperty] private double _workspaceDefaultZoomPercent;
    [ObservableProperty] private double _workspaceHistoryLimit;

    public PreferencesViewModel(AppPreferencesService preferencesService)
    {
        _preferencesService = preferencesService;

        RefreshOptions();
        _preferencesService.PreferencesChanged += RefreshOptions;
    }

    partial void OnSelectedThemeChanged(PreferenceOption<AppThemePreference>? value)
    {
        if (!_isRefreshingOptions && value is not null)
            _preferencesService.Update(preferences => preferences.Theme = value.Value);
    }

    partial void OnSelectedItemsPerPageChanged(PreferenceOption<int>? value)
    {
        if (!_isRefreshingOptions && value is not null)
            _preferencesService.Update(preferences => preferences.SearchItemsPerPage = value.Value);
    }

    partial void OnSelectedLanguageChanged(PreferenceOption<AppLanguagePreference>? value)
    {
        if (!_isRefreshingOptions && value is not null)
            _preferencesService.Update(preferences => preferences.Language = value.Value);
    }

    partial void OnConfirmDeletionChanged(bool value)
    {
        if (!_isRefreshingOptions)
            _preferencesService.Update(preferences => preferences.ConfirmDeletion = value);
    }

    partial void OnSelectedWorkspacePasteModeChanged(PreferenceOption<WorkspacePastePreference>? value)
    {
        if (!_isRefreshingOptions && value is not null)
            _preferencesService.Update(preferences => preferences.WorkspacePasteMode = value.Value);
    }

    partial void OnSelectedWorkspaceBackgroundChanged(PreferenceOption<WorkspaceBackgroundPreference>? value)
    {
        if (!_isRefreshingOptions && value is not null)
            _preferencesService.Update(preferences => preferences.WorkspaceBackground = value.Value);
    }

    partial void OnWorkspaceDefaultZoomPercentChanged(double value)
    {
        if (!_isRefreshingOptions)
            _preferencesService.Update(preferences => preferences.WorkspaceDefaultZoomPercent = (int)value);
    }

    partial void OnWorkspaceHistoryLimitChanged(double value)
    {
        if (!_isRefreshingOptions)
        {
            _preferencesService.Update(preferences =>
                preferences.WorkspaceHistoryLimit = Math.Clamp(
                    (int)value,
                    AppPreferences.MinWorkspaceHistoryLimit,
                    AppPreferences.MaxWorkspaceHistoryLimit));
        }
    }

    public void Dispose()
    {
        _preferencesService.PreferencesChanged -= RefreshOptions;
    }

    private void RefreshOptions()
    {
        _isRefreshingOptions = true;

        ThemeOptions.Clear();
        ThemeOptions.Add(new(_preferencesService.T("Loc.Preferences.Theme.System"), AppThemePreference.System));
        ThemeOptions.Add(new(_preferencesService.T("Loc.Preferences.Theme.Dark"), AppThemePreference.Dark));
        ThemeOptions.Add(new(_preferencesService.T("Loc.Preferences.Theme.Light"), AppThemePreference.Light));

        ItemsPerPageOptions.Clear();
        foreach (var count in new[] { 10, 20, 30, 50, 100 })
            ItemsPerPageOptions.Add(new(_preferencesService.T("Loc.Preferences.Items.Count", count), count));

        LanguageOptions.Clear();
        LanguageOptions.Add(new(_preferencesService.T("Loc.Preferences.Language.PtBr"), AppLanguagePreference.PortugueseBrazil));
        LanguageOptions.Add(new(_preferencesService.T("Loc.Preferences.Language.En"), AppLanguagePreference.English));

        WorkspacePasteOptions.Clear();
        WorkspacePasteOptions.Add(new(_preferencesService.T("Loc.Preferences.Paste.Pointer"), WorkspacePastePreference.Pointer));
        WorkspacePasteOptions.Add(new(_preferencesService.T("Loc.Preferences.Paste.Center"), WorkspacePastePreference.Center));
        WorkspacePasteOptions.Add(new(_preferencesService.T("Loc.Preferences.Paste.Cascade"), WorkspacePastePreference.Cascade));

        WorkspaceBackgroundOptions.Clear();
        WorkspaceBackgroundOptions.Add(new(_preferencesService.T("Loc.Preferences.Background.Dark"), WorkspaceBackgroundPreference.Dark));
        WorkspaceBackgroundOptions.Add(new(_preferencesService.T("Loc.Preferences.Background.Neutral"), WorkspaceBackgroundPreference.Neutral));
        WorkspaceBackgroundOptions.Add(new(_preferencesService.T("Loc.Preferences.Background.Black"), WorkspaceBackgroundPreference.Black));

        var preferences = _preferencesService.Current;
        SelectedTheme = FindOption(ThemeOptions, preferences.Theme);
        SelectedItemsPerPage = FindOption(ItemsPerPageOptions, preferences.SearchItemsPerPage);
        SelectedLanguage = FindOption(LanguageOptions, preferences.Language);
        ConfirmDeletion = preferences.ConfirmDeletion;
        SelectedWorkspacePasteMode = FindOption(WorkspacePasteOptions, preferences.WorkspacePasteMode);
        SelectedWorkspaceBackground = FindOption(WorkspaceBackgroundOptions, preferences.WorkspaceBackground);
        WorkspaceDefaultZoomPercent = preferences.WorkspaceDefaultZoomPercent;
        WorkspaceHistoryLimit = Math.Clamp(
            preferences.WorkspaceHistoryLimit,
            AppPreferences.MinWorkspaceHistoryLimit,
            AppPreferences.MaxWorkspaceHistoryLimit);

        _isRefreshingOptions = false;
    }

    private static PreferenceOption<T>? FindOption<T>(ObservableCollection<PreferenceOption<T>> options, T value)
    {
        foreach (var option in options)
        {
            if (Equals(option.Value, value))
                return option;
        }

        return options.Count == 0 ? null : options[0];
    }
}

public sealed record PreferenceOption<T>(string Label, T Value)
{
    public override string ToString() => Label;
}
