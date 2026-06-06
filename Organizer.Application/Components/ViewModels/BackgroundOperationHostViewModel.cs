using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Organizer.Application.ViewModels;

public partial class BackgroundOperationHostViewModel : ObservableObject
{
    [ObservableProperty] private bool _hasOperations;

    public ObservableCollection<BackgroundOperationViewModel> Operations { get; } = [];

    public void Add(BackgroundOperationViewModel operation)
    {
        Operations.Add(operation);
        HasOperations = true;
    }

    public void Remove(Guid id)
    {
        var operation = Operations.FirstOrDefault(item => item.Id == id);
        if (operation is null)
            return;

        Operations.Remove(operation);
        operation.Dispose();
        HasOperations = Operations.Count > 0;
    }
}
