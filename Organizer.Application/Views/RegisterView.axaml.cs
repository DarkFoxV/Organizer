using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Organizer.Application.ViewModels;

namespace Organizer.Application.Views;

public partial class RegisterView : UserControl
{
    private TopLevel? _topLevel;

    public RegisterView()
    {
        InitializeComponent();

        AttachedToVisualTree += (_, _) =>
        {
            _topLevel = TopLevel.GetTopLevel(this);
            if (_topLevel is not null)
                _topLevel.AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);

            if (DataContext is RegisterViewModel vm)
            {
                ImageOrderListControl.DataContext = vm.ImageOrder;
                TagSelectorControl.DataContext = vm.TagSelector;
            }
        };

        DetachedFromVisualTree += (_, _) =>
        {
            if (_topLevel is not null)
            {
                _topLevel.RemoveHandler(KeyDownEvent, OnKeyDown);
                _topLevel = null;
            }

            if (DataContext is RegisterViewModel vm)
                vm.Dispose();
        };
    }

    private RegisterViewModel VM => (RegisterViewModel)DataContext!;

    private async void OnPickImages(object? sender, RoutedEventArgs e)
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null) return;
        await VM.PickImagesCommand.ExecuteAsync(storage);
    }

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.V || !e.KeyModifiers.HasFlag(KeyModifiers.Control))
            return;

        var clipboard = _topLevel?.Clipboard ?? TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
            return;

        if (await VM.TryPasteImagesAsync(clipboard))
            e.Handled = true;
    }
}
