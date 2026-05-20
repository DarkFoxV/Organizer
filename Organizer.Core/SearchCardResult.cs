using System;
using Organize.Organizer.Core.Enums;

namespace Organize.Organizer.Core;

public class SearchCardResult
{
    public int CardId { get; init; }
    public string Title { get; init; } = string.Empty;
    public CardType CardType { get; init; }
    public DateTime CreatedAt { get; init; }
    public int ImageCount { get; init; }
    public int? CoverImageId { get; init; }
    public byte[]? CoverThumbnail { get; init; }
    public string CoverFilename { get; init; } = string.Empty;
    public string CoverDescription { get; init; } = string.Empty;
}