using System;
using System.Collections.Generic;
using Organize.Organizer.Core.Enums;

namespace Organize.Organizer.Core;

public class Card
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public CardType CardType { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // FK opcional para a imagem de capa
    public int? CoverImageId { get; set; }
    public Image? CoverImage { get; set; }

    // Navegação
    public ICollection<Image> Images { get; set; } = [];
}