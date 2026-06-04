using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Organize.Organizer.Core.Enums;
using Organizer.Application.Services;

namespace Organizer.Application.Converters;

public sealed class TagColorBrushConverter : IValueConverter
{
    public static readonly TagColorBrushConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var color = value is TagColor tagColor
            ? tagColor
            : TagColor.Blue;

        return new SolidColorBrush(Color.Parse(TagColorPalette.Get(color).SelectedBackground));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
