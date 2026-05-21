using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Organize.Organizer.Core.Enums;
using Organize.Organizer.Core.Interfaces;

namespace Organizer.Application.ViewModels.Components;

public partial class TagSelectorViewModel : ObservableObject
{
    private readonly ITagService _tagService;

    public ObservableCollection<TagItemViewModel> Tags { get; } = [];

    [ObservableProperty] private bool _showAddButton;

    [ObservableProperty] private bool _showNewTagInput;

    [ObservableProperty] private string _newTagName = string.Empty;

    public IEnumerable<TagItemViewModel> SelectedTags =>
        Tags.Where(t => t.IsSelected);

    // Disparado sempre que qualquer tag é selecionada/deselecionada
    public event Action? SelectionChanged;

    public TagSelectorViewModel(ITagService tagService, bool showAddButton = true)
    {
        _tagService = tagService;
        ShowAddButton = showAddButton;
    }

    // Carrega tags do banco e monta os ViewModels
    public async Task LoadAsync()
    {
        var selectedTagIds = SelectedTags
            .Select(tag => tag.Id)
            .ToHashSet();

        var tags = await _tagService.GetAllAsync();

        Tags.Clear();

        foreach (var tag in tags)
        {
            var vm = new TagItemViewModel
            {
                Id = tag.Id,
                Name = tag.Name,
                Color = (TagColor)(int)tag.Color,
                IsSelected = selectedTagIds.Contains(tag.Id)
            };

            vm.Toggled += _ =>
            {
                OnPropertyChanged(nameof(SelectedTags));
                SelectionChanged?.Invoke();
            };

            Tags.Add(vm);
        }

        OnPropertyChanged(nameof(SelectedTags));
    }

    public void SetSelectedTagIds(IEnumerable<int> tagIds)
    {
        var selected = tagIds.ToHashSet();

        foreach (var tag in Tags)
            tag.IsSelected = selected.Contains(tag.Id);

        OnPropertyChanged(nameof(SelectedTags));
        SelectionChanged?.Invoke();
    }

    [RelayCommand]
    private void OpenNewTagInput()
    {
        ShowNewTagInput = true;
        NewTagName = string.Empty;
    }

    [RelayCommand]
    private void CancelNewTag()
    {
        ShowNewTagInput = false;
        NewTagName = string.Empty;
    }

    [RelayCommand]
    private async Task ConfirmNewTag()
    {
        if (string.IsNullOrWhiteSpace(NewTagName)) return;

        try
        {
            var tag = await _tagService.CreateAsync(
                NewTagName.Trim(),
                TagColor.Blue);

            var vm = new TagItemViewModel
            {
                Id = tag.Id,
                Name = tag.Name,
                Color = TagColor.Blue,
            };
            vm.Toggled += _ =>
            {
                OnPropertyChanged(nameof(SelectedTags));
                SelectionChanged?.Invoke();
            };
            Tags.Add(vm);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao criar tag: {ex.Message}");
        }
        finally
        {
            ShowNewTagInput = false;
            NewTagName = string.Empty;
        }
    }
}