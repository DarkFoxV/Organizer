namespace Organize.Organizer.Core;

public class ImageTag
{
    public int ImageId { get; set; }
    public Image Image { get; set; } = null!;

    public int TagId { get; set; }
    public Tag Tag { get; set; } = null!;
}