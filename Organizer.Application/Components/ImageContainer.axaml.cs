using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Organizer.Application.ViewModels.Components;

namespace Organizer.Application.Components;

public partial class ImageContainer : UserControl
{
    private static Bitmap? _clipboardBitmap;

    public ImageContainer()
    {
        InitializeComponent();
    }

    private async void OnCopyImage(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not CardItemViewModel vm)
            return;

        if (vm.IsGroup)
        {
            vm.RequestCopy();
            return;
        }

        var imageData = vm.ImageData;
        if ((imageData is null || imageData.Length == 0) && vm.LoadImageDataAsync is not null)
            imageData = await vm.LoadImageDataAsync();

        if (imageData is null || imageData.Length == 0)
            return;

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
            return;

        await using var stream = new MemoryStream(imageData);
        var bitmap = new Bitmap(stream);

        var previousBitmap = _clipboardBitmap;
        _clipboardBitmap = bitmap;

        await clipboard.SetBitmapAsync(bitmap);

        previousBitmap?.Dispose();
    }
}
