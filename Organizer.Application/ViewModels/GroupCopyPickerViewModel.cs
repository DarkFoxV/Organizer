using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Organize.Organizer.Core;
using Organizer.Core.Helpers;

namespace Organizer.Application.ViewModels;

public partial class GroupCopyPickerViewModel : ObservableObject
{
    private Func<int, Task<byte[]?>>? _loadImageDataAsync;

    [ObservableProperty] private bool _isVisible;

    [ObservableProperty] private string _title = "Copiar imagem do grupo";

    public ObservableCollection<GroupCopyPickerItemViewModel> Items { get; } = [];

    public async Task OpenAsync(
        IEnumerable<GroupImageSummary> images,
        Func<int, Task<byte[]?>> loadImageDataAsync)
    {
        _loadImageDataAsync = loadImageDataAsync;

        ClearItems();
        var items = await Task.Run(() =>
        {
            return images
                .Select((image, index) => new GroupCopyPickerItemViewModel
                {
                    Id = image.Id,
                    Index = index,
                    Thumbnail = ImageHelper.ToBitmap(image.Thumbnail, maxWidth: 220, maxHeight: 160),
                    Filename = image.Filename,
                    MimeType = image.MimeType ?? "application/octet-stream",
                    Description = image.Description
                })
                .ToList();
        });

        foreach (var item in items)
            Items.Add(item);

        OnPropertyChanged(nameof(CountText));
        IsVisible = true;
    }

    public async Task<byte[]?> LoadImageDataAsync(int imageId)
    {
        return _loadImageDataAsync is null
            ? null
            : await _loadImageDataAsync(imageId);
    }

    public string CountText => Items.Count == 1
        ? "1 imagem disponivel"
        : $"{Items.Count} imagens disponiveis";

    [RelayCommand]
    private void Close()
    {
        IsVisible = false;
        _loadImageDataAsync = null;
        ClearItems();
    }

    private void ClearItems()
    {
        foreach (var item in Items)
            item.Dispose();

        Items.Clear();
        OnPropertyChanged(nameof(CountText));
    }
}