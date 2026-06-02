using System;

namespace Organizer.Application.Services;

public sealed record BackupFileInfo(
    string Name,
    string Path,
    long Size,
    DateTimeOffset? CreatedAt);
