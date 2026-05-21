namespace Organizer.Application.Services;

public sealed record ClipboardImageData(
    string Filename,
    string MimeType,
    byte[] Data
);