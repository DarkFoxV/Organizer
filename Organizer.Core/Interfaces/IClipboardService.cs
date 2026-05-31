using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Input.Platform;

namespace Organizer.Application.Services;

public interface IClipboardService
{
    Task<IReadOnlyList<ClipboardImageData>> GetImagesAsync(IClipboard clipboard);

    Task<bool> SetImageAsync(
        IClipboard clipboard,
        Stream imageStream,
        string? mimeType = null);
}
