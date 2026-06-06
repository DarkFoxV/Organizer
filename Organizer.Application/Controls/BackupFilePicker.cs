using System;
using Avalonia.Platform.Storage;
using Organizer.Application.Services;

namespace Organizer.Application.Views;

internal static class BackupFilePicker
{
    public static FilePickerFileType BackupFileType { get; } = new("Organizer backup")
    {
        Patterns = ["*.obak"],
        MimeTypes = ["application/zip", "application/x-zip-compressed"]
    };

    public static FilePickerFileType DatabaseFileType { get; } = new("SQLite database")
    {
        Patterns = ["*.db", "*.sqlite", "*.sqlite3"],
        MimeTypes = ["application/vnd.sqlite3", "application/octet-stream"]
    };

    public static FilePickerSaveOptions CreateBackupSaveOptions()
    {
        return new FilePickerSaveOptions
        {
            Title = AppPreferencesService.Translate("Loc.Backup.CreateLocalBackup"),
            SuggestedFileName = $"organizer-backup-{DateTimeOffset.Now:yyyy-MM-dd-HHmm}.obak",
            DefaultExtension = "obak",
            FileTypeChoices = [BackupFileType]
        };
    }

    public static FilePickerSaveOptions CreateDatabaseExportOptions()
    {
        return new FilePickerSaveOptions
        {
            Title = AppPreferencesService.Translate("Loc.Backup.ExportDatabase"),
            SuggestedFileName = "organizer.db",
            DefaultExtension = "db",
            FileTypeChoices = [DatabaseFileType]
        };
    }

    public static FilePickerOpenOptions CreateBackupOpenOptions()
    {
        return new FilePickerOpenOptions
        {
            Title = AppPreferencesService.Translate("Loc.Backup.RestoreFromBackup"),
            AllowMultiple = false,
            FileTypeFilter = [BackupFileType]
        };
    }

    public static FilePickerOpenOptions CreateDatabaseImportOptions()
    {
        return new FilePickerOpenOptions
        {
            Title = AppPreferencesService.Translate("Loc.Backup.ImportDatabase"),
            AllowMultiple = false,
            FileTypeFilter = [DatabaseFileType]
        };
    }
}
