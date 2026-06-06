using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Organizer.Application.Services;

public sealed class BackupService(
    DatabaseFileService databaseFileService,
    AppPreferencesService preferencesService)
{
    public const string Extension = ".obak";

    private const int CurrentSchemaVersion = 1;
    private const string DatabaseEntryName = "organizer.db";
    private const string MetadataEntryName = "backup.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public DatabaseInfo GetDatabaseInfo()
    {
        return new DatabaseInfo(
            databaseFileService.DatabasePath,
            databaseFileService.GetDatabaseSize(),
            preferencesService.Current.LastLocalBackupAt,
            preferencesService.Current.LastCloudBackupAt);
    }

    public async Task<BackupMetadata> CreateLocalBackupAsync(
        string destinationPath,
        bool recordLocalBackup = true,
        CancellationToken cancellationToken = default)
    {
        var snapshotPath = await databaseFileService.CreateConsistentSnapshotAsync(cancellationToken);

        try
        {
            var metadata = new BackupMetadata
            {
                SchemaVersion = CurrentSchemaVersion,
                AppName = "Organizer",
                CreatedAt = DateTimeOffset.UtcNow,
                DatabaseFile = DatabaseEntryName,
                DatabaseSize = new FileInfo(snapshotPath).Length,
                DatabaseSha256 = await DatabaseFileService.ComputeSha256Async(snapshotPath, cancellationToken),
                AppVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? string.Empty
            };

            await CreateBackupArchiveAsync(
                destinationPath,
                snapshotPath,
                metadata,
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (recordLocalBackup)
                preferencesService.Update(preferences => preferences.LastLocalBackupAt = metadata.CreatedAt);

            return metadata;
        }
        finally
        {
            DeleteIfExists(snapshotPath);
        }
    }

    public Task ExportDatabaseAsync(string destinationPath, CancellationToken cancellationToken = default)
    {
        return databaseFileService.ExportSnapshotAsync(destinationPath, cancellationToken);
    }

    public async Task ValidateBackupAsync(string backupFilePath, CancellationToken cancellationToken = default)
    {
        var databasePath = await ExtractValidatedDatabaseAsync(backupFilePath, cancellationToken);
        DeleteIfExists(databasePath);
    }

    public async Task RestoreFromBackupAsync(string backupFilePath, CancellationToken cancellationToken = default)
    {
        var databasePath = await ExtractValidatedDatabaseAsync(backupFilePath, cancellationToken);

        try
        {
            await databaseFileService.ReplaceDatabaseAsync(databasePath, cancellationToken);
        }
        finally
        {
            DeleteIfExists(databasePath);
        }
    }

    public Task ValidateDatabaseAsync(string databasePath, CancellationToken cancellationToken = default)
    {
        return databaseFileService.ValidateOrganizerDatabaseAsync(databasePath, cancellationToken);
    }

    public Task ImportDatabaseAsync(string databasePath, CancellationToken cancellationToken = default)
    {
        return databaseFileService.ReplaceDatabaseAsync(databasePath, cancellationToken);
    }

    private static async Task CreateBackupArchiveAsync(
        string destinationPath,
        string databasePath,
        BackupMetadata metadata,
        CancellationToken cancellationToken)
    {
        EnsureParentDirectory(destinationPath);

        var tempPath = destinationPath + ".tmp";
        DeleteIfExists(tempPath);

        try
        {
            await using (var output = File.Create(tempPath))
            using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
            {
                var databaseEntry = archive.CreateEntry(DatabaseEntryName, CompressionLevel.Optimal);
                await using (var databaseInput = File.OpenRead(databasePath))
                await using (var databaseOutput = databaseEntry.Open())
                    await databaseInput.CopyToAsync(databaseOutput, cancellationToken);

                var metadataEntry = archive.CreateEntry(MetadataEntryName, CompressionLevel.Optimal);
                await using var metadataStream = metadataEntry.Open();
                await JsonSerializer.SerializeAsync(
                    metadataStream,
                    metadata,
                    JsonOptions,
                    cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            DeleteIfExists(destinationPath);
            File.Move(tempPath, destinationPath);
        }
        catch
        {
            DeleteIfExists(tempPath);
            throw;
        }
    }

    private async Task<string> ExtractValidatedDatabaseAsync(
        string backupFilePath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(backupFilePath) || !File.Exists(backupFilePath))
            throw new InvalidDataException("Backup file was not found.");

        using var archive = ZipFile.OpenRead(backupFilePath);
        var databaseEntry = archive.GetEntry(DatabaseEntryName)
            ?? throw new InvalidDataException("Backup is missing organizer.db.");
        var metadataEntry = archive.GetEntry(MetadataEntryName)
            ?? throw new InvalidDataException("Backup is missing backup.json.");

        BackupMetadata? metadata;
        await using (var metadataStream = metadataEntry.Open())
            metadata = await JsonSerializer.DeserializeAsync<BackupMetadata>(
                metadataStream,
                JsonOptions,
                cancellationToken);

        if (metadata is null)
            throw new InvalidDataException("Backup metadata is invalid.");

        if (metadata.SchemaVersion != CurrentSchemaVersion)
            throw new InvalidDataException("Backup schema version is not supported.");

        if (!string.Equals(metadata.DatabaseFile, DatabaseEntryName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Backup metadata points to an unexpected database file.");

        var extractedPath = Path.Combine(
            Path.GetTempPath(),
            $"organizer-backup-restore-{Guid.NewGuid():N}.db");

        try
        {
            await using (var entryStream = databaseEntry.Open())
            await using (var output = File.Create(extractedPath))
                await entryStream.CopyToAsync(output, cancellationToken);

            var file = new FileInfo(extractedPath);
            if (file.Length != metadata.DatabaseSize)
                throw new InvalidDataException("Backup database size does not match metadata.");

            var sha256 = await DatabaseFileService.ComputeSha256Async(extractedPath, cancellationToken);
            if (!string.Equals(sha256, metadata.DatabaseSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Backup database checksum does not match metadata.");

            await databaseFileService.ValidateOrganizerDatabaseAsync(extractedPath, cancellationToken);
            return extractedPath;
        }
        catch
        {
            DeleteIfExists(extractedPath);
            throw;
        }
    }

    private static void EnsureParentDirectory(string path)
    {
        var directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }
}
