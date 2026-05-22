namespace Organizer.Application.Views;

using Avalonia.Controls;
using global::Organizer.Application.ViewModels;

public partial class MainWindow : Window
{
    private bool _closeConfirmed;

    public MainWindow()
    {
        InitializeComponent();
        Closing += OnClosing;
    }

    public MainWindow(MainWindowViewModel vm)
        : this()
    {
        DataContext = vm;
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_closeConfirmed)
            return;

        if (DataContext is not MainWindowViewModel { HasUnsavedWorkspaceChanges: true })
            return;

        e.Cancel = true;

        var canClose = await ConfirmationDialog.ShowAsync(
            this,
            "Fechar Organizer",
            "Existem imagens abertas no workspace. Fechar o app vai descartar esse canvas se ele nao foi salvo.",
            "Fechar",
            "Cancelar",
            isDanger: true);

        if (!canClose)
            return;

        _closeConfirmed = true;
        Close();
    }
}
