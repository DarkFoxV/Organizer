using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Organizer.Application.ViewModels;

namespace Organizer.Application.Services;

public interface IWorkspaceStartupService
{
    Task ApplyAsync(
        IEnumerable<string>? args,
        Window mainWindow,
        MainWindowViewModel mainWindowViewModel);
}

public sealed class WorkspaceStartupService : IWorkspaceStartupService
{
    private readonly IStartupFileService _startupFileService;
    private readonly WorkspaceViewModel _workspaceViewModel;

    public WorkspaceStartupService(
        IStartupFileService startupFileService,
        WorkspaceViewModel workspaceViewModel)
    {
        _startupFileService = startupFileService;
        _workspaceViewModel = workspaceViewModel;
    }

    public async Task ApplyAsync(
        IEnumerable<string>? args,
        Window mainWindow,
        MainWindowViewModel mainWindowViewModel)
    {
        var launchContext = _startupFileService.GetLaunchContext(args);

        if (launchContext.Mode != StartupLaunchMode.Workspace
            || string.IsNullOrWhiteSpace(launchContext.WorkspacePath))
        {
            return;
        }

        try
        {
            // Native .owsp association on Windows/Linux/macOS should point to
            // Organizer and pass the selected file path in argv. This is the
            // expansion point for future multi-file activation and startup
            // drag-and-drop handling.
            var file = await mainWindow.StorageProvider.TryGetFileFromPathAsync(new Uri(launchContext.WorkspacePath));
            if (file is null)
                return;

            if (await _workspaceViewModel.OpenWorkspaceFileAsync(file))
                mainWindowViewModel.ShowWorkspace();
        }
        catch
        {
            // Invalid startup input must never block a normal app launch.
            // The fallback stays as Home, which is already the default view.
        }
    }
}
