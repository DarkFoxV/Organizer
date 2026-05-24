using Avalonia.Controls;
using Organizer.Application.ViewModels;

namespace Organizer.Organizer.Application.Views;

public partial class PreferencesView : UserControl
{
    public PreferencesView()
    {
        InitializeComponent();
        DetachedFromVisualTree += (_, _) =>
        {
            if (DataContext is PreferencesViewModel vm)
                vm.Dispose();
        };
    }
}
