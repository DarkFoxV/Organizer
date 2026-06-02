using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Organizer.Application.Services;

public interface IBackupStorageProvider
{
    string Name { get; }

    Task UploadBackupAsync(string backupFilePath, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BackupFileInfo>> ListBackupsAsync(CancellationToken cancellationToken = default);

    Task DownloadBackupAsync(
        BackupFileInfo backup,
        string destinationPath,
        CancellationToken cancellationToken = default);
}
