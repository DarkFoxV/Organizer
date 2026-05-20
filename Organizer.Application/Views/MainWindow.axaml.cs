namespace Organizer.Application.Views;

using Avalonia.Controls;
using global::Organizer.Application.ViewModels;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(MainWindowViewModel vm)
        : this()
    {
        DataContext = vm;
    }
}