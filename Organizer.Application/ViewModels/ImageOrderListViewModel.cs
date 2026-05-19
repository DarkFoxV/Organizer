using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Organizer.Application.ViewModels.Components;

public partial class ImageOrderListViewModel : ObservableObject
{
    [ObservableProperty] private bool _isEmpty = true;

    public ImageOrderListViewModel()
    {
        Items.CollectionChanged += (_, _) =>
        {
            IsEmpty = Items.Count == 0;
            OnPropertyChanged(nameof(CountLabel));
        };
    }

    public ObservableCollection<ImageOrderItemViewModel> Items { get; } = [];

    public string CountLabel => $"Imagens selecionadas ({Items.Count})";

    public async Task AddImageAsync(
        IStorageFile file)
    {
        var vm = new ImageOrderItemViewModel
        {
            Filename = file.Name,
            MimeType = DetectMime(file.Name),
            SourceFile = file
        };

        await AddImageAsync(vm);
    }

    public async Task AddImageAsync(string filename, string mimeType, byte[] data)
    {
        var vm = new ImageOrderItemViewModel
        {
            Filename = filename,
            MimeType = mimeType,
            SourceData = data
        };

        await AddImageAsync(vm);
    }

    private async Task AddImageAsync(ImageOrderItemViewModel vm)
    {

        vm.RemoveRequested += Remove;

        await vm.LoadThumbnailAsync();

        Items.Add(vm);

        OnPropertyChanged(nameof(IsEmpty));
    }
    
    public void Remove(ImageOrderItemViewModel item)
    {
        item.Dispose();

        Items.Remove(item);

        OnPropertyChanged(nameof(IsEmpty));
    }
    
    public void Move(int fromIndex, int toIndex)
    {
        if (fromIndex == toIndex) return;

        if (fromIndex < 0 || toIndex < 0) return;

        if (fromIndex >= Items.Count || toIndex >= Items.Count) return;

        Items.Move(fromIndex, toIndex);
    }

    private static string DetectMime(string filename) =>
        Path.GetExtension(filename).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png"            => "image/png",
            ".gif"            => "image/gif",
            ".webp"           => "image/webp",
            ".bmp"            => "image/bmp",
            _                 => "application/octet-stream"
        };
    
}
