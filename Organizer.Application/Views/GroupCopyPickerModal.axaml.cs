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

        var data = await vm.LoadImageDataAsync(item.Id);
        if (data is null || data.Length == 0)
            return;

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
            return;

        if (await ClipboardService.SetImageAsync(clipboard, data, item.MimeType))
            vm.CloseCommand.Execute(null);
    }
}
