using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Organizer.Application.ViewModels;

namespace Organizer.Application.Components;

public partial class BackgroundOperationHost : UserControl
{
    public BackgroundOperationHost()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<BackgroundOperationHostViewModel>();
    }
}
