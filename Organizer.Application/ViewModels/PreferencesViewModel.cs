using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.EntityFrameworkCore;
using Organizer.Application.Services;

namespace Organizer.Application.ViewModels;

public partial class PreferencesViewModel : ObservableObject, IDisposable
{
    private readonly AppPreferencesService _preferencesService;
    private readonly BackupService _backupService;
    private readonly GoogleDriveOAuthService _googleDriveOAuthService;
    private readonly GoogleDriveBackupStorageProvider _googleDriveBackupStorageProvider;
    private readonly AppDbContextFactory _dbContextFactory;
    private readonly HomeWorkspaceCacheService _homeWorkspaceCacheService;
    private readonly IToastService _toastService;
    private readonly IBackgroundOperationService _backgroundOperationService;
    private readonly BackgroundOperationHostViewModel _backgroundOperationHost;
    private readonly DispatcherTimer _saveIndicatorTimer;
    private readonly DispatcherTimer _workspacePreferenceSaveTimer;
    private readonly CancellationTokenSource _lifetimeCts = new();
    private bool _isRefreshingOptions;
    private bool _isSavingPreference;
    private bool _hasPendingWorkspacePreferences;
    private bool _isDisposed;

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
    [ObservableProperty] private bool _isSaveIndicatorVisible;
    [ObservableProperty] private int _aboutImageCount;
    [ObservableProperty] private int _aboutWorkspaceCount;
    [ObservableProperty] private int _aboutTagCount;

    public bool IsGeneralSectionVisible => SelectedSection == PreferencesSection.General;
    public bool IsDataBackupSectionVisible => SelectedSection == PreferencesSection.DataBackup;
    public bool IsAboutSectionVisible => SelectedSection == PreferencesSection.About;
    public bool IsNotBusy => !IsBusy && !_backgroundOperationHost.HasOperations;

    public bool IsGeneralSelected => SelectedSection == PreferencesSection.General;
    public bool IsDataBackupSelected => SelectedSection == PreferencesSection.DataBackup;
    public bool IsAboutSelected => SelectedSection == PreferencesSection.About;
    public bool IsSystemThemeSelected => SelectedTheme?.Value == AppThemePreference.System;
    public bool IsDarkThemeSelected => SelectedTheme?.Value == AppThemePreference.Dark;
    public bool IsLightThemeSelected => SelectedTheme?.Value == AppThemePreference.Light;
    public bool IsGrayThemeSelected => SelectedTheme?.Value == AppThemePreference.Gray;

    public string CloudProviderName => "Google Drive";
    public string CloudProviderStatus => _googleDriveOAuthService.IsConnected
        ? _preferencesService.T("Loc.Backup.CloudStatusConnected")
        : _preferencesService.T("Loc.Backup.CloudStatusNotConnected");
    public bool IsCloudConnected => _googleDriveOAuthService.IsConnected;
    public bool IsCloudDisconnected => !IsCloudConnected;
    public string AppVersion => GetAppVersion();
    public string BuildNumber => Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "1.0.0";
    public string AboutImagesText => _preferencesService.T("Loc.Preferences.AboutImages", AboutImageCount);
    public string AboutWorkspacesText => _preferencesService.T("Loc.Preferences.AboutWorkspaces", AboutWorkspaceCount);
    public string AboutTagsText => _preferencesService.T("Loc.Preferences.AboutTags", AboutTagCount);
    public string TechnologyStackText => ".NET 10 • Avalonia • EF Core • SQLite";

    public PreferencesViewModel(
        AppPreferencesService preferencesService,
        BackupService backupService,
        GoogleDriveOAuthService googleDriveOAuthService,
        GoogleDriveBackupStorageProvider googleDriveBackupStorageProvider,
        AppDbContextFactory dbContextFactory,
        HomeWorkspaceCacheService homeWorkspaceCacheService,
        IToastService toastService,
        IBackgroundOperationService backgroundOperationService,
        BackgroundOperationHostViewModel backgroundOperationHost)
    {
        _preferencesService = preferencesService;
        _backupService = backupService;
        _googleDriveOAuthService = googleDriveOAuthService;
        _googleDriveBackupStorageProvider = googleDriveBackupStorageProvider;
        _dbContextFactory = dbContextFactory;
        _homeWorkspaceCacheService = homeWorkspaceCacheService;
        _toastService = toastService;
        _backgroundOperationService = backgroundOperationService;
        _backgroundOperationHost = backgroundOperationHost;
        _saveIndicatorTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2.5)
        };
        _saveIndicatorTimer.Tick += OnSaveIndicatorTimerTick;
        _workspacePreferenceSaveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _workspacePreferenceSaveTimer.Tick += OnWorkspacePreferenceSaveTimerTick;

        RefreshOptions();
        RefreshDatabaseInfo();
        _ = RefreshAboutStatsAsync();
        _preferencesService.PreferencesChanged += OnPreferencesChanged;
        _backgroundOperationHost.PropertyChanged += OnBackgroundOperationHostPropertyChanged;
    }

    partial void OnSelectedThemeChanged(PreferenceOption<AppThemePreference>? value)
    {
        OnPropertyChanged(nameof(IsSystemThemeSelected));
        OnPropertyChanged(nameof(IsDarkThemeSelected));
        OnPropertyChanged(nameof(IsLightThemeSelected));
        OnPropertyChanged(nameof(IsGrayThemeSelected));

        if (!_isRefreshingOptions && value is not null)
            SavePreference(preferences => preferences.Theme = value.Value);
    }

    partial void OnSelectedItemsPerPageChanged(PreferenceOption<int>? value)
    {
        if (!_isRefreshingOptions && value is not null)
            SavePreference(preferences => preferences.SearchItemsPerPage = value.Value);
    }

    partial void OnSelectedLanguageChanged(PreferenceOption<AppLanguagePreference>? value)
    {
        if (!_isRefreshingOptions && value is not null)
            SavePreference(preferences => preferences.Language = value.Value);
    }

    partial void OnConfirmDeletionChanged(bool value)
    {
        if (!_isRefreshingOptions)
            SavePreference(preferences => preferences.ConfirmDeletion = value);
    }

    partial void OnSelectedWorkspacePasteModeChanged(PreferenceOption<WorkspacePastePreference>? value)
    {
        if (!_isRefreshingOptions && value is not null)
            SavePreference(preferences => preferences.WorkspacePasteMode = value.Value);
    }

    partial void OnSelectedWorkspaceBackgroundChanged(PreferenceOption<WorkspaceBackgroundPreference>? value)
    {
        if (!_isRefreshingOptions && value is not null)
            SavePreference(preferences => preferences.WorkspaceBackground = value.Value);
    }

    partial void OnWorkspaceDefaultZoomPercentChanged(double value)
    {
        if (!_isRefreshingOptions)
            ScheduleWorkspacePreferenceSave();
    }

    partial void OnWorkspaceHistoryLimitChanged(double value)
    {
        if (!_isRefreshingOptions)
            ScheduleWorkspacePreferenceSave();
    }

    partial void OnGoogleDriveClientIdChanged(string value)
    {
        if (_isRefreshingOptions)
            return;

        SavePreference(preferences => preferences.GoogleDriveClientId = NormalizeOptionalValue(value));
        OnPropertyChanged(nameof(CloudProviderStatus));
    }

    partial void OnGoogleDriveClientSecretChanged(string value)
    {
        if (_isRefreshingOptions)
            return;

        SavePreference(preferences => preferences.GoogleDriveClientSecret = NormalizeOptionalValue(value));
    }

    partial void OnAboutImageCountChanged(int value)
    {
        OnPropertyChanged(nameof(AboutImagesText));
    }

    partial void OnAboutWorkspaceCountChanged(int value)
    {
        OnPropertyChanged(nameof(AboutWorkspacesText));
    }

    partial void OnAboutTagCountChanged(int value)
    {
        OnPropertyChanged(nameof(AboutTagsText));
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

        if (value == PreferencesSection.About)
            _ = RefreshAboutStatsAsync();
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

    public void SelectSystemTheme()
    {
        SelectedTheme = FindOption(ThemeOptions, AppThemePreference.System);
    }

    public void SelectDarkTheme()
    {
        SelectedTheme = FindOption(ThemeOptions, AppThemePreference.Dark);
    }

    public void SelectLightTheme()
    {
        SelectedTheme = FindOption(ThemeOptions, AppThemePreference.Light);
    }

    public void SelectGrayTheme()
    {
        SelectedTheme = FindOption(ThemeOptions, AppThemePreference.Gray);
    }

    public Task CreateLocalBackupAsync(string destinationPath)
    {
        if (!IsNotBusy)
            return Task.CompletedTask;

        var operation = _backgroundOperationService.Start(
            _preferencesService.T("Loc.BackgroundOperation.LocalBackupTitle"),
            _preferencesService.T("Loc.BackgroundOperation.Preparing"),
            canCancel: true);

        _ = RunBackgroundOperationAsync(
            operation,
            async cancellationToken =>
            {
                operation.ReportProgress(
                    15,
                    _preferencesService.T("Loc.BackgroundOperation.CreatingSnapshot"));
                var metadata = await Task.Run(
                    () => _backupService.CreateLocalBackupAsync(
                        destinationPath,
                        recordLocalBackup: false,
                        cancellationToken: cancellationToken),
                    cancellationToken);
                _preferencesService.Update(
                    preferences => preferences.LastLocalBackupAt = metadata.CreatedAt);
                operation.ReportProgress(
                    100,
                    _preferencesService.T("Loc.BackgroundOperation.Finalizing"));
                RefreshDatabaseInfo();
                _toastService.Success(
                    _preferencesService.T("Loc.Backup.ToastBackupCreatedTitle"),
                    _preferencesService.T("Loc.Backup.ToastBackupCreatedMessage"));
            },
            _preferencesService.T("Loc.Backup.ToastBackupFailedTitle"));

        return Task.CompletedTask;
    }

    public Task ExportDatabaseAsync(string destinationPath)
    {
        if (!IsNotBusy)
            return Task.CompletedTask;

        var operation = _backgroundOperationService.Start(
            _preferencesService.T("Loc.BackgroundOperation.ExportTitle"),
            _preferencesService.T("Loc.BackgroundOperation.CreatingSnapshot"),
            canCancel: true);

        _ = RunBackgroundOperationAsync(
            operation,
            async cancellationToken =>
            {
                operation.ReportProgress(
                    20,
                    _preferencesService.T("Loc.BackgroundOperation.CreatingSnapshot"));
                await Task.Run(
                    () => _backupService.ExportDatabaseAsync(destinationPath, cancellationToken),
                    cancellationToken);
                operation.ReportProgress(
                    100,
                    _preferencesService.T("Loc.BackgroundOperation.Finalizing"));
                _toastService.Success(
                    _preferencesService.T("Loc.Backup.ToastDatabaseExportedTitle"),
                    _preferencesService.T("Loc.Backup.ToastDatabaseExportedMessage"));
            },
            _preferencesService.T("Loc.Backup.ToastExportFailedTitle"));

        return Task.CompletedTask;
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

    public Task ConnectGoogleDriveAsync()
    {
        if (!IsNotBusy)
            return Task.CompletedTask;

        var operation = _backgroundOperationService.Start(
            _preferencesService.T("Loc.BackgroundOperation.GoogleConnectTitle"),
            _preferencesService.T("Loc.BackgroundOperation.GoogleConnectMessage"),
            canCancel: true);

        _ = RunBackgroundOperationAsync(
            operation,
            async cancellationToken =>
            {
                await _googleDriveOAuthService.ConnectAsync(cancellationToken);
                OnPropertyChanged(nameof(CloudProviderStatus));
                OnPropertyChanged(nameof(IsCloudConnected));
                OnPropertyChanged(nameof(IsCloudDisconnected));
                _toastService.Success(
                    _preferencesService.T("Loc.Backup.ToastGoogleDriveConnectedTitle"),
                    _preferencesService.T("Loc.Backup.ToastGoogleDriveConnectedMessage"));
            },
            _preferencesService.T("Loc.Backup.ToastGoogleDriveConnectFailedTitle"));

        return Task.CompletedTask;
    }

    public Task DisconnectGoogleDriveAsync()
    {
        if (!IsNotBusy)
            return Task.CompletedTask;

        var operation = _backgroundOperationService.Start(
            _preferencesService.T("Loc.BackgroundOperation.GoogleDisconnectTitle"),
            _preferencesService.T("Loc.BackgroundOperation.GoogleDisconnectMessage"));

        _ = RunBackgroundOperationAsync(
            operation,
            async _ =>
            {
                await _googleDriveOAuthService.DisconnectAsync();
                OnPropertyChanged(nameof(CloudProviderStatus));
                OnPropertyChanged(nameof(IsCloudConnected));
                OnPropertyChanged(nameof(IsCloudDisconnected));
                _toastService.Success(
                    _preferencesService.T("Loc.Backup.ToastGoogleDriveDisconnectedTitle"),
                    _preferencesService.T("Loc.Backup.ToastGoogleDriveDisconnectedMessage"));
            },
            _preferencesService.T("Loc.Backup.ToastGoogleDriveDisconnectFailedTitle"));

        return Task.CompletedTask;
    }

    public Task BackupToGoogleDriveAsync(CancellationToken cancellationToken = default)
    {
        if (!IsNotBusy)
            return Task.CompletedTask;

        var operationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var operation = _backgroundOperationService.Start(
            _preferencesService.T("Loc.BackgroundOperation.CloudBackupTitle"),
            _preferencesService.T("Loc.BackgroundOperation.Preparing"),
            canCancel: true,
            operationCts);

        _ = RunBackgroundOperationAsync(
            operation,
            async operationCancellationToken =>
            {
                EnsureGoogleDriveConnected();

                var backupPath = Path.Combine(
                    Path.GetTempPath(),
                    $"organizer-backup-{DateTimeOffset.Now:yyyy-MM-dd-HHmmss}.obak");

                try
                {
                    operation.ReportProgress(
                        10,
                        _preferencesService.T("Loc.BackgroundOperation.CreatingSnapshot"));
                    await Task.Run(
                        () => _backupService.CreateLocalBackupAsync(
                            backupPath,
                            recordLocalBackup: false,
                            operationCancellationToken),
                        operationCancellationToken);

                    operation.ReportProgress(
                        55,
                        _preferencesService.T("Loc.BackgroundOperation.Uploading"));
                    await _googleDriveBackupStorageProvider.UploadBackupAsync(
                        backupPath,
                        operationCancellationToken);

                    operation.ReportProgress(
                        100,
                        _preferencesService.T("Loc.BackgroundOperation.Finalizing"));
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

        return Task.CompletedTask;
    }

    public Task RestoreLatestFromGoogleDriveAsync(CancellationToken cancellationToken = default)
    {
        if (!IsNotBusy)
            return Task.CompletedTask;

        var operationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var operation = _backgroundOperationService.Start(
            _preferencesService.T("Loc.BackgroundOperation.CloudRestoreTitle"),
            _preferencesService.T("Loc.BackgroundOperation.Downloading"),
            canCancel: true,
            operationCts);

        _ = RunBackgroundOperationAsync(
            operation,
            async operationCancellationToken =>
            {
                EnsureGoogleDriveConnected();

                var backupPath = Path.Combine(
                    Path.GetTempPath(),
                    $"organizer-cloud-restore-{Guid.NewGuid():N}.obak");

                try
                {
                    operation.ReportProgress(
                        15,
                        _preferencesService.T("Loc.BackgroundOperation.Downloading"));
                    await _googleDriveBackupStorageProvider.DownloadLatestBackupAsync(
                        backupPath,
                        operationCancellationToken);

                    operation.ReportProgress(
                        65,
                        _preferencesService.T("Loc.BackgroundOperation.ValidatingRestore"));
                    await Task.Run(
                        () => _backupService.RestoreFromBackupAsync(
                            backupPath,
                            operationCancellationToken),
                        operationCancellationToken);

                    operation.ReportProgress(
                        100,
                        _preferencesService.T("Loc.BackgroundOperation.Finalizing"));
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

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _lifetimeCts.Cancel();
        FlushWorkspacePreferenceSave();
        _preferencesService.PreferencesChanged -= OnPreferencesChanged;
        _backgroundOperationHost.PropertyChanged -= OnBackgroundOperationHostPropertyChanged;
        _saveIndicatorTimer.Stop();
        _saveIndicatorTimer.Tick -= OnSaveIndicatorTimerTick;
        _workspacePreferenceSaveTimer.Stop();
        _workspacePreferenceSaveTimer.Tick -= OnWorkspacePreferenceSaveTimerTick;
        _lifetimeCts.Dispose();
    }

    private void OnPreferencesChanged(
        object? sender,
        AppPreferencesChangedEventArgs e)
    {
        if (!_isSavingPreference &&
            (e.LanguageChanged ||
             e.ThemeChanged ||
             e.SearchItemsPerPageChanged ||
             e.ConfirmDeletionChanged ||
             e.WorkspacePasteModeChanged ||
             e.WorkspaceBackgroundChanged ||
             e.WorkspaceDefaultZoomChanged ||
             e.WorkspaceHistoryLimitChanged ||
             e.BackupPreferencesChanged))
        {
            RefreshOptions();
        }

        if (e.BackupPreferencesChanged)
        {
            RefreshDatabaseInfo();
            OnPropertyChanged(nameof(CloudProviderStatus));
            OnPropertyChanged(nameof(IsCloudConnected));
            OnPropertyChanged(nameof(IsCloudDisconnected));
        }

        if (e.LanguageChanged)
        {
            OnPropertyChanged(nameof(CloudProviderStatus));
            OnPropertyChanged(nameof(AboutImagesText));
            OnPropertyChanged(nameof(AboutWorkspacesText));
            OnPropertyChanged(nameof(AboutTagsText));
        }
    }

    private void OnBackgroundOperationHostPropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BackgroundOperationHostViewModel.HasOperations))
            OnPropertyChanged(nameof(IsNotBusy));
    }

    private void SavePreference(Action<AppPreferences> update)
    {
        _isSavingPreference = true;
        try
        {
            _preferencesService.Update(update);
        }
        finally
        {
            _isSavingPreference = false;
        }

        ShowSaveIndicator();
    }

    private void ShowSaveIndicator()
    {
        IsSaveIndicatorVisible = true;
        _saveIndicatorTimer.Stop();
        _saveIndicatorTimer.Start();
    }

    private void OnSaveIndicatorTimerTick(object? sender, EventArgs e)
    {
        _saveIndicatorTimer.Stop();
        IsSaveIndicatorVisible = false;
    }

    private void ScheduleWorkspacePreferenceSave()
    {
        _hasPendingWorkspacePreferences = true;
        ShowSaveIndicator();
        _workspacePreferenceSaveTimer.Stop();
        _workspacePreferenceSaveTimer.Start();
    }

    private void OnWorkspacePreferenceSaveTimerTick(object? sender, EventArgs e)
    {
        FlushWorkspacePreferenceSave();
    }

    private void FlushWorkspacePreferenceSave()
    {
        if (!_hasPendingWorkspacePreferences)
            return;

        _workspacePreferenceSaveTimer.Stop();
        _hasPendingWorkspacePreferences = false;

        var zoomPercent = (int)Math.Round(WorkspaceDefaultZoomPercent);
        var historyLimit = Math.Clamp(
            (int)Math.Round(WorkspaceHistoryLimit),
            AppPreferences.MinWorkspaceHistoryLimit,
            AppPreferences.MaxWorkspaceHistoryLimit);

        SavePreference(preferences =>
        {
            preferences.WorkspaceDefaultZoomPercent = zoomPercent;
            preferences.WorkspaceHistoryLimit = historyLimit;
        });
    }

    private async Task RunBackupActionAsync(Func<Task> action, string errorTitle)
    {
        if (!IsNotBusy)
            return;

        IsBusy = true;

        try
        {
            await action();
        }
        catch (OperationCanceledException) when (_isDisposed)
        {
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

    private async Task RunBackgroundOperationAsync(
        IBackgroundOperation operation,
        Func<CancellationToken, Task> action,
        string errorTitle)
    {
        try
        {
            await action(operation.CancellationToken);
            operation.Complete();
        }
        catch (OperationCanceledException) when (operation.CancellationToken.IsCancellationRequested)
        {
            operation.Cancel();
            _toastService.Warning(
                _preferencesService.T("Loc.BackgroundOperation.CanceledTitle"),
                _preferencesService.T("Loc.BackgroundOperation.CanceledMessage"));
        }
        catch (Exception ex)
        {
            operation.Fail(ex);
            _toastService.Error(errorTitle, ex.Message);
        }
    }

    private async Task<bool> RunValidationAsync(Func<Task> action, string errorTitle)
    {
        if (!IsNotBusy)
            return false;

        IsBusy = true;

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
        finally
        {
            IsBusy = false;
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

    private async Task RefreshAboutStatsAsync()
    {
        await using var lease = await _dbContextFactory.CreateLeaseAsync();
        AboutImageCount = await lease.Context.Images.CountAsync();
        AboutTagCount = await lease.Context.Tags.CountAsync();
        AboutWorkspaceCount = _homeWorkspaceCacheService.RecentWorkspaces.Count;
    }

    private void RefreshOptions()
    {
        _isRefreshingOptions = true;

        ThemeOptions.Clear();
        ThemeOptions.Add(new(_preferencesService.T("Loc.Preferences.Theme.System"), AppThemePreference.System));
        ThemeOptions.Add(new(_preferencesService.T("Loc.Preferences.Theme.Dark"), AppThemePreference.Dark));
        ThemeOptions.Add(new(_preferencesService.T("Loc.Preferences.Theme.Light"), AppThemePreference.Light));
        ThemeOptions.Add(new(_preferencesService.T("Loc.Preferences.Theme.Gray"), AppThemePreference.Gray));

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
        WorkspaceBackgroundOptions.Add(new(_preferencesService.T("Loc.Preferences.Background.Theme"), WorkspaceBackgroundPreference.Theme));
        WorkspaceBackgroundOptions.Add(new(_preferencesService.T("Loc.Preferences.Background.Light"), WorkspaceBackgroundPreference.Light));
        WorkspaceBackgroundOptions.Add(new(_preferencesService.T("Loc.Preferences.Background.Dark"), WorkspaceBackgroundPreference.Dark));
        WorkspaceBackgroundOptions.Add(new(_preferencesService.T("Loc.Preferences.Background.Gray"), WorkspaceBackgroundPreference.Gray));

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

    private static string GetAppVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        return string.IsNullOrWhiteSpace(informationalVersion)
            ? assembly.GetName().Version?.ToString() ?? "1.0.0"
            : informationalVersion;
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
