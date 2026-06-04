using System;
using Organizer.Application.Services;
using Organizer.Application.Views;
using Organizer.Application.ViewModels;

namespace Organizer.Application;

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;

public class App : Application
{
    public static IServiceProvider Services { get; set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var vm = Services.GetRequiredService<MainWindowViewModel>();
            var startupService = Services.GetRequiredService<IWorkspaceStartupService>();
            var mainWindow = new MainWindow(vm);

            desktop.MainWindow = mainWindow;
            _ = startupService.ApplyAsync(desktop.Args, mainWindow, vm);
        }
        #if DEBUG
                this.AttachDeveloperTools();
        #endif
        base.OnFrameworkInitializationCompleted();
    }
}
