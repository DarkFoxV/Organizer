namespace Organize.Organizer.Core.Helpers;

public static class TagNormalizer
{
    public static string Normalize(string value)
    {
        return value
            .Trim()
            .ToLowerInvariant();
    }
}