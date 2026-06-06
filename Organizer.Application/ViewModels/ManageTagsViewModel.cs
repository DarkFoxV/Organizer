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
using Organizer.Application.ViewModels.Components;

namespace Organizer.Application.ViewModels;

public partial class ManageTagsViewModel : ObservableObject
{
    private readonly ITagService _tagService;
    private readonly AppPreferencesService _preferencesService;

    [ObservableProperty] private bool _isLoading;

    [ObservableProperty] private TagColor _newTagColor = TagColor.Blue;

    [ObservableProperty] private int _tagCount;

    [ObservableProperty] private int _taggedImageCount;

    [ObservableProperty] private bool _isDeleteConfirmationVisible;

    [ObservableProperty] private TagRowViewModel? _pendingDeleteTag;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VisibleTags))]
    [NotifyPropertyChangedFor(nameof(HasVisibleTags))]
    [NotifyPropertyChangedFor(nameof(ShowFilterEmpty))]
    private string _filterText = string.Empty;

    // ── Nova tag ──────────────────────────────────────────────────────────────

    [ObservableProperty] private string _newTagName = string.Empty;

    // ── Init ──────────────────────────────────────────────────────────────────

    public ManageTagsViewModel(
        ITagService tagService,
        AppPreferencesService preferencesService)
    {
        _tagService = tagService;
        _preferencesService = preferencesService;
        _preferencesService.PreferencesChanged += OnPreferencesChanged;
        _ = LoadTagsAsync();
    }

    // ── Dados ─────────────────────────────────────────────────────────────────

    public ObservableCollection<TagRowViewModel> Tags { get; } = [];

    public IEnumerable<TagColor> ColorOptions => Enum.GetValues<TagColor>();

    public bool HasTags => Tags.Count > 0;

    public IEnumerable<TagRowViewModel> VisibleTags
    {
        get
        {
            if (string.IsNullOrWhiteSpace(FilterText))
                return Tags;

            return Tags.Where(tag =>
                tag.Name.Contains(FilterText, StringComparison.CurrentCultureIgnoreCase));
        }
    }

    public bool HasVisibleTags => VisibleTags.Any();

    public bool ShowFilterEmpty => HasTags && !HasVisibleTags;

    public int ActiveTagCount => Tags.Count(tag => !tag.IsUnused);

    public int UnusedTagCount => Tags.Count(tag => tag.IsUnused);

    public string CollectionStatsText => _preferencesService.T("Loc.Tags.CollectionStats", TagCount, TaggedImageCount);

    public string CollectionHealthText => _preferencesService.T(
        "Loc.Tags.CollectionHealth",
        Tags.Count(tag => !tag.IsUnused),
        Tags.Count(tag => tag.IsUnused));

    public string ActiveTagSummaryText => ActiveTagCount == TagCount
        ? _preferencesService.T("Loc.Tags.AllActive")
        : _preferencesService.T("Loc.Tags.ActiveCount", ActiveTagCount);

    public string CoverageText => TaggedImageCount == 0
        ? _preferencesService.T("Loc.Tags.NoTaggedImages")
        : _preferencesService.T("Loc.Tags.TaggedImagesSummary");

    public string UnusedTagSummaryText => UnusedTagCount == 0
        ? _preferencesService.T("Loc.Tags.NoUnused")
        : _preferencesService.T("Loc.Tags.UnusedCount", UnusedTagCount);

    public string TagSectionTitle => _preferencesService.T("Loc.Tags.SectionCount", TagCount);

    public string DeleteConfirmationTitle => PendingDeleteTag is null
        ? string.Empty
        : _preferencesService.T("Loc.Tags.DeleteConfirmTitle", PendingDeleteTag.Name);

    public string DeleteConfirmationUsageText => PendingDeleteTag is null
        ? string.Empty
        : PendingDeleteTag.UsageCount == 0
            ? _preferencesService.T("Loc.Tags.DeleteConfirmUnused")
            : _preferencesService.T("Loc.Tags.DeleteConfirmUsage", PendingDeleteTag.UsageCount);

    // ── Load ──────────────────────────────────────────────────────────────────

    private async Task LoadTagsAsync()
    {
        IsLoading = true;

        Tags.Clear();

        var tags = await _tagService.GetAllAsync();
        var usageCounts = await _tagService.GetUsageCountsAsync();
        TaggedImageCount = await _tagService.CountTaggedImagesAsync();

        foreach (var tag in tags)
        {
            usageCounts.TryGetValue(tag.Id, out var usageCount);
            AddRow(tag.Id, tag.Name, tag.Color, usageCount);
        }

        SortRows();
        RefreshStats();
        IsLoading = false;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void AddRow(int id, string name, TagColor color, int usageCount = 0)
    {
        var row = new TagRowViewModel
        {
            Id = id,
            Name = name,
            Color = color,
            EditName = name,
            EditColor = color,
            UsageCount = usageCount
        };
        UpdateUsageText(row);

        row.SaveRequested += OnSaveTag;
        row.DeleteRequested += OnDeleteTag;

        Tags.Add(row);
        SortRows();
        RefreshStats();
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
        OnPropertyChanged(nameof(VisibleTags));
        OnPropertyChanged(nameof(HasVisibleTags));
        OnPropertyChanged(nameof(ShowFilterEmpty));
    }

    private async void OnDeleteTag(TagRowViewModel row)
    {
        PendingDeleteTag = row;
        IsDeleteConfirmationVisible = true;
        OnPropertyChanged(nameof(DeleteConfirmationTitle));
        OnPropertyChanged(nameof(DeleteConfirmationUsageText));
    }

    [RelayCommand]
    private async Task ConfirmDelete()
    {
        if (PendingDeleteTag is not { } row)
            return;

        await _tagService.DeleteAsync(row.Id);

        Tags.Remove(row);
        PendingDeleteTag = null;
        IsDeleteConfirmationVisible = false;
        await RefreshTaggedImageCountAsync();
        RefreshStats();
    }

    [RelayCommand]
    private void CancelDelete()
    {
        PendingDeleteTag = null;
        IsDeleteConfirmationVisible = false;
        OnPropertyChanged(nameof(DeleteConfirmationTitle));
        OnPropertyChanged(nameof(DeleteConfirmationUsageText));
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

    private void RefreshStats()
    {
        TagCount = Tags.Count;
        OnPropertyChanged(nameof(HasTags));
        OnPropertyChanged(nameof(VisibleTags));
        OnPropertyChanged(nameof(HasVisibleTags));
        OnPropertyChanged(nameof(ShowFilterEmpty));
        OnPropertyChanged(nameof(ActiveTagCount));
        OnPropertyChanged(nameof(UnusedTagCount));
        OnPropertyChanged(nameof(CollectionStatsText));
        OnPropertyChanged(nameof(CollectionHealthText));
        OnPropertyChanged(nameof(ActiveTagSummaryText));
        OnPropertyChanged(nameof(CoverageText));
        OnPropertyChanged(nameof(UnusedTagSummaryText));
        OnPropertyChanged(nameof(TagSectionTitle));
    }

    private async Task RefreshTaggedImageCountAsync()
    {
        TaggedImageCount = await _tagService.CountTaggedImagesAsync();
        OnPropertyChanged(nameof(CollectionStatsText));
        OnPropertyChanged(nameof(CoverageText));
    }

    private void UpdateUsageText(TagRowViewModel row)
    {
        row.UsageText = row.UsageCount == 1
            ? _preferencesService.T("Loc.Tags.UsageOne")
            : _preferencesService.T("Loc.Tags.UsageMany", row.UsageCount);
        row.UsageBadgeText = row.IsUnused
            ? row.UsageText
            : row.UsageText;
    }

    private void SortRows()
    {
        var sorted = Tags
            .OrderByDescending(tag => tag.UsageCount)
            .ThenBy(tag => tag.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        for (var targetIndex = 0; targetIndex < sorted.Count; targetIndex++)
        {
            var currentIndex = Tags.IndexOf(sorted[targetIndex]);
            if (currentIndex >= 0 && currentIndex != targetIndex)
                Tags.Move(currentIndex, targetIndex);
        }

        var maxUsageCount = Math.Max(1, Tags.Count == 0 ? 1 : Tags.Max(tag => tag.UsageCount));
        foreach (var tag in Tags)
            tag.MaxUsageCount = maxUsageCount;
    }

    private void OnPreferencesChanged(
        object? sender,
        AppPreferencesChangedEventArgs e)
    {
        if (!e.LanguageChanged)
            return;

        foreach (var row in Tags)
            UpdateUsageText(row);

        OnPropertyChanged(nameof(CollectionStatsText));
        OnPropertyChanged(nameof(CollectionHealthText));
        OnPropertyChanged(nameof(ActiveTagSummaryText));
        OnPropertyChanged(nameof(CoverageText));
        OnPropertyChanged(nameof(UnusedTagSummaryText));
        OnPropertyChanged(nameof(TagSectionTitle));
        OnPropertyChanged(nameof(DeleteConfirmationTitle));
        OnPropertyChanged(nameof(DeleteConfirmationUsageText));
    }
}
