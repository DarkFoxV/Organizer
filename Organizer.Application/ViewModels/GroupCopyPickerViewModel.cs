using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Organize.Organizer.Core;
using Organizer.Application.Services;
using Organizer.Core.Helpers;

namespace Organizer.Application.ViewModels;

public partial class GroupCopyPickerViewModel : ObservableObject, IDisposable
{
    private readonly AppPreferencesService _preferencesService;
    private Func<int, Task<Stream?>>? _openImageDataStreamAsync;
    private int _loadVersion;
    private bool _isDisposed;

    [ObservableProperty] private bool _isVisible;

    public string Title => _preferencesService.T("Loc.CopyPicker.Title");

    public ObservableCollection<GroupCopyPickerItemViewModel> Items { get; } = [];

    public GroupCopyPickerViewModel(AppPreferencesService preferencesService)
    {
        _preferencesService = preferencesService;
        _preferencesService.PreferencesChanged += OnPreferencesChanged;
    }

    public async Task OpenAsync(
        IEnumerable<GroupImageSummary> images,
        Func<int, Task<Stream?>>? openImageDataStreamAsync)
    {
        if (_isDisposed)
            return;

        var loadVersion = ++_loadVersion;
        _openImageDataStreamAsync = openImageDataStreamAsync;

        ClearItems();
        var items = await Task.Run(() => CreateItems(images));

        if (loadVersion != _loadVersion)
        {
            foreach (var item in items)
                item.Dispose();

            return;
        }

        foreach (var item in items)
            Items.Add(item);

        OnPropertyChanged(nameof(CountText));
        IsVisible = true;
    }

    public async Task<Stream?> OpenImageDataStreamAsync(int imageId)
    {
        return _isDisposed || _openImageDataStreamAsync is null
            ? null
            : await _openImageDataStreamAsync(imageId);
    }

    public string CountText => Items.Count == 1
        ? _preferencesService.T("Loc.CopyPicker.CountOne")
        : _preferencesService.T("Loc.CopyPicker.CountMany", Items.Count);

    [RelayCommand]
    public void Close()
    {
        CloseCore(queueMemoryCompaction: true);
    }

    public void CloseWithoutMemoryCompaction()
    {
        CloseCore(queueMemoryCompaction: false);
    }

    private void CloseCore(bool queueMemoryCompaction)
    {
        var hadImageResources = IsVisible || Items.Count > 0;

        _loadVersion++;
        IsVisible = false;
        _openImageDataStreamAsync = null;
        ClearItems();

        if (queueMemoryCompaction && hadImageResources)
            MemoryCleanupService.QueueLargeImageMemoryCompaction();
    }

    private void ClearItems()
    {
        var items = Items.ToList();

        Items.Clear();

        foreach (var item in items)
            item.Dispose();

        OnPropertyChanged(nameof(CountText));
    }

    private static List<GroupCopyPickerItemViewModel> CreateItems(IEnumerable<GroupImageSummary> images)
    {
        var items = new List<GroupCopyPickerItemViewModel>();

        try
        {
            foreach (var (image, index) in images.Select((image, index) => (image, index)))
            {
                items.Add(new GroupCopyPickerItemViewModel
                {
                    Id = image.Id,
                    Index = index,
                    Thumbnail = ImageHelper.ToBitmap(image.Thumbnail, maxWidth: 220, maxHeight: 160),
                    Filename = image.Filename,
                    MimeType = image.MimeType ?? "application/octet-stream",
                    Description = image.Description
                });
            }

            return items;
        }
        catch
        {
            foreach (var item in items)
                item.Dispose();

            throw;
        }
    }

    private void OnPreferencesChanged()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(CountText));
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        Close();
        _preferencesService.PreferencesChanged -= OnPreferencesChanged;
        _isDisposed = true;
    }
}
