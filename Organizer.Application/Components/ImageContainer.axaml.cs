using System.IO;
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

    private static IToastService ToastService =>
        App.Services.GetRequiredService<IToastService>();

    public ImageContainer()
    {
        InitializeComponent();
    }

    private async void OnCopyImage(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not CardItemViewModel vm)
            return;

        var compactLargeImage = false;

        try
        {
            if (vm.IsGroup)
            {
                vm.RequestCopy();
                return;
            }

            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is null)
            {
                ShowCopyFailedToast();
                return;
            }

            var imageData = vm.ImageData;
            compactLargeImage = imageData is { Length: >= 85_000 };

            if (imageData is { Length: > 0 })
            {
                using var imageStream = new MemoryStream(imageData, writable: false);
                ShowCopyResultToast(await ClipboardService.SetImageAsync(clipboard, imageStream, vm.MimeType));
                return;
            }

            if (vm.LoadImageDataStreamAsync is null)
            {
                ShowCopyFailedToast();
                return;
            }

            await using var loadedImageStream = await vm.LoadImageDataStreamAsync();
            if (!ReferenceEquals(DataContext, vm))
                return;

            if (loadedImageStream is null || (loadedImageStream.CanSeek && loadedImageStream.Length == loadedImageStream.Position))
                return;

            compactLargeImage = loadedImageStream.CanSeek && loadedImageStream.Length >= 85_000;
            ShowCopyResultToast(await ClipboardService.SetImageAsync(clipboard, loadedImageStream, vm.MimeType));
        }
        catch (System.Exception ex)
        {
            System.Console.WriteLine($"[ImageContainer.OnCopyImage] {ex}");
            ShowCopyFailedToast();
        }
        finally
        {
            if (compactLargeImage)
                MemoryCleanupService.QueueLargeImageMemoryCompaction();
        }
    }

    private static void ShowCopyResultToast(bool copied)
    {
        if (copied)
        {
            ToastService.Success(
                AppPreferencesService.Translate("Loc.Search.ToastImageCopiedTitle"),
                AppPreferencesService.Translate("Loc.Search.ToastImageCopiedMessage"));
            return;
        }

        ShowCopyFailedToast();
    }

    private static void ShowCopyFailedToast()
    {
        ToastService.Error(
            AppPreferencesService.Translate("Loc.Search.ToastCopyFailedTitle"),
            AppPreferencesService.Translate("Loc.Search.ToastCopyFailedMessage"));
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
