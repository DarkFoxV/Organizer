using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace Organizer.Application.Services;

public sealed class WorkspaceArchiveService
{
    private const int CurrentSchemaVersion = 1;
    private const string ManifestEntryName = "workspace.json";
    private const string AssetsPrefix = "assets/";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public async Task SaveAsync(Stream output, IReadOnlyList<WorkspaceArchiveItem> items)
    {
        if (output.CanSeek)
            output.SetLength(0);

        using var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        var manifestItems = new List<WorkspaceManifestItem>(items.Count);

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var imageData = item.ImageData;

            if (imageData.Length == 0)
                throw new InvalidDataException($"Workspace item {index + 1} has no image data.");

            var assetName = $"{AssetsPrefix}{index + 1:D4}{GetExtension(item.MimeType)}";
            var assetEntry = archive.CreateEntry(assetName, CompressionLevel.Optimal);

            await using (var assetStream = assetEntry.Open())
                await assetStream.WriteAsync(imageData);

            manifestItems.Add(new WorkspaceManifestItem
            {
                Label = item.Label,
                MimeType = item.MimeType,
                Asset = assetName,
                X = item.X,
                Y = item.Y,
                Width = item.Width,
                Height = item.Height,
                OriginalWidth = item.OriginalWidth,
                OriginalHeight = item.OriginalHeight,
                ZIndex = item.ZIndex
            });
        }

        var manifest = new WorkspaceManifest
        {
            SchemaVersion = CurrentSchemaVersion,
            CreatedAt = DateTimeOffset.UtcNow,
            Items = manifestItems
        };

        var manifestEntry = archive.CreateEntry(ManifestEntryName, CompressionLevel.Optimal);
        await using var manifestStream = manifestEntry.Open();
        await JsonSerializer.SerializeAsync(manifestStream, manifest, JsonOptions);
    }

    public async Task<IReadOnlyList<WorkspaceArchiveItem>> LoadAsync(Stream input)
    {
        using var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
        var manifestEntry = archive.GetEntry(ManifestEntryName)
            ?? throw new InvalidDataException("Workspace zip is missing workspace.json.");

        WorkspaceManifest? manifest;
        await using (var manifestStream = manifestEntry.Open())
            manifest = await JsonSerializer.DeserializeAsync<WorkspaceManifest>(manifestStream, JsonOptions);

        if (manifest is null)
            throw new InvalidDataException("Workspace manifest is empty or invalid.");

        ValidateManifest(manifest);

        var items = new List<WorkspaceArchiveItem>(manifest.Items.Count);

        foreach (var item in manifest.Items)
        {
            ValidateAssetPath(item.Asset);

            var assetEntry = archive.GetEntry(item.Asset)
                ?? throw new InvalidDataException($"Workspace asset '{item.Asset}' was not found.");

            var imageData = await ReadEntryAsync(assetEntry);
            ValidateImage(imageData, item.Asset);

            items.Add(new WorkspaceArchiveItem(
                Label: item.Label,
                MimeType: NormalizeMimeType(item.MimeType),
                ImageData: imageData,
                X: item.X,
                Y: item.Y,
                Width: item.Width,
                Height: item.Height,
                OriginalWidth: item.OriginalWidth,
                OriginalHeight: item.OriginalHeight,
                ZIndex: item.ZIndex));
        }

        return items;
    }

    private static void ValidateManifest(WorkspaceManifest manifest)
    {
        if (manifest.SchemaVersion != CurrentSchemaVersion)
            throw new InvalidDataException($"Unsupported workspace schema version {manifest.SchemaVersion}.");

        if (manifest.Items is null)
            throw new InvalidDataException("Workspace manifest has no items list.");

        if (manifest.Items.Count == 0)
            throw new InvalidDataException("Workspace has no images.");

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

    private static async Task<byte[]> ReadEntryAsync(ZipArchiveEntry entry)
    {
        await using var entryStream = entry.Open();
        using var memoryStream = new MemoryStream();
        await entryStream.CopyToAsync(memoryStream);
        return memoryStream.ToArray();
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
        public DateTimeOffset CreatedAt { get; set; }
        public List<WorkspaceManifestItem> Items { get; set; } = [];
    }

    private sealed class WorkspaceManifestItem
    {
        public string Label { get; set; } = string.Empty;
        public string MimeType { get; set; } = "image/png";
        public string Asset { get; set; } = string.Empty;
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double OriginalWidth { get; set; }
        public double OriginalHeight { get; set; }
        public int ZIndex { get; set; }
    }
}

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
    int ZIndex);
