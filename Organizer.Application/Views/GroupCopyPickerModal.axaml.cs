using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using Organizer.Application.Services;
using Organizer.Application.ViewModels;

namespace Organizer.Application.Views;

public partial class GroupCopyPickerModal : UserControl
{
    private static IClipboardService ClipboardService =>
        global::Organizer.Application.App.Services.GetRequiredService<IClipboardService>();

    public GroupCopyPickerModal()
    {
        InitializeComponent();
    }

    private async void OnCopyImage(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not GroupCopyPickerViewModel vm)
            return;

        if (sender is not Control { DataContext: GroupCopyPickerItemViewModel item })
            return;

        var compactLargeImage = false;

        try
        {
            await using var data = await vm.OpenImageDataStreamAsync(item.Id);
            if (!vm.IsVisible || !ReferenceEquals(DataContext, vm))
                return;

            if (data is null || (data.CanSeek && data.Length == data.Position))
                return;

            compactLargeImage = data.CanSeek && data.Length >= 85_000;

            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is null)
                return;

            if (await ClipboardService.SetImageAsync(clipboard, data, item.MimeType))
                vm.CloseWithoutMemoryCompaction();
        }
        catch (System.Exception ex)
        {
            System.Console.WriteLine($"[GroupCopyPickerModal.OnCopyImage] {ex}");
        }
        finally
        {
            if (compactLargeImage)
                MemoryCleanupService.QueueLargeImageMemoryCompaction();
        }
    }
}
