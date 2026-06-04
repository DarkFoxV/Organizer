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
        services.AddSingleton<IAppLogger, TraceAppLogger>();
        services.AddSingleton<ITokenStorage, PreferencesTokenStorage>();
        services.AddSingleton<WorkspaceArchiveService>();
        services.AddSingleton<IStartupFileService, StartupFileService>();
        services.AddSingleton<IWorkspaceStartupService, WorkspaceStartupService>();
        services.AddSingleton<HomeWorkspaceCacheService>();
        services.AddSingleton<DatabaseFileService>();
        services.AddSingleton<BackupService>();
        services.AddSingleton<GoogleDriveOAuthService>();
        services.AddSingleton<LocalBackupStorageProvider>();
        services.AddSingleton<GoogleDriveBackupStorageProvider>();
        services.AddSingleton<IBackupStorageProvider>(sp => sp.GetRequiredService<LocalBackupStorageProvider>());
        services.AddSingleton<IBackupStorageProvider>(sp => sp.GetRequiredService<GoogleDriveBackupStorageProvider>());
        services.AddSingleton<ToastService>();
        services.AddSingleton<IToastService>(sp => sp.GetRequiredService<ToastService>());
        services.AddSingleton<IToastNotificationStore>(sp => sp.GetRequiredService<ToastService>());
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
        services.AddSingleton<WorkspaceViewModel>();
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
            .WithInterFont()
            .LogToTrace()
            .With(new SkiaOptions
            {
                MaxGpuResourceSizeBytes = 512 * 1024 * 1024
            });
}
