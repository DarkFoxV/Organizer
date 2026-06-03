using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using global::Organizer.Application.Views;
using Organizer.Application.Services;
using Organizer.Application.ViewModels;

namespace Organizer.Organizer.Application.Views;

public partial class PreferencesView : UserControl
{
    public PreferencesView()
    {
        InitializeComponent();
        DetachedFromVisualTree += (_, _) =>
        {
            if (DataContext is PreferencesViewModel vm)
                vm.Dispose();
        };
    }

    private PreferencesViewModel VM => (PreferencesViewModel)DataContext!;

    private void OnGeneralSettingsClick(object? sender, RoutedEventArgs e)
    {
        VM.SelectGeneral();
    }

    private void OnDataBackupClick(object? sender, RoutedEventArgs e)
    {
        VM.SelectDataBackup();
    }

    private void OnAboutClick(object? sender, RoutedEventArgs e)
    {
        VM.SelectAbout();
    }

    private void OnSystemThemeClick(object? sender, RoutedEventArgs e)
    {
        VM.SelectSystemTheme();
    }

    private void OnDarkThemeClick(object? sender, RoutedEventArgs e)
    {
        VM.SelectDarkTheme();
    }

    private void OnLightThemeClick(object? sender, RoutedEventArgs e)
    {
        VM.SelectLightTheme();
    }

    private void OnGrayThemeClick(object? sender, RoutedEventArgs e)
    {
        VM.SelectGrayTheme();
    }

    private async void OnCreateLocalBackupClick(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
            return;

        var file = await storage.SaveFilePickerAsync(BackupFilePicker.CreateBackupSaveOptions());
        var path = file?.TryGetLocalPath();

        if (!string.IsNullOrWhiteSpace(path))
            await VM.CreateLocalBackupAsync(path);
    }

    private async void OnRestoreFromBackupClick(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
            return;

        var files = await storage.OpenFilePickerAsync(BackupFilePicker.CreateBackupOpenOptions());
        var path = files.Count > 0 ? files[0].TryGetLocalPath() : null;

        if (string.IsNullOrWhiteSpace(path) || !await VM.ValidateBackupForRestoreAsync(path))
            return;

        if (TopLevel.GetTopLevel(this) is not Window owner)
            return;

        var confirmed = await ConfirmationDialog.ShowAsync(
            owner,
            AppPreferencesService.Translate("Loc.Backup.RestoreConfirmTitle"),
            AppPreferencesService.Translate("Loc.Backup.RestoreConfirmMessage"),
            AppPreferencesService.Translate("Loc.Backup.Restore"),
            AppPreferencesService.Translate("Loc.Common.Cancel"),
            isDanger: true);

        if (confirmed)
            await VM.RestoreFromBackupAsync(path);
    }

    private async void OnExportDatabaseClick(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
            return;

        var file = await storage.SaveFilePickerAsync(BackupFilePicker.CreateDatabaseExportOptions());
        var path = file?.TryGetLocalPath();

        if (!string.IsNullOrWhiteSpace(path))
            await VM.ExportDatabaseAsync(path);
    }

    private async void OnImportDatabaseClick(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
            return;

        var files = await storage.OpenFilePickerAsync(BackupFilePicker.CreateDatabaseImportOptions());
        var path = files.Count > 0 ? files[0].TryGetLocalPath() : null;

        if (string.IsNullOrWhiteSpace(path) || !await VM.ValidateDatabaseForImportAsync(path))
            return;

        if (TopLevel.GetTopLevel(this) is not Window owner)
            return;

        var confirmed = await ConfirmationDialog.ShowAsync(
            owner,
            AppPreferencesService.Translate("Loc.Backup.ImportConfirmTitle"),
            AppPreferencesService.Translate("Loc.Backup.ImportConfirmMessage"),
            AppPreferencesService.Translate("Loc.Backup.Import"),
            AppPreferencesService.Translate("Loc.Common.Cancel"),
            isDanger: true);

        if (confirmed)
            await VM.ImportDatabaseAsync(path);
    }

    private void OnCloudPlaceholderClick(object? sender, RoutedEventArgs e)
    {
        VM.ShowCloudPlaceholder();
    }

    private async void OnConnectGoogleDriveClick(object? sender, RoutedEventArgs e)
    {
        await VM.ConnectGoogleDriveAsync();
    }

    private async void OnBackupToGoogleDriveClick(object? sender, RoutedEventArgs e)
    {
        await VM.BackupToGoogleDriveAsync();
    }

    private async void OnRestoreFromGoogleDriveClick(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner)
            return;

        var confirmed = await ConfirmationDialog.ShowAsync(
            owner,
            AppPreferencesService.Translate("Loc.Backup.RestoreConfirmTitle"),
            AppPreferencesService.Translate("Loc.Backup.RestoreCloudConfirmMessage"),
            AppPreferencesService.Translate("Loc.Backup.Restore"),
            AppPreferencesService.Translate("Loc.Common.Cancel"),
            isDanger: true);

        if (confirmed)
            await VM.RestoreLatestFromGoogleDriveAsync();
    }

    private async void OnDisconnectGoogleDriveClick(object? sender, RoutedEventArgs e)
    {
        await VM.DisconnectGoogleDriveAsync();
    }
}
