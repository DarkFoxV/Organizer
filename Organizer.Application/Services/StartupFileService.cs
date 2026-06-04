using System;
using System.Collections.Generic;
using System.IO;

namespace Organizer.Application.Services;

public enum StartupLaunchMode
{
    Home,
    Workspace
}

public sealed record StartupLaunchContext(
    StartupLaunchMode Mode,
    string? WorkspacePath = null);

public interface IStartupFileService
{
    StartupLaunchContext GetLaunchContext(IEnumerable<string>? args);
}

public sealed class StartupFileService : IStartupFileService
{
    public StartupLaunchContext GetLaunchContext(IEnumerable<string>? args)
    {
        // Desktop file associations launch the app with the opened file path as a
        // command-line argument. The same parser can later receive paths from
        // OS-level drag-and-drop or multi-file startup activation.
        foreach (var arg in args ?? [])
        {
            if (string.IsNullOrWhiteSpace(arg))
                continue;

            try
            {
                var workspacePath = NormalizePath(arg.Trim());
                if (IsValidWorkspacePath(workspacePath))
                    return new StartupLaunchContext(StartupLaunchMode.Workspace, workspacePath);
            }
            catch
            {
                // Malformed startup arguments fall back to Home.
            }
        }

        return new StartupLaunchContext(StartupLaunchMode.Home);
    }

    private static bool IsValidWorkspacePath(string path)
    {
        return string.Equals(Path.GetExtension(path), WorkspaceArchiveService.Extension, StringComparison.OrdinalIgnoreCase)
            && File.Exists(path);
    }

    private static string NormalizePath(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.IsFile
            ? uri.LocalPath
            : Path.GetFullPath(value);
    }
}
