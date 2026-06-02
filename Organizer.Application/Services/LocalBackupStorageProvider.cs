using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Organizer.Application.Services;

public sealed class LocalBackupStorageProvider : IBackupStorageProvider
{
    private readonly string _backupDirectory;

    public LocalBackupStorageProvider()
    {
        _backupDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Organizer",
            "Backups");
    }

    public string Name => "Local";

    public Task UploadBackupAsync(string backupFilePath, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_backupDirectory);

        var destination = Path.Combine(_backupDirectory, Path.GetFileName(backupFilePath));
        File.Copy(backupFilePath, destination, overwrite: true);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<BackupFileInfo>> ListBackupsAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_backupDirectory);

        IReadOnlyList<BackupFileInfo> backups = Directory
            .EnumerateFiles(_backupDirectory, "*.obak")
            .Select(path =>
            {
                var file = new FileInfo(path);
                return new BackupFileInfo(file.Name, file.FullName, file.Length, file.CreationTimeUtc);
            })
            .OrderByDescending(backup => backup.CreatedAt)
            .ToList();

        return Task.FromResult(backups);
    }

    public Task DownloadBackupAsync(
        BackupFileInfo backup,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        File.Copy(backup.Path, destinationPath, overwrite: true);
        return Task.CompletedTask;
    }
}
