using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Organizer.Application.Services;
using Organizer.Application.ViewModels.Components;

namespace Organizer.Application.Components;

public partial class ImageContainer : UserControl
{
    private static IClipboardService ClipboardService =>
        App.Services.GetRequiredService<IClipboardService>();

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

        await ClipboardService.SetImageAsync(clipboard, imageData, vm.MimeType);
    }

    private void OnCardPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left)
            return;

        if (e.Source is Control control && control.FindAncestorOfType<Button>() is not null)
            return;

        if (DataContext is CardItemViewModel vm)
            vm.ViewCommand.Execute(null);
    }
}
