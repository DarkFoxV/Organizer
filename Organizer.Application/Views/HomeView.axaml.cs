using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using global::Organizer.Application.Services;
using global::Organizer.Application.ViewModels;

namespace Organizer.Application.Views;

public partial class HomeView : UserControl
{
    public HomeView()
    {
        InitializeComponent();
    }

    private HomeViewModel VM => (HomeViewModel)DataContext!;

    private async void OnWorkspaceCardClick(object? sender, PointerReleasedEventArgs e)
    {
        if (IsInsideActionButton(e.Source as Control, sender as Control))
            return;

        if ((sender as Control)?.DataContext is not HomeWorkspaceItemViewModel workspace)
            return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is null)
            return;

        await VM.OpenWorkspaceAsync(workspace, topLevel.StorageProvider);
    }

    private static bool IsInsideActionButton(Control? source, Control? card)
    {
        for (var current = source; current is not null && !ReferenceEquals(current, card); current = current.GetVisualParent() as Control)
        {
            if (current is Button)
                return true;
        }

        return false;
    }

    private void OnNewWorkspaceClick(object? sender, RoutedEventArgs e)
    {
        VM.NewWorkspace();
    }

    private void OnImportImagesClick(object? sender, RoutedEventArgs e)
    {
        VM.ImportImages();
    }

    private void OnRemoveFromHomeClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;

        if ((sender as Control)?.DataContext is HomeWorkspaceItemViewModel workspace)
            VM.RemoveFromHome(workspace);
    }

    private async void OnDeleteWorkspaceClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;

        if ((sender as Control)?.DataContext is not HomeWorkspaceItemViewModel workspace)
            return;

        if (TopLevel.GetTopLevel(this) is Window owner)
        {
            var confirmed = await ConfirmationDialog.ShowAsync(
                owner,
                AppPreferencesService.Translate("Loc.Home.DeleteTitle"),
                AppPreferencesService.Translate("Loc.Home.DeleteMessage"),
                AppPreferencesService.Translate("Loc.Common.Delete"),
                AppPreferencesService.Translate("Loc.Common.Cancel"),
                isDanger: true);

            if (!confirmed)
                return;
        }

        VM.DeleteWorkspace(workspace);
    }
}
