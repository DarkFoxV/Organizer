using System.Collections.Generic;
using Organize.Organizer.Core.Enums;

namespace Organizer.Application.Services;

public static class TagColorPalette
{
    private static readonly IReadOnlyDictionary<TagColor, TagColorPaletteEntry> Entries =
        new Dictionary<TagColor, TagColorPaletteEntry>
        {
            [TagColor.Blue] = new("#3b82f6", "#0d1f3c", "#60a5fa", "#dbeafe", "#1d4ed8", "#bfdbfe"),
            [TagColor.Red] = new("#dc2626", "#1f0a0a", "#f87171", "#fee2e2", "#991b1b", "#fecaca"),
            [TagColor.Green] = new("#16a34a", "#0a1f0f", "#4ade80", "#dcfce7", "#166534", "#bbf7d0"),
            [TagColor.Orange] = new("#d97706", "#1f160a", "#fb923c", "#ffedd5", "#9a3412", "#fed7aa"),
            [TagColor.Purple] = new("#9333ea", "#160a1f", "#c084fc", "#f3e8ff", "#7e22ce", "#e9d5ff"),
            [TagColor.Pink] = new("#db2777", "#1f0a14", "#f472b6", "#fce7f3", "#be185d", "#fbcfe8"),
            [TagColor.Indigo] = new("#4f46e5", "#0e0d1f", "#818cf8", "#e0e7ff", "#4338ca", "#c7d2fe"),
            [TagColor.Teal] = new("#0d9488", "#0a1a1f", "#2dd4bf", "#ccfbf1", "#0f766e", "#99f6e4"),
            [TagColor.Gray] = new("#6b7280", "#141414", "#9ca3af", "#f1f5f9", "#475569", "#cbd5e1"),
            [TagColor.Yellow] = new("#ca8a04", "#1f1a08", "#facc15", "#fef9c3", "#854d0e", "#fde68a"),
            [TagColor.Lime] = new("#65a30d", "#111f0a", "#a3e635", "#ecfccb", "#3f6212", "#d9f99d"),
            [TagColor.Emerald] = new("#059669", "#071f17", "#34d399", "#d1fae5", "#047857", "#a7f3d0"),
            [TagColor.Cyan] = new("#0891b2", "#071a1f", "#22d3ee", "#cffafe", "#0e7490", "#a5f3fc"),
            [TagColor.Sky] = new("#0284c7", "#071826", "#38bdf8", "#e0f2fe", "#0369a1", "#bae6fd"),
            [TagColor.Violet] = new("#7c3aed", "#140d26", "#a78bfa", "#ede9fe", "#6d28d9", "#ddd6fe"),
            [TagColor.Fuchsia] = new("#c026d3", "#1f0a20", "#e879f9", "#fae8ff", "#a21caf", "#f5d0fe"),
            [TagColor.Rose] = new("#e11d48", "#210a12", "#fb7185", "#ffe4e6", "#be123c", "#fecdd3"),
            [TagColor.Amber] = new("#d97706", "#211707", "#fbbf24", "#fef3c7", "#b45309", "#fde68a"),
            [TagColor.Slate] = new("#64748b", "#111827", "#94a3b8", "#f1f5f9", "#475569", "#cbd5e1"),
            [TagColor.Zinc] = new("#71717a", "#18181b", "#a1a1aa", "#f4f4f5", "#52525b", "#d4d4d8"),
            [TagColor.Stone] = new("#78716c", "#1c1917", "#a8a29e", "#f5f5f4", "#57534e", "#d6d3d1"),
            [TagColor.Brown] = new("#a16207", "#1f1307", "#d6a45f", "#f5ead7", "#7c4a03", "#e6c99f"),
            [TagColor.Coral] = new("#e05d44", "#25110d", "#fb8f7b", "#ffe5df", "#b33f2e", "#ffc8bd"),
            [TagColor.Mint] = new("#10b981", "#082016", "#6ee7b7", "#dffced", "#047857", "#b7f7d8")
        };

    public static TagColorPaletteEntry Get(TagColor color)
    {
        return Entries.TryGetValue(color, out var entry)
            ? entry
            : Entries[TagColor.Blue];
    }

    public static IEnumerable<KeyValuePair<TagColor, TagColorPaletteEntry>> All => Entries;
}

public sealed record TagColorPaletteEntry(
    string SelectedBackground,
    string DarkDimBackground,
    string DarkDimForeground,
    string LightDimBackground,
    string LightDimForeground,
    string LightDimBorder);
