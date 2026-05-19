using System;
using System.Collections.Generic;

namespace Organize.Organizer.Core;

public class Image
{
    public int Id { get; set; }

    // Qual card essa imagem pertence
    public int CardId { get; set; }
    public Card Card { get; set; } = null!;

    // Ordem dentro do card (0-based)
    public int Position { get; set; }

    // Dados binários
    public byte[] Data { get; set; } = [];
    public byte[]? Thumbnail { get; set; }

    public string Filename { get; set; } = string.Empty;
    public string? MimeType { get; set; }
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navegação de tags
    public ICollection<ImageTag> ImageTags { get; set; } = [];
}