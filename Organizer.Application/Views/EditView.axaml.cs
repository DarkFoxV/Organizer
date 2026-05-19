using Avalonia.Controls;
using Organizer.Application.ViewModels;

namespace Organizer.Application.Views;

public partial class EditView : UserControl
{
    public EditView()
    {
        InitializeComponent();

        AttachedToVisualTree += (_, _) =>
        {
            if (DataContext is EditViewModel vm)
                TagSelectorControl.DataContext = vm.TagSelector;
        };
    }
}
