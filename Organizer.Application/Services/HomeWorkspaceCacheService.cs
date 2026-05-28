using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;

namespace Organizer.Application.Services;

public sealed class HomeWorkspaceCacheService
{
    private readonly string _cachePath;
    private readonly string _cacheDirectory;
    private readonly WorkspaceArchiveService _workspaceArchiveService;
    private HomeWorkspaceCache _cache;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public HomeWorkspaceCacheService(WorkspaceArchiveService workspaceArchiveService)
    {
        _workspaceArchiveService = workspaceArchiveService;

        var appDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Organizer");

        _cachePath = Path.Combine(appDirectory, "home-workspaces.json");
        _cacheDirectory = Path.Combine(appDirectory, "cache");
        _cache = Load();
        ValidateStartup();
    }

    public event Action? Changed;

    public IReadOnlyList<HomeWorkspaceCacheEntry> RecentWorkspaces => _cache.RecentWorkspaces;

    public async Task RememberAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            var fullPath = Path.GetFullPath(path);
            var summary = await _workspaceArchiveService.ReadSummaryAsync(fullPath);
            var thumbnailCache = await CacheThumbnailAsync(fullPath, summary.ThumbnailData);

            _cache.RecentWorkspaces.RemoveAll(workspace =>
                string.Equals(workspace.Path, fullPath, StringComparison.OrdinalIgnoreCase));

            _cache.RecentWorkspaces.Insert(0, new HomeWorkspaceCacheEntry
            {
                Path = fullPath,
                Name = summary.Name,
                ThumbnailCache = thumbnailCache,
                ImageCount = summary.ImageCount,
                LastOpenedAt = DateTimeOffset.Now,
                IsMissing = false
            });

            Save();
            Changed?.Invoke();
        }
        catch
        {
            // Home cache must never make workspace save/load fail.
        }
    }

    public void Remove(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var removed = _cache.RecentWorkspaces.RemoveAll(workspace =>
            string.Equals(workspace.Path, fullPath, StringComparison.OrdinalIgnoreCase));

        if (removed == 0)
            return;

        Save();
        Changed?.Invoke();
    }

    public void DeleteWorkspace(string path)
    {
        var fullPath = Path.GetFullPath(path);

        if (File.Exists(fullPath))
            MoveToTrashOrDelete(fullPath);

        Remove(fullPath);
    }

    public void ValidateStartup()
    {
        var changed = false;

        foreach (var workspace in _cache.RecentWorkspaces)
        {
            var isMissing = !File.Exists(workspace.Path);
            if (workspace.IsMissing == isMissing)
                continue;

            workspace.IsMissing = isMissing;
            changed = true;
        }

        if (!changed)
            return;

        Save();
        Changed?.Invoke();
    }

    public string GetThumbnailFullPath(HomeWorkspaceCacheEntry entry)
    {
        return string.IsNullOrWhiteSpace(entry.ThumbnailCache)
            ? string.Empty
            : Path.Combine(Path.GetDirectoryName(_cachePath) ?? string.Empty, entry.ThumbnailCache);
    }

    private HomeWorkspaceCache Load()
    {
        try
        {
            if (!File.Exists(_cachePath))
                return new HomeWorkspaceCache();

            var json = File.ReadAllText(_cachePath);
            var cache = JsonSerializer.Deserialize<HomeWorkspaceCache>(json, JsonOptions) ?? new HomeWorkspaceCache();
            cache.RecentWorkspaces ??= [];
            return cache;
        }
        catch
        {
            return new HomeWorkspaceCache();
        }
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_cachePath) ?? string.Empty);
        var json = JsonSerializer.Serialize(_cache, JsonOptions);
        File.WriteAllText(_cachePath, json);
    }

    private async Task<string> CacheThumbnailAsync(string workspacePath, byte[]? thumbnailData)
    {
        if (thumbnailData is not { Length: > 0 })
            return string.Empty;

        Directory.CreateDirectory(_cacheDirectory);

        var fileName = $"{GetSafeFileName(Path.GetFileNameWithoutExtension(workspacePath))}-{HashPath(workspacePath)}.webp";
        var relativePath = Path.Combine("cache", fileName);
        var fullPath = Path.Combine(Path.GetDirectoryName(_cachePath) ?? string.Empty, relativePath);

        await File.WriteAllBytesAsync(fullPath, thumbnailData);
        return relativePath;
    }

    private static string GetSafeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var chars = value.Select(ch => invalidChars.Contains(ch) ? '-' : ch).ToArray();
        var fileName = new string(chars).Trim('-', ' ');
        return string.IsNullOrWhiteSpace(fileName) ? "workspace" : fileName;
    }

    private static string HashPath(string path)
    {
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(path));
        return Convert.ToHexString(hash, 0, 5).ToLowerInvariant();
    }

    private static void MoveToTrashOrDelete(string path)
    {
        try
        {
            var trashDirectory = GetTrashDirectory();
            if (trashDirectory is not null)
            {
                Directory.CreateDirectory(trashDirectory);
                var destination = GetAvailableTrashPath(trashDirectory, Path.GetFileName(path));
                File.Move(path, destination);
                return;
            }
        }
        catch
        {
            // Fall back to permanent deletion below.
        }

        File.Delete(path);
    }

    private static string? GetTrashDirectory()
    {
        if (OperatingSystem.IsWindows())
            return null;

        if (OperatingSystem.IsMacOS())
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".Trash");

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local",
            "share",
            "Trash",
            "files");
    }

    private static string GetAvailableTrashPath(string trashDirectory, string fileName)
    {
        var destination = Path.Combine(trashDirectory, fileName);

        if (!File.Exists(destination))
            return destination;

        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);

        for (var index = 1; ; index++)
        {
            destination = Path.Combine(trashDirectory, $"{baseName}-{index}{extension}");
            if (!File.Exists(destination))
                return destination;
        }
    }
}

public sealed class HomeWorkspaceCache
{
    public List<HomeWorkspaceCacheEntry> RecentWorkspaces { get; set; } = [];
}

public sealed class HomeWorkspaceCacheEntry
{
    public string Path { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ThumbnailCache { get; set; } = string.Empty;
    public int ImageCount { get; set; }
    public DateTimeOffset? LastOpenedAt { get; set; }
    public bool IsMissing { get; set; }
}
