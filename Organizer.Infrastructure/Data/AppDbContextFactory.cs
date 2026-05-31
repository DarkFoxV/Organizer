using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Data.Sqlite;
using Organize.Organizer.Infrastructure.Data;

namespace Organizer.Application.Services;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>, IDisposable, IAsyncDisposable
{
    private const int MaxConcurrentContexts = 3;

    private readonly ConcurrentQueue<SqliteConnection> _availableConnections = new();
    private readonly SemaphoreSlim _connectionSlots = new(MaxConcurrentContexts, MaxConcurrentContexts);
    private bool _isDisposed;

    public AppDbContextFactory()
    {
        EnsureDbDirectory();

        for (var i = 0; i < MaxConcurrentContexts; i++)
        {
            var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            ConfigureConnection(connection);
            _availableConnections.Enqueue(connection);
        }
    }

    private static string DbPath
    {
        get
        {
#if DEBUG
            return Path.Combine(
                AppContext.BaseDirectory,
                "organizer-dev.db");
#else
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Organizer",
                "organizer.db");
#endif
        }
    }

    private static string ConnectionString
    {
        get
        {
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = DbPath,
                Pooling = false
            };

            return builder.ToString();
        }
    }

    public AppDbContext CreateDbContext(string[] args)
    {
        EnsureDbDirectory();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(ConnectionString)
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
#if DEBUG
            .EnableSensitiveDataLogging()
#endif
            .Options;

        return new AppDbContext(options);
    }

    public async Task<AppDbContextLease> CreateLeaseAsync()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        await _connectionSlots.WaitAsync();

        if (!_availableConnections.TryDequeue(out var connection))
        {
            _connectionSlots.Release();
            throw new InvalidOperationException("No SQLite connection is available.");
        }

        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
#if DEBUG
                .EnableSensitiveDataLogging()
#endif
                .Options;

            return new AppDbContextLease(
                new AppDbContext(options),
                connection,
                ReturnConnection);
        }
        catch
        {
            ReturnConnection(connection);
            throw;
        }
    }

    public AppDbContext Create() => CreateDbContext([]);

    private static void EnsureDbDirectory()
    {
        var directory = Path.GetDirectoryName(DbPath);

        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
    }

    private static void ConfigureConnection(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
                              PRAGMA foreign_keys = ON;
                              PRAGMA journal_mode = WAL;
                              PRAGMA busy_timeout = 5000;
                              """;
        command.ExecuteNonQuery();
    }

    private void ReturnConnection(SqliteConnection connection)
    {
        if (_isDisposed)
        {
            connection.Dispose();
            return;
        }

        _availableConnections.Enqueue(connection);
        _connectionSlots.Release();
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _connectionSlots.Dispose();

        while (_availableConnections.TryDequeue(out var connection))
            connection.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _connectionSlots.Dispose();

        while (_availableConnections.TryDequeue(out var connection))
            await connection.DisposeAsync();
    }

    public sealed class AppDbContextLease(AppDbContext context, SqliteConnection connection, Action<SqliteConnection> release)
        : IAsyncDisposable, IDisposable
    {
        private bool _isDisposed;

        public AppDbContext Context { get; } = context;

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            Context.Dispose();
            release(connection);
        }

        public async ValueTask DisposeAsync()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            await Context.DisposeAsync();
            release(connection);
        }
    }
}
