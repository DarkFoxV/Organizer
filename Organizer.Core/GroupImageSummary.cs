namespace Organize.Organizer.Core;

public class GroupImageSummary
{
    public int Id { get; init; }
    public int Position { get; init; }
    public byte[]? Thumbnail { get; init; }
    public string Filename { get; init; } = string.Empty;
    public string? MimeType { get; init; }
    public string Description { get; init; } = string.Empty;
}