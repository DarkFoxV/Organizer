using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Organize.Organizer.Core.Enums;
using Organize.Organizer.Core.Interfaces;
using Organizer.Application.Services;

namespace Organizer.Application.ViewModels.Components;

public partial class TagSelectorViewModel : ObservableObject, IDisposable
{
    private readonly ITagService _tagService;
    private readonly AppPreferencesService? _preferencesService;
    private int _loadVersion;
    private bool _isDisposed;

    public ObservableCollection<TagItemViewModel> Tags { get; } = [];

    [ObservableProperty] private bool _showAddButton;

    [ObservableProperty] private bool _showNewTagInput;

    [ObservableProperty] private string _newTagName = string.Empty;

    public IEnumerable<TagItemViewModel> SelectedTags =>
        Tags.Where(t => t.IsSelected);

    public event Action? SelectionChanged;

    public TagSelectorViewModel(ITagService tagService, bool showAddButton = true)
        : this(tagService, null, showAddButton)
    {
    }

    public TagSelectorViewModel(
        ITagService tagService,
        AppPreferencesService? preferencesService,
        bool showAddButton = true)
    {
        _tagService = tagService;
        _preferencesService = preferencesService;
        ShowAddButton = showAddButton;
    }

    public async Task LoadAsync()
    {
        if (_isDisposed)
            return;

        var loadVersion = ++_loadVersion;
        var selectedTagIds = SelectedTags
            .Select(tag => tag.Id)
            .ToHashSet();

        var tags = await _tagService.GetAllAsync();
        if (_isDisposed || loadVersion != _loadVersion)
            return;

        ClearTags();

        foreach (var tag in tags)
        {
            var vm = CreateTagItemViewModel(
                tag.Id,
                tag.Name);
            vm.Color = (TagColor)(int)tag.Color;
            vm.IsSelected = selectedTagIds.Contains(tag.Id);

            vm.Toggled += OnTagToggled;

            Tags.Add(vm);
        }

        OnPropertyChanged(nameof(SelectedTags));
    }

    private TagItemViewModel CreateTagItemViewModel(int id, string name)
    {
        return _preferencesService is null
            ? new TagItemViewModel
            {
                Id = id,
                Name = name
            }
            : new TagItemViewModel(_preferencesService)
            {
                Id = id,
                Name = name
            };
    }

    private void OnTagToggled(TagItemViewModel _)
    {
        if (_isDisposed)
            return;

        OnPropertyChanged(nameof(SelectedTags));
        SelectionChanged?.Invoke();
    }

    public void SetSelectedTagIds(IEnumerable<int> tagIds)
    {
        if (_isDisposed)
            return;

        var selected = tagIds.ToHashSet();

        foreach (var tag in Tags)
            tag.IsSelected = selected.Contains(tag.Id);

        OnPropertyChanged(nameof(SelectedTags));
        SelectionChanged?.Invoke();
    }

    [RelayCommand]
    private void OpenNewTagInput()
    {
        if (_isDisposed)
            return;

        ShowNewTagInput = true;
        NewTagName = string.Empty;
    }

    [RelayCommand]
    private void CancelNewTag()
    {
        if (_isDisposed)
            return;

        ShowNewTagInput = false;
        NewTagName = string.Empty;
    }

    [RelayCommand]
    private async Task ConfirmNewTag()
    {
        if (_isDisposed)
            return;

        if (string.IsNullOrWhiteSpace(NewTagName)) return;

        try
        {
            var tag = await _tagService.CreateAsync(
                NewTagName.Trim(),
                TagColor.Blue);
            if (_isDisposed)
                return;

            var vm = CreateTagItemViewModel(
                tag.Id,
                tag.Name);
            vm.Color = TagColor.Blue;
            vm.Toggled += OnTagToggled;
            Tags.Add(vm);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao criar tag: {ex.Message}");
        }
        finally
        {
            if (!_isDisposed)
            {
                ShowNewTagInput = false;
                NewTagName = string.Empty;
            }
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _loadVersion++;
        ClearTags();
        SelectionChanged = null;
    }

    private void ClearTags()
    {
        foreach (var tag in Tags)
        {
            tag.Toggled -= OnTagToggled;
            tag.Dispose();
        }

        Tags.Clear();
    }
}
