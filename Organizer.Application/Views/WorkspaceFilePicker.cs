using Avalonia.Platform.Storage;
using Organizer.Application.Services;

namespace Organizer.Application.Views;

internal static class WorkspaceFilePicker
{
    public static FilePickerFileType WorkspaceFileType { get; } = new("Organizer workspace")
    {
        Patterns = ["*.owsp"],
        MimeTypes = ["application/zip", "application/x-zip-compressed"]
    };

    public static FilePickerSaveOptions CreateSaveOptions()
    {
        return new FilePickerSaveOptions
        {
            Title = AppPreferencesService.Translate("Loc.Workspace.SaveTitle"),
            SuggestedFileName = "workspace.owsp",
            DefaultExtension = "owsp",
            FileTypeChoices = [WorkspaceFileType]
        };
    }
}
