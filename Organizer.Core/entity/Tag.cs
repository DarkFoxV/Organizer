using System.Collections.Generic;
using Organize.Organizer.Core.Enums;

namespace Organize.Organizer.Core;

public class Tag
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public TagColor Color { get; set; }

    // Navegação
    public ICollection<ImageTag> ImageTags { get; set; } = [];
}