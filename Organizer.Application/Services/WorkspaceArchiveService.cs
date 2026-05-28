using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace Organizer.Application.Services;

public sealed class WorkspaceArchiveService
{
    public const string Extension = ".owsp";

    private const int CurrentSchemaVersion = 1;
    private const string ManifestEntryName = "workspace.json";
    private const string ThumbnailEntryName = "thumbnail.webp";
    private const string AssetsPrefix = "assets/";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public async Task SaveAtomicAsync(
        string path,
        IReadOnlyList<WorkspaceArchiveItem> items,
        string name,
        byte[]? thumbnailData)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidDataException("Workspace path is empty.");

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var tmpPath = fullPath + ".tmp";
        var bakPath = fullPath + ".bak";

        try
        {
            await using (var stream = File.Create(tmpPath))
                await SaveAsync(stream, items, name, thumbnailData);

            await ValidateStrictAsync(tmpPath);

            if (File.Exists(bakPath))
                File.Delete(bakPath);

            if (File.Exists(fullPath))
                File.Move(fullPath, bakPath);

            File.Move(tmpPath, fullPath);

            if (File.Exists(bakPath))
                File.Delete(bakPath);
        }
        catch
        {
            if (File.Exists(tmpPath))
                File.Delete(tmpPath);

            if (!File.Exists(fullPath) && File.Exists(bakPath))
                File.Move(bakPath, fullPath);

            throw;
        }
    }

    public async Task SaveAsync(
        Stream output,
        IReadOnlyList<WorkspaceArchiveItem> items,
        string name,
        byte[]? thumbnailData)
    {
        if (output.CanSeek)
            output.SetLength(0);

        using var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        var manifestItems = new List<WorkspaceManifestItem>(items.Count);
        var now = DateTimeOffset.UtcNow;

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var imageData = item.ImageData;

            if (imageData.Length == 0)
                throw new InvalidDataException($"Workspace item {index + 1} has no image data.");

            ValidateImage(imageData, item.Label);

            var assetName = $"{AssetsPrefix}{index + 1:D4}{GetExtension(item.MimeType)}";
            var assetEntry = archive.CreateEntry(assetName, CompressionLevel.Optimal);

            await using (var assetStream = assetEntry.Open())
                await assetStream.WriteAsync(imageData);

            manifestItems.Add(new WorkspaceManifestItem
            {
                Label = item.Label,
                MimeType = NormalizeMimeType(item.MimeType),
                Asset = assetName,
                Size = imageData.LongLength,
                Sha256 = ComputeSha256(imageData),
                X = item.X,
                Y = item.Y,
                Width = item.Width,
                Height = item.Height,
                OriginalWidth = item.OriginalWidth,
                OriginalHeight = item.OriginalHeight,
                ZIndex = item.ZIndex
            });
        }

        await WriteEntryAsync(archive, ThumbnailEntryName, GetThumbnailData(thumbnailData, items));

        var manifest = new WorkspaceManifest
        {
            SchemaVersion = CurrentSchemaVersion,
            Name = string.IsNullOrWhiteSpace(name) ? "Workspace" : name.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
            Items = manifestItems
        };

        var manifestEntry = archive.CreateEntry(ManifestEntryName, CompressionLevel.Optimal);
        await using var manifestStream = manifestEntry.Open();
        await JsonSerializer.SerializeAsync(manifestStream, manifest, JsonOptions);
    }

    public async Task<IReadOnlyList<WorkspaceArchiveItem>> LoadAsync(Stream input)
    {
        using var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
        var manifest = await ReadManifestAsync(archive);

        ValidateManifest(manifest);

        var items = new List<WorkspaceArchiveItem>(manifest.Items.Count);

        foreach (var item in manifest.Items)
            items.Add(await ReadItemAsync(archive, item, strict: false));

        return items;
    }

    public async Task<WorkspaceArchiveSummary> ReadSummaryAsync(string path)
    {
        await using var input = File.OpenRead(path);
        using var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
        var manifest = await ReadManifestAsync(archive);
        var thumbnail = archive.GetEntry(ThumbnailEntryName) is { } thumbnailEntry
            ? await ReadEntryAsync(thumbnailEntry)
            : null;

        return new WorkspaceArchiveSummary(
            string.IsNullOrWhiteSpace(manifest.Name)
                ? Path.GetFileNameWithoutExtension(path)
                : manifest.Name,
            manifest.Items?.Count ?? 0,
            thumbnail,
            manifest.UpdatedAt);
    }

    public async Task ValidateStrictAsync(string path)
    {
        await using var input = File.OpenRead(path);
        using var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
        var manifest = await ReadManifestAsync(archive);

        ValidateManifest(manifest);

        if (archive.GetEntry(ThumbnailEntryName) is null)
            throw new InvalidDataException("Workspace zip is missing thumbnail.webp.");

        foreach (var item in manifest.Items)
            await ReadItemAsync(archive, item, strict: true);
    }

    private static async Task<WorkspaceManifest> ReadManifestAsync(ZipArchive archive)
    {
        var manifestEntry = archive.GetEntry(ManifestEntryName)
            ?? throw new InvalidDataException("Workspace zip is missing workspace.json.");

        WorkspaceManifest? manifest;
        await using (var manifestStream = manifestEntry.Open())
            manifest = await JsonSerializer.DeserializeAsync<WorkspaceManifest>(manifestStream, JsonOptions);

        return manifest ?? throw new InvalidDataException("Workspace manifest is empty or invalid.");
    }

    private static async Task<WorkspaceArchiveItem> ReadItemAsync(
        ZipArchive archive,
        WorkspaceManifestItem item,
        bool strict)
    {
        string? validationMessage = null;
        byte[] imageData = [];

        try
        {
            ValidateAssetPath(item.Asset);

            var assetEntry = archive.GetEntry(item.Asset)
                ?? throw new InvalidDataException($"Workspace asset '{item.Asset}' was not found.");

            imageData = await ReadEntryAsync(assetEntry);

            if (item.Size is { } expectedSize && imageData.LongLength != expectedSize)
            {
                throw new InvalidDataException(
                    $"Workspace asset '{item.Asset}' size mismatch. Expected {expectedSize}, got {imageData.LongLength}.");
            }

            if (!string.IsNullOrWhiteSpace(item.Sha256))
            {
                var actualHash = ComputeSha256(imageData);
                if (!string.Equals(item.Sha256, actualHash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"Workspace asset '{item.Asset}' SHA256 mismatch.");
            }

            ValidateImage(imageData, item.Asset);
        }
        catch (Exception ex) when (!strict)
        {
            validationMessage = ex.Message;
            imageData = [];
        }

        return new WorkspaceArchiveItem(
            Label: item.Label,
            MimeType: NormalizeMimeType(item.MimeType),
            ImageData: imageData,
            X: item.X,
            Y: item.Y,
            Width: item.Width,
            Height: item.Height,
            OriginalWidth: item.OriginalWidth,
            OriginalHeight: item.OriginalHeight,
            ZIndex: item.ZIndex,
            IsMissingOrCorrupted: validationMessage is not null,
            ValidationMessage: validationMessage);
    }

    private static void ValidateManifest(WorkspaceManifest manifest)
    {
        if (manifest.SchemaVersion != CurrentSchemaVersion)
            throw new InvalidDataException($"Unsupported workspace schema version {manifest.SchemaVersion}.");

        if (manifest.Items is null)
            throw new InvalidDataException("Workspace manifest has no items list.");

        foreach (var item in manifest.Items)
        {
            if (string.IsNullOrWhiteSpace(item.Asset))
                throw new InvalidDataException("Workspace item has no asset path.");

            if (item.Width <= 0 || item.Height <= 0)
                throw new InvalidDataException($"Workspace item '{item.Asset}' has invalid size.");
        }
    }

    private static void ValidateAssetPath(string path)
    {
        if (!path.StartsWith(AssetsPrefix, StringComparison.Ordinal)
            || path.Contains("..", StringComparison.Ordinal)
            || path.Contains("\\", StringComparison.Ordinal)
            || path.EndsWith("/", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Workspace asset path '{path}' is invalid.");
        }
    }

    private static async Task WriteEntryAsync(ZipArchive archive, string entryName, byte[] data)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        await using var stream = entry.Open();
        await stream.WriteAsync(data);
    }

    private static async Task<byte[]> ReadEntryAsync(ZipArchiveEntry entry)
    {
        await using var entryStream = entry.Open();
        using var memoryStream = new MemoryStream();
        await entryStream.CopyToAsync(memoryStream);
        return memoryStream.ToArray();
    }

    private static byte[] GetThumbnailData(byte[]? thumbnailData, IReadOnlyList<WorkspaceArchiveItem> items)
    {
        if (thumbnailData is { Length: > 0 })
            return thumbnailData;

        return items.FirstOrDefault(item => item.ImageData.Length > 0)?.ImageData
            ?? throw new InvalidDataException("Workspace thumbnail could not be generated.");
    }

    private static void ValidateImage(byte[] imageData, string assetPath)
    {
        if (imageData.Length == 0)
            throw new InvalidDataException($"Workspace asset '{assetPath}' is empty.");

        try
        {
            using var stream = new MemoryStream(imageData, writable: false);
            using var bitmap = new Bitmap(stream);

            if (bitmap.PixelSize.Width <= 0 || bitmap.PixelSize.Height <= 0)
                throw new InvalidDataException($"Workspace asset '{assetPath}' has invalid dimensions.");
        }
        catch (Exception ex) when (ex is not InvalidDataException)
        {
            throw new InvalidDataException($"Workspace asset '{assetPath}' is not a valid image.", ex);
        }
    }

    private static string ComputeSha256(byte[] data)
    {
        return Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
    }

    private static string NormalizeMimeType(string? mimeType)
    {
        if (string.IsNullOrWhiteSpace(mimeType))
            return "image/png";

        return mimeType.Trim().ToLowerInvariant();
    }

    private static string GetExtension(string mimeType) =>
        NormalizeMimeType(mimeType) switch
        {
            "image/jpeg" or "image/jpg" => ".jpg",
            "image/gif" => ".gif",
            "image/bmp" => ".bmp",
            "image/webp" => ".webp",
            _ => ".png"
        };

    private sealed class WorkspaceManifest
    {
        public int SchemaVersion { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public List<WorkspaceManifestItem> Items { get; set; } = [];
    }

    private sealed class WorkspaceManifestItem
    {
        public string Label { get; set; } = string.Empty;
        public string MimeType { get; set; } = "image/png";
        public string Asset { get; set; } = string.Empty;
        public long? Size { get; set; }
        public string? Sha256 { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double OriginalWidth { get; set; }
        public double OriginalHeight { get; set; }
        public int ZIndex { get; set; }
    }
}

public sealed record WorkspaceArchiveSummary(
    string Name,
    int ImageCount,
    byte[]? ThumbnailData,
    DateTimeOffset? UpdatedAt);

public sealed record WorkspaceArchiveItem(
    string Label,
    string MimeType,
    byte[] ImageData,
    double X,
    double Y,
    double Width,
    double Height,
    double OriginalWidth,
    double OriginalHeight,
    int ZIndex,
    bool IsMissingOrCorrupted = false,
    string? ValidationMessage = null);
