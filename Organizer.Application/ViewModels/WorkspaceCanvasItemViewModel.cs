using System;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Organizer.Application.ViewModels;

public partial class WorkspaceCanvasItemViewModel : ObservableObject, IDisposable
{
    public const double SelectionChromePadding = 28;
    public const double SelectionBorderThickness = 12;

    [ObservableProperty] private Bitmap? _bitmap;

    [ObservableProperty] private double _x;

    [ObservableProperty] private double _y;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ContainerX))]
    private double _displayX;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ContainerY))]
    private double _displayY;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ContainerWidth), nameof(ImageMargin))]
    private double _width;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ContainerHeight), nameof(ImageMargin))]
    private double _height;

    [ObservableProperty] private int _zIndex;

    [ObservableProperty] private bool _isSelected;

    public string Label { get; init; } = string.Empty;

    public double OriginalWidth { get; init; }

    public double OriginalHeight { get; init; }

    public double ContainerX => DisplayX - SelectionChromePadding;

    public double ContainerY => DisplayY - SelectionChromePadding;

    public double ContainerWidth => Width + SelectionChromePadding * 2;

    public double ContainerHeight => Height + SelectionChromePadding * 2;

    public Avalonia.Thickness ImageMargin => new(SelectionChromePadding);

    public Avalonia.Thickness SelectionBorderThicknessValue => new(SelectionBorderThickness);

    public Avalonia.Thickness SelectionBorderMargin => new(SelectionChromePadding - SelectionBorderThickness / 2);

    public event Action<WorkspaceCanvasItemViewModel>? RemoveRequested;

    public void Dispose()
    {
        Bitmap?.Dispose();
        Bitmap = null;
    }

    [RelayCommand]
    private void Remove()
    {
        RemoveRequested?.Invoke(this);
    }
}
