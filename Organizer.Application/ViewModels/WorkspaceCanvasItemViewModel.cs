using System;
using Avalonia;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Organizer.Application.ViewModels;

public partial class WorkspaceCanvasItemViewModel : ObservableObject, IDisposable
{
    public const double SelectionChromePadding = 28;
    public const double SelectionBorderThickness = 12;

    [ObservableProperty] private Bitmap? _bitmap;

    [ObservableProperty] private double _x;

    [ObservableProperty] private double _y;

    [ObservableProperty] private double _displayX;

    [ObservableProperty] private double _displayY;

    [ObservableProperty] private double _width;

    [ObservableProperty] private double _height;

    [ObservableProperty] private int _zIndex;

    [ObservableProperty] private bool _isSelected;

    [ObservableProperty] private Bitmap? _thumbnailBitmap;

    [ObservableProperty] private Bitmap? _halfBitmap;

    [ObservableProperty] private Bitmap? _quarterBitmap;

    public string Label { get; init; } = string.Empty;

    public string MimeType { get; init; } = "image/png";

    public byte[] ImageData { get; init; } = [];

    public double OriginalWidth { get; init; }

    public double OriginalHeight { get; init; }

    public Rect Bounds => new(X, Y, Width, Height);

    public void Dispose()
    {
        var bitmap = Bitmap;
        Bitmap = null;
        bitmap?.Dispose();

        var thumb = ThumbnailBitmap;
        ThumbnailBitmap = null;
        thumb?.Dispose();

        var half = HalfBitmap;
        HalfBitmap = null;
        half?.Dispose();

        var quarter = QuarterBitmap;
        QuarterBitmap = null;
        quarter?.Dispose();
    }
}
