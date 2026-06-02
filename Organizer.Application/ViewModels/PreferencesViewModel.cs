using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Organizer.Application.Services;

namespace Organizer.Application.ViewModels;

public partial class PreferencesViewModel : ObservableObject, IDisposable
{
    private readonly AppPreferencesService _preferencesService;
    private readonly BackupService _backupService;
    private readonly GoogleDriveOAuthService _googleDriveOAuthService;
    private readonly GoogleDriveBackupStorageProvider _googleDriveBackupStorageProvider;
    private readonly IToastService _toastService;
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
    [ObservableProperty] private PreferencesSection _selectedSection = PreferencesSection.General;
    [ObservableProperty] private string _databaseLocation = string.Empty;
    [ObservableProperty] private string _databaseSize = "0 B";
    [ObservableProperty] private string _lastLocalBackup = string.Empty;
    [ObservableProperty] private string _lastCloudBackup = string.Empty;
    [ObservableProperty] private string _googleDriveClientId = string.Empty;
    [ObservableProperty] private string _googleDriveClientSecret = string.Empty;
    [ObservableProperty] private bool _isBusy;

    public bool IsGeneralSectionVisible => SelectedSection == PreferencesSection.General;
    public bool IsDataBackupSectionVisible => SelectedSection == PreferencesSection.DataBackup;
    public bool IsAboutSectionVisible => SelectedSection == PreferencesSection.About;
    public bool IsNotBusy => !IsBusy;

    public bool IsGeneralSelected => SelectedSection == PreferencesSection.General;
    public bool IsDataBackupSelected => SelectedSection == PreferencesSection.DataBackup;
    public bool IsAboutSelected => SelectedSection == PreferencesSection.About;

    public string CloudProviderName => "Google Drive";
    public string CloudProviderStatus => _googleDriveOAuthService.IsConnected
        ? _preferencesService.T("Loc.Backup.CloudStatusConnected")
        : _preferencesService.T("Loc.Backup.CloudStatusNotConnected");

    public PreferencesViewModel(
        AppPreferencesService preferencesService,
        BackupService backupService,
        GoogleDriveOAuthService googleDriveOAuthService,
        GoogleDriveBackupStorageProvider googleDriveBackupStorageProvider,
        IToastService toastService)
    {
        _preferencesService = preferencesService;
        _backupService = backupService;
        _googleDriveOAuthService = googleDriveOAuthService;
        _googleDriveBackupStorageProvider = googleDriveBackupStorageProvider;
        _toastService = toastService;

        RefreshOptions();
        RefreshDatabaseInfo();
        _preferencesService.PreferencesChanged += OnPreferencesChanged;
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

    partial void OnGoogleDriveClientIdChanged(string value)
    {
        if (_isRefreshingOptions)
            return;

        _preferencesService.Update(preferences => preferences.GoogleDriveClientId = NormalizeOptionalValue(value));
        OnPropertyChanged(nameof(CloudProviderStatus));
    }

    partial void OnGoogleDriveClientSecretChanged(string value)
    {
        if (_isRefreshingOptions)
            return;

        _preferencesService.Update(preferences => preferences.GoogleDriveClientSecret = NormalizeOptionalValue(value));
    }

    partial void OnSelectedSectionChanged(PreferencesSection value)
    {
        OnPropertyChanged(nameof(IsGeneralSectionVisible));
        OnPropertyChanged(nameof(IsDataBackupSectionVisible));
        OnPropertyChanged(nameof(IsAboutSectionVisible));
        OnPropertyChanged(nameof(IsGeneralSelected));
        OnPropertyChanged(nameof(IsDataBackupSelected));
        OnPropertyChanged(nameof(IsAboutSelected));

        if (value == PreferencesSection.DataBackup)
            RefreshDatabaseInfo();
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotBusy));
    }

    public void SelectGeneral()
    {
        SelectedSection = PreferencesSection.General;
    }

    public void SelectDataBackup()
    {
        SelectedSection = PreferencesSection.DataBackup;
    }

    public void SelectAbout()
    {
        SelectedSection = PreferencesSection.About;
    }

    public async Task CreateLocalBackupAsync(string destinationPath)
    {
        await RunBackupActionAsync(
            async () =>
            {
                await _backupService.CreateLocalBackupAsync(destinationPath);
                RefreshDatabaseInfo();
                _toastService.Success(
                    _preferencesService.T("Loc.Backup.ToastBackupCreatedTitle"),
                    _preferencesService.T("Loc.Backup.ToastBackupCreatedMessage"));
            },
            _preferencesService.T("Loc.Backup.ToastBackupFailedTitle"));
    }

    public async Task ExportDatabaseAsync(string destinationPath)
    {
        await RunBackupActionAsync(
            async () =>
            {
                await _backupService.ExportDatabaseAsync(destinationPath);
                _toastService.Success(
                    _preferencesService.T("Loc.Backup.ToastDatabaseExportedTitle"),
                    _preferencesService.T("Loc.Backup.ToastDatabaseExportedMessage"));
            },
            _preferencesService.T("Loc.Backup.ToastExportFailedTitle"));
    }

    public async Task<bool> ValidateBackupForRestoreAsync(string backupPath)
    {
        return await RunValidationAsync(
            () => _backupService.ValidateBackupAsync(backupPath),
            _preferencesService.T("Loc.Backup.ToastInvalidBackupTitle"));
    }

    public async Task RestoreFromBackupAsync(string backupPath)
    {
        await RunBackupActionAsync(
            async () =>
            {
                await _backupService.RestoreFromBackupAsync(backupPath);
                RefreshDatabaseInfo();
                _toastService.Success(
                    _preferencesService.T("Loc.Backup.ToastBackupRestoredTitle"),
                    _preferencesService.T("Loc.Backup.ToastRestartMessage"));
            },
            _preferencesService.T("Loc.Backup.ToastRestoreFailedTitle"));
    }

    public async Task<bool> ValidateDatabaseForImportAsync(string databasePath)
    {
        return await RunValidationAsync(
            () => _backupService.ValidateDatabaseAsync(databasePath),
            _preferencesService.T("Loc.Backup.ToastInvalidDatabaseTitle"));
    }

    public async Task ImportDatabaseAsync(string databasePath)
    {
        await RunBackupActionAsync(
            async () =>
            {
                await _backupService.ImportDatabaseAsync(databasePath);
                RefreshDatabaseInfo();
                _toastService.Success(
                    _preferencesService.T("Loc.Backup.ToastDatabaseImportedTitle"),
                    _preferencesService.T("Loc.Backup.ToastRestartMessage"));
            },
            _preferencesService.T("Loc.Backup.ToastImportFailedTitle"));
    }

    public void ShowCloudPlaceholder()
    {
        _toastService.Info(
            _preferencesService.T("Loc.Backup.ToastGoogleDriveTitle"),
            _preferencesService.T("Loc.Backup.ToastGoogleDriveMessage"));
    }

    public async Task ConnectGoogleDriveAsync()
    {
        await RunBackupActionAsync(
            async () =>
            {
                await _googleDriveOAuthService.ConnectAsync();
                OnPropertyChanged(nameof(CloudProviderStatus));
                _toastService.Success(
                    _preferencesService.T("Loc.Backup.ToastGoogleDriveConnectedTitle"),
                    _preferencesService.T("Loc.Backup.ToastGoogleDriveConnectedMessage"));
            },
            _preferencesService.T("Loc.Backup.ToastGoogleDriveConnectFailedTitle"));
    }

    public async Task BackupToGoogleDriveAsync(CancellationToken cancellationToken = default)
    {
        await RunBackupActionAsync(
            async () =>
            {
                EnsureGoogleDriveConnected();

                var backupPath = Path.Combine(
                    Path.GetTempPath(),
                    $"organizer-backup-{DateTimeOffset.Now:yyyy-MM-dd-HHmmss}.obak");

                try
                {
                    await _backupService.CreateLocalBackupAsync(
                        backupPath,
                        recordLocalBackup: false,
                        cancellationToken);
                    await _googleDriveBackupStorageProvider.UploadBackupAsync(backupPath, cancellationToken);

                    _preferencesService.Update(preferences => preferences.LastCloudBackupAt = DateTimeOffset.UtcNow);
                    RefreshDatabaseInfo();
                    _toastService.Success(
                        _preferencesService.T("Loc.Backup.ToastCloudBackupCreatedTitle"),
                        _preferencesService.T("Loc.Backup.ToastCloudBackupCreatedMessage"));
                }
                finally
                {
                    DeleteIfExists(backupPath);
                }
            },
            _preferencesService.T("Loc.Backup.ToastCloudBackupFailedTitle"));
    }

    public async Task RestoreLatestFromGoogleDriveAsync(CancellationToken cancellationToken = default)
    {
        await RunBackupActionAsync(
            async () =>
            {
                EnsureGoogleDriveConnected();

                var backupPath = Path.Combine(
                    Path.GetTempPath(),
                    $"organizer-cloud-restore-{Guid.NewGuid():N}.obak");

                try
                {
                    await _googleDriveBackupStorageProvider.DownloadLatestBackupAsync(backupPath, cancellationToken);
                    await _backupService.RestoreFromBackupAsync(backupPath, cancellationToken);
                    RefreshDatabaseInfo();
                    _toastService.Success(
                        _preferencesService.T("Loc.Backup.ToastBackupRestoredTitle"),
                        _preferencesService.T("Loc.Backup.ToastRestartMessage"));
                }
                finally
                {
                    DeleteIfExists(backupPath);
                }
            },
            _preferencesService.T("Loc.Backup.ToastRestoreFailedTitle"));
    }

    public void Dispose()
    {
        _preferencesService.PreferencesChanged -= OnPreferencesChanged;
    }

    private void OnPreferencesChanged()
    {
        RefreshOptions();
        RefreshDatabaseInfo();
        OnPropertyChanged(nameof(CloudProviderStatus));
    }

    private async Task RunBackupActionAsync(Func<Task> action, string errorTitle)
    {
        if (IsBusy)
            return;

        IsBusy = true;

        try
        {
            await action();
        }
        catch (Exception ex)
        {
            _toastService.Error(errorTitle, ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<bool> RunValidationAsync(Func<Task> action, string errorTitle)
    {
        try
        {
            await action();
            return true;
        }
        catch (Exception ex)
        {
            _toastService.Error(errorTitle, ex.Message);
            return false;
        }
    }

    private void RefreshDatabaseInfo()
    {
        var info = _backupService.GetDatabaseInfo();
        DatabaseLocation = info.Location;
        DatabaseSize = FormatBytes(info.Size);
        LastLocalBackup = FormatDate(info.LastLocalBackupAt);
        LastCloudBackup = FormatDate(info.LastCloudBackupAt);
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
        GoogleDriveClientId = preferences.GoogleDriveClientId ?? string.Empty;
        GoogleDriveClientSecret = preferences.GoogleDriveClientSecret ?? string.Empty;

        _isRefreshingOptions = false;
    }

    private void EnsureGoogleDriveConnected()
    {
        if (!_googleDriveOAuthService.IsConnected)
            throw new InvalidOperationException(_preferencesService.T("Loc.Backup.GoogleDriveNotConnected"));
    }

    private string FormatDate(DateTimeOffset? value)
    {
        return value is null
            ? _preferencesService.T("Loc.Backup.NotAvailable")
            : value.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        var value = (double)Math.Max(0, bytes);
        var unit = 0;

        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0
            ? $"{value:0} {units[unit]}"
            : $"{value:0.0} {units[unit]}";
    }

    private static string? NormalizeOptionalValue(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
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

public enum PreferencesSection
{
    General,
    DataBackup,
    About
}

public sealed record PreferenceOption<T>(string Label, T Value)
{
    public override string ToString() => Label;
}
