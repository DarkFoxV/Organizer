using System;
using Organizer.Application.Enums;

namespace Organizer.Application.Services;

public sealed class ToastNotification
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public ToastType Type { get; init; }

    public string Title { get; init; } = string.Empty;

    public string? Message { get; init; }

    public TimeSpan Duration { get; init; }

    public bool CanClose { get; init; } = true;

    public bool HasMessage => !string.IsNullOrWhiteSpace(Message);

    public bool IsInfo => Type == ToastType.Info;

    public bool IsProgress => Type == ToastType.Progress;

    public bool IsSuccess => Type == ToastType.Success;

    public bool IsWarning => Type == ToastType.Warning;

    public bool IsError => Type == ToastType.Error;

    public string Icon => Type switch
    {
        ToastType.Progress => "...",
        ToastType.Success => "OK",
        ToastType.Warning => "!",
        ToastType.Error => "X",
        _ => "i"
    };
}
