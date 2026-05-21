using System;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Organize.Organizer.Core.Interfaces;
using Organizer.Application;
using Organizer.Application.Services;
using Organizer.Application.ViewModels;
using Organizer.Application.ViewModels.Components;

namespace Organizer;

internal class Program
{
    private static IServiceProvider Services { get; set; } = null!;

    [STAThread]
    public static void Main(string[] args)
    {
        SQLitePCL.Batteries.Init();

        var services = new ServiceCollection();

        // ─────────────────────────────
        // DB
        // ─────────────────────────────

        services.AddSingleton<AppDbContextFactory>();

        // ─────────────────────────────
        // SERVICES
        // ─────────────────────────────

        services.AddSingleton<AppPreferencesService>();
        services.AddTransient<ICardService, CardService>();
        services.AddTransient<IImageService, ImageService>();
        services.AddTransient<ITagService, TagService>();
        services.AddSingleton<IClipboardService, ClipboardService>();

        // ─────────────────────────────
        // VIEW-MODELS (ROOT)
        // ─────────────────────────────

        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<NavbarViewModel>();
        services.AddTransient<HomeViewModel>();
        services.AddTransient<SearchViewModel>();
        services.AddTransient<WorkspaceViewModel>();
        services.AddTransient<PreferencesViewModel>();
        services.AddTransient<ManageTagsViewModel>();
        services.AddTransient<RegisterViewModel>();
        services.AddTransient<EditViewModel>();

        // ─────────────────────────────
        // VIEW-MODELS (COMPONENTS)
        // ─────────────────────────────

        services.AddTransient<SearchBarViewModel>();
        services.AddTransient<PaginationViewModel>();
        services.AddTransient<ImagePreviewViewModel>();

        services.AddTransient<TagSelectorViewModel>();
        services.AddTransient<TagItemViewModel>();
        services.AddTransient<TagRowViewModel>();

        services.AddTransient<ImageOrderListViewModel>();
        services.AddTransient<ImageOrderItemViewModel>();
        services.AddTransient<CardItemViewModel>();

        // ─────────────────────────────

        using var serviceProvider = services.BuildServiceProvider();

        Services = serviceProvider;
        App.Services = Services;

        var dbFactory = Services.GetRequiredService<AppDbContextFactory>();
        using (var lease = dbFactory.CreateLeaseAsync().GetAwaiter().GetResult())
        {
            lease.Context.Database.EnsureCreated();
        }

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    private static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
