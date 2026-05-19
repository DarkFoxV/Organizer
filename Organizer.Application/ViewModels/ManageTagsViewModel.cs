using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Organize.Organizer.Core.Enums;
using Organize.Organizer.Core.Interfaces;
using Organizer.Application.ViewModels.Components;

namespace Organizer.Application.ViewModels;

public partial class ManageTagsViewModel : ObservableObject
{
    private readonly ITagService _tagService;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private TagColor _newTagColor = TagColor.Blue;

    // ── Nova tag ──────────────────────────────────────────────────────────────

    [ObservableProperty]
    private string _newTagName = string.Empty;

    // ── Init ──────────────────────────────────────────────────────────────────

    public ManageTagsViewModel(ITagService tagService)
    {
        _tagService = tagService;
        _ = LoadTagsAsync();
    }

    // ── Dados ─────────────────────────────────────────────────────────────────

    public ObservableCollection<TagRowViewModel> Tags { get; } = [];

    public IEnumerable<TagColor> ColorOptions => Enum.GetValues<TagColor>();

    // ── Load ──────────────────────────────────────────────────────────────────

    private async Task LoadTagsAsync()
    {
        IsLoading = true;

        Tags.Clear();

        var tags = await _tagService.GetAllAsync();

        foreach (var tag in tags)
        {
            AddRow(tag.Id, tag.Name, tag.Color);
        }

        IsLoading = false;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void AddRow(int id, string name, TagColor color)
    {
        var row = new TagRowViewModel
        {
            Id = id,
            Name = name,
            Color = color,
            EditName = name,
            EditColor = color
        };

        row.SaveRequested += OnSaveTag;
        row.DeleteRequested += OnDeleteTag;

        Tags.Add(row);
    }

    // ── Handlers ──────────────────────────────────────────────────────────────

    private async void OnSaveTag(TagRowViewModel row)
    {
        await _tagService.RenameAsync(row.Id, row.EditName);

        var updated = await _tagService.ChangeColorAsync(
            row.Id,
            row.EditColor);

        row.Name = updated.Name;
        row.Color = updated.Color;
    }

    private async void OnDeleteTag(TagRowViewModel row)
    {
        await _tagService.DeleteAsync(row.Id);

        Tags.Remove(row);
    }

    // ── Criar nova tag ────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task CreateTag()
    {
        if (string.IsNullOrWhiteSpace(NewTagName))
            return;

        var tag = await _tagService.CreateAsync(
            NewTagName,
            NewTagColor);

        AddRow(tag.Id, tag.Name, tag.Color);

        NewTagName = string.Empty;
        NewTagColor = TagColor.Blue;
    }
}
