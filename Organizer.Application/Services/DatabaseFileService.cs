using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Organizer.Application.Services;

public sealed class DatabaseFileService(AppDbContextFactory dbContextFactory)
{
    private static readonly string[] ExpectedTables = ["Cards", "Images", "Tags", "ImageTags"];

    public string DatabasePath => AppDbContextFactory.DatabasePath;

    public string EmergencyBackupDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Organizer",
        "EmergencyBackups");

    public long GetDatabaseSize()
    {
        return File.Exists(DatabasePath)
            ? new FileInfo(DatabasePath).Length
            : 0;
    }

    public async Task<string> CreateConsistentSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var snapshotPath = Path.Combine(
            Path.GetTempPath(),
            $"organizer-db-snapshot-{Guid.NewGuid():N}.db");

        if (File.Exists(snapshotPath))
            File.Delete(snapshotPath);

        await using var lease = await dbContextFactory.CreateLeaseAsync();
        var connection = lease.Context.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "VACUUM INTO $path;";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "$path";
        parameter.Value = snapshotPath;
        command.Parameters.Add(parameter);

        await command.ExecuteNonQueryAsync(cancellationToken);
        return snapshotPath;
    }

    public async Task ExportSnapshotAsync(string destinationPath, CancellationToken cancellationToken = default)
    {
        var snapshotPath = await CreateConsistentSnapshotAsync(cancellationToken);
        var tempDestinationPath = destinationPath + ".tmp";

        try
        {
            EnsureParentDirectory(destinationPath);
            DeleteIfExists(tempDestinationPath);

            await using (var input = File.OpenRead(snapshotPath))
            await using (var output = File.Create(tempDestinationPath))
                await input.CopyToAsync(output, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            DeleteIfExists(destinationPath);
            File.Move(tempDestinationPath, destinationPath);
        }
        finally
        {
            DeleteIfExists(snapshotPath);
            DeleteIfExists(tempDestinationPath);
        }
    }

    public async Task ReplaceDatabaseAsync(string replacementDatabasePath, CancellationToken cancellationToken = default)
    {
        await ValidateOrganizerDatabaseAsync(replacementDatabasePath, cancellationToken);
        var emergencyBackupPath = await CreateEmergencyBackupAsync(cancellationToken);

        try
        {
            await dbContextFactory.WithExclusiveDatabaseAccessAsync(() =>
            {
                ReplaceDatabaseFiles(replacementDatabasePath);
                return Task.CompletedTask;
            });
        }
        catch
        {
            await dbContextFactory.WithExclusiveDatabaseAccessAsync(() =>
            {
                ReplaceDatabaseFiles(emergencyBackupPath);
                return Task.CompletedTask;
            });

            throw;
        }
    }

    public async Task<string> CreateEmergencyBackupAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(EmergencyBackupDirectory);

        var snapshotPath = await CreateConsistentSnapshotAsync(cancellationToken);
        var destinationPath = Path.Combine(
            EmergencyBackupDirectory,
            $"organizer-emergency-{DateTimeOffset.UtcNow:yyyy-MM-dd-HHmmss}.db");

        try
        {
            File.Copy(snapshotPath, destinationPath, overwrite: false);
            return destinationPath;
        }
        finally
        {
            DeleteIfExists(snapshotPath);
        }
    }

    public async Task ValidateOrganizerDatabaseAsync(
        string databasePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(databasePath) || !File.Exists(databasePath))
            throw new InvalidDataException("Database file was not found.");

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        };

        await using var connection = new SqliteConnection(builder.ToString());
        await connection.OpenAsync(cancellationToken);

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA integrity_check;";
            var result = await command.ExecuteScalarAsync(cancellationToken);

            if (!string.Equals(result?.ToString(), "ok", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("SQLite integrity check failed.");
        }

        var existingTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table';";

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                existingTables.Add(reader.GetString(0));
        }

        foreach (var table in ExpectedTables)
        {
            if (!existingTables.Contains(table))
                throw new InvalidDataException($"Database is missing required Organizer table '{table}'.");
        }
    }

    public static async Task<string> ComputeSha256Async(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private void ReplaceDatabaseFiles(string sourcePath)
    {
        EnsureParentDirectory(DatabasePath);

        var tempTarget = DatabasePath + ".replace";
        File.Copy(sourcePath, tempTarget, overwrite: true);

        DeleteIfExists(DatabasePath);
        DeleteIfExists(DatabasePath + "-wal");
        DeleteIfExists(DatabasePath + "-shm");

        File.Move(tempTarget, DatabasePath);
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
