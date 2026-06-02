using System;

namespace Organizer.Application.Services;

public sealed class BackupMetadata
{
    public int SchemaVersion { get; set; }

    public string AppName { get; set; } = "Organizer";

    public DateTimeOffset CreatedAt { get; set; }

    public string DatabaseFile { get; set; } = "organizer.db";

    public long DatabaseSize { get; set; }

    public string DatabaseSha256 { get; set; } = string.Empty;

    public string AppVersion { get; set; } = string.Empty;
}
