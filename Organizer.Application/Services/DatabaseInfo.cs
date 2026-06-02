using System;

namespace Organizer.Application.Services;

public sealed record DatabaseInfo(
    string Location,
    long Size,
    DateTimeOffset? LastLocalBackupAt,
    DateTimeOffset? LastCloudBackupAt);
