using Avalonia.Controls;
using Organizer.Application.ViewModels;

namespace Organizer.Application.Components;

public partial class Navbar : UserControl
{
    public Navbar()
    {
        InitializeComponent();
        DataContext = new NavbarViewModel();
    }

    public NavbarViewModel ViewModel => (NavbarViewModel)DataContext!;
}