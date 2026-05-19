using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Organize.Organizer.Core.Enums;

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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    private string _name = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayColor))]
    [NotifyPropertyChangedFor(nameof(ColorHex))]
    private TagColor _color;

    // ── Dados temporários da edição ───────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    private string _editName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayColor))]
    private TagColor _editColor;

    // ── Display ───────────────────────────────────────────────────────────────

    public string DisplayName => IsEditing ? EditName : Name;

    public TagColor DisplayColor => IsEditing ? EditColor : Color;

    public bool IsNotEditing => !IsEditing;

    // ── Cor visual ────────────────────────────────────────────────────────────

    public string ColorHex => DisplayColor switch
    {
        TagColor.Red => "#dc2626",
        TagColor.Green => "#16a34a",
        TagColor.Blue => "#3b82f6",
        TagColor.Orange => "#d97706",
        TagColor.Purple => "#9333ea",
        TagColor.Pink => "#db2777",
        TagColor.Indigo => "#4f46e5",
        TagColor.Teal => "#0d9488",
        TagColor.Gray => "#6b7280",
        _ => "#3b82f6"
    };

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