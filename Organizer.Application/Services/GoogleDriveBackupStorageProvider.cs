using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google;
using Google.Apis.Download;
using Google.Apis.Drive.v3;
using Google.Apis.Upload;
using DriveFile = Google.Apis.Drive.v3.Data.File;

namespace Organizer.Application.Services;

public sealed class GoogleDriveBackupStorageProvider(
    GoogleDriveOAuthService authService,
    BackupService backupService,
    AppPreferencesService preferencesService,
    IAppLogger logger) : IBackupStorageProvider
{
    private const string BackupFolderName = "Organizer Backups";
    private const string LatestBackupFileName = "organizer-latest.obak";
    private const string FolderMimeType = "application/vnd.google-apps.folder";
    private const string BackupMimeType = "application/octet-stream";

    public string Name => "Google Drive";

    public async Task UploadBackupAsync(string backupFilePath, CancellationToken cancellationToken = default)
    {
        logger.Info("Google Drive backup upload started.");

        try
        {
            await ValidateBackupBeforeUploadAsync(backupFilePath, cancellationToken);

            using var driveService = await authService.CreateDriveServiceAsync(cancellationToken);
            var folderId = await EnsureBackupFolderAsync(driveService, cancellationToken);
            var latestFile = await FindLatestBackupFileAsync(driveService, folderId, cancellationToken);

            await using var input = System.IO.File.OpenRead(backupFilePath);
            var metadata = new DriveFile
            {
                Name = LatestBackupFileName,
                MimeType = BackupMimeType,
                Parents = latestFile is null ? [folderId] : null
            };

            IUploadProgress progress;
            if (latestFile?.Id is { } existingFileId)
            {
                var update = driveService.Files.Update(metadata, existingFileId, input, BackupMimeType);
                update.Fields = "id,name";
                progress = await update.UploadAsync(cancellationToken);
            }
            else
            {
                var create = driveService.Files.Create(metadata, input, BackupMimeType);
                create.Fields = "id,name";
                progress = await create.UploadAsync(cancellationToken);
            }

            if (progress.Status == UploadStatus.Failed)
                throw progress.Exception ?? new InvalidOperationException(preferencesService.T("Loc.Backup.GoogleDriveUploadFailed"));

            logger.Info("Google Drive backup upload completed.");
        }
        catch (Exception ex)
        {
            logger.Error("Google Drive backup upload failed", ex);

            if (ex is FileNotFoundException or InvalidDataException)
                throw new InvalidOperationException(preferencesService.T("Loc.Backup.GoogleDriveInvalidBackupUpload"), ex);

            throw new InvalidOperationException(preferencesService.T("Loc.Backup.GoogleDriveUploadFailed"), ex);
        }
    }

    public async Task<IReadOnlyList<BackupFileInfo>> ListBackupsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var driveService = await authService.CreateDriveServiceAsync(cancellationToken);
            var folderId = await EnsureBackupFolderAsync(driveService, cancellationToken);
            var latestFile = await FindLatestBackupFileAsync(driveService, folderId, cancellationToken);

            return latestFile is null
                ? []
                :
                [
                    new BackupFileInfo(
                        latestFile.Name ?? LatestBackupFileName,
                        latestFile.Id ?? string.Empty,
                        latestFile.Size ?? 0,
                        latestFile.CreatedTimeDateTimeOffset)
                ];
        }
        catch (Exception ex)
        {
            logger.Error("Google Drive backup list failed", ex);
            throw new InvalidOperationException(preferencesService.T("Loc.Backup.GoogleDriveListFailed"), ex);
        }
    }

    public async Task<BackupFileInfo?> GetLatestBackupAsync(CancellationToken cancellationToken = default)
    {
        return (await ListBackupsAsync(cancellationToken)).FirstOrDefault();
    }

    public async Task DownloadBackupAsync(
        BackupFileInfo backup,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        logger.Info("Google Drive backup download started.");

        try
        {
            using var driveService = await authService.CreateDriveServiceAsync(cancellationToken);

            var directory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            await using var output = System.IO.File.Create(destinationPath);
            var request = driveService.Files.Get(backup.Path);
            var progress = await request.DownloadAsync(output, cancellationToken);

            if (progress.Status == DownloadStatus.Failed)
                throw progress.Exception ?? new InvalidOperationException(preferencesService.T("Loc.Backup.GoogleDriveDownloadFailed"));

            logger.Info("Google Drive backup download completed.");
        }
        catch (Exception ex)
        {
            logger.Error("Google Drive backup download failed", ex);
            throw new InvalidOperationException(preferencesService.T("Loc.Backup.GoogleDriveDownloadFailed"), ex);
        }
    }

    public async Task DownloadLatestBackupAsync(
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        var latest = await GetLatestBackupAsync(cancellationToken)
            ?? throw new FileNotFoundException(preferencesService.T("Loc.Backup.GoogleDriveNoBackupFound"));

        await DownloadBackupAsync(latest, destinationPath, cancellationToken);
    }

    private static async Task<string> EnsureBackupFolderAsync(
        DriveService driveService,
        CancellationToken cancellationToken)
    {
        var existingFolder = await FindBackupFolderAsync(driveService, cancellationToken);
        if (existingFolder?.Id is not null)
            return existingFolder.Id;

        var metadata = new DriveFile
        {
            Name = BackupFolderName,
            MimeType = FolderMimeType
        };

        var create = driveService.Files.Create(metadata);
        create.Fields = "id";
        var folder = await create.ExecuteAsync(cancellationToken);

        return folder.Id ?? throw new InvalidDataException("Google Drive folder response is missing id.");
    }

    private static async Task<DriveFile?> FindBackupFolderAsync(
        DriveService driveService,
        CancellationToken cancellationToken)
    {
        var list = driveService.Files.List();
        list.Spaces = "drive";
        list.Fields = "files(id,name)";
        list.Q = $"name = '{EscapeQueryValue(BackupFolderName)}' and mimeType = '{FolderMimeType}' and trashed = false";

        var result = await list.ExecuteAsync(cancellationToken);
        return result.Files?.FirstOrDefault(file => !string.IsNullOrWhiteSpace(file.Id));
    }

    private static async Task<DriveFile?> FindLatestBackupFileAsync(
        DriveService driveService,
        string folderId,
        CancellationToken cancellationToken)
    {
        var list = driveService.Files.List();
        list.Spaces = "drive";
        list.Fields = "files(id,name,size,createdTime)";
        list.Q = $"'{EscapeQueryValue(folderId)}' in parents and name = '{LatestBackupFileName}' and trashed = false";

        var result = await list.ExecuteAsync(cancellationToken);
        return result.Files?.FirstOrDefault(file => !string.IsNullOrWhiteSpace(file.Id));
    }

    private async Task ValidateBackupBeforeUploadAsync(
        string backupFilePath,
        CancellationToken cancellationToken)
    {
        var file = new FileInfo(backupFilePath);

        if (!file.Exists)
            throw new FileNotFoundException("Backup file was not found.", backupFilePath);

        if (file.Length == 0)
            throw new InvalidDataException("Backup file is empty.");

        await backupService.ValidateBackupAsync(backupFilePath, cancellationToken);
    }

    private static string EscapeQueryValue(string value)
    {
        return value.Replace("\\", "\\\\").Replace("'", "\\'");
    }
}
