using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Organize.Organizer.Core.Enums;
using Organizer.Application.Services;

namespace Organizer.Application.ViewModels.Components;

public partial class TagRowViewModel : ObservableObject
{
    // ── Estado de edição ──────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotEditing))]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    [NotifyPropertyChangedFor(nameof(DisplayColor))]
    private bool _isEditing;

    // ── Dados persistidos ─────────────────────────────────────────────────────

    public int Id { get; init; }

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(DisplayName))]
    private string _name = string.Empty;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(DisplayColor))] [NotifyPropertyChangedFor(nameof(ColorHex))]
    private TagColor _color;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsUnused))]
    [NotifyPropertyChangedFor(nameof(UsageBarWidth))]
    private int _usageCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UsageBarWidth))]
    private int _maxUsageCount = 1;

    [ObservableProperty] private string _usageText = string.Empty;

    [ObservableProperty] private string _usageBadgeText = string.Empty;

    // ── Dados temporários da edição ───────────────────────────────────────────

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(DisplayName))]
    private string _editName = string.Empty;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(DisplayColor))]
    private TagColor _editColor;

    // ── Display ───────────────────────────────────────────────────────────────

    public string DisplayName => IsEditing ? EditName : Name;

    public TagColor DisplayColor => IsEditing ? EditColor : Color;

    public bool IsNotEditing => !IsEditing;

    public bool IsUnused => UsageCount == 0;

    public double UsageBarWidth => MaxUsageCount <= 0
        ? 0
        : Math.Clamp(UsageCount / (double)MaxUsageCount, 0, 1) * 90;

    // ── Cor visual ────────────────────────────────────────────────────────────

    public string ColorHex => TagColorPalette.Get(DisplayColor).SelectedBackground;

    // ── Eventos ───────────────────────────────────────────────────────────────

    public event Action<TagRowViewModel>? SaveRequested;

    public event Action<TagRowViewModel>? DeleteRequested;

    // ── Comandos ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private void StartEdit()
    {
        EditName = Name;
        EditColor = Color;

        IsEditing = true;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsEditing = false;
    }

    [RelayCommand]
    private void Save()
    {
        IsEditing = false;

        SaveRequested?.Invoke(this);
    }

    [RelayCommand]
    private void Delete()
    {
        DeleteRequested?.Invoke(this);
    }
}
