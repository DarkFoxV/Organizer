using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Organize.Organizer.Infrastructure.Data;

namespace Organizer.Application.Services;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
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

    public AppDbContext CreateDbContext(string[] args)
    {
        var directory = Path.GetDirectoryName(DbPath);

        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={DbPath}")
            .EnableSensitiveDataLogging()
            .Options;

        return new AppDbContext(options);
    }

    public AppDbContext Create()
    {
        return CreateDbContext([]);
    }
}