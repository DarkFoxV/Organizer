using System.IO;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Organizer.Application.ViewModels;

namespace Organizer.Application.Views;

public partial class GroupCopyPickerModal : UserControl
{
    private static Bitmap? _clipboardBitmap;

    public GroupCopyPickerModal()
    {
        InitializeComponent();
    }

    private async void OnCopyCurrentImage(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not GroupCopyPickerViewModel vm)
            return;

        var data = await vm.LoadCurrentImageDataAsync();
        if (data is null || data.Length == 0)
            return;

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
            return;

        await using var stream = new MemoryStream(data);
        var bitmap = new Bitmap(stream);

        var previousBitmap = _clipboardBitmap;
        _clipboardBitmap = bitmap;

        await ClipboardExtensions.SetBitmapAsync(clipboard, bitmap);

        previousBitmap?.Dispose();
        vm.CloseCommand.Execute(null);
    }
}
