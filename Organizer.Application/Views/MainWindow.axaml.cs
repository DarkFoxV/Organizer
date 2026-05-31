namespace Organizer.Application.Views;

using System.Threading.Tasks;
using Avalonia.Controls;
using global::Organizer.Application.Services;
using global::Organizer.Application.ViewModels;

public partial class MainWindow : Window
{
    private bool _closeConfirmed;
    private bool _isClosing;

    public MainWindow()
    {
        InitializeComponent();
        Closing += OnClosing;
    }

    public MainWindow(MainWindowViewModel vm)
        : this()
    {
        DataContext = vm;
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_closeConfirmed)
            return;

        if (_isClosing)
        {
            e.Cancel = true;
            return;
        }

        if (DataContext is not MainWindowViewModel vm)
            return;

        if (vm.IsGlobalLoading)
        {
            e.Cancel = true;

            await ConfirmationDialog.ShowAsync(
                this,
                AppPreferencesService.Translate("Loc.Common.Wait"),
                string.IsNullOrWhiteSpace(vm.GlobalLoadingText)
                    ? "Uma operacao ainda esta em andamento. Aguarde ela terminar antes de fechar o app."
                    : vm.GlobalLoadingText,
                AppPreferencesService.Translate("Loc.Common.Ok"),
                AppPreferencesService.Translate("Loc.Common.Ok"));

            return;
        }

        if (!vm.HasUnsavedWorkspaceChanges)
            return;

        e.Cancel = true;
        _isClosing = true;

        try
        {
            if (vm.HasFileBackedWorkspace)
            {
                if (await vm.SaveWorkspaceToCurrentFileAsync())
                {
                    _closeConfirmed = true;
                    Close();
                    return;
                }

                var shouldSaveAs = await ConfirmationDialog.ShowAsync(
                    this,
                    AppPreferencesService.Translate("Loc.Workspace.SaveErrorTitle"),
                    AppPreferencesService.Translate("Loc.Workspace.SaveErrorMessage"),
                    AppPreferencesService.Translate("Loc.Common.Save"),
                    AppPreferencesService.Translate("Loc.Common.Ok"),
                    isDanger: true);

                if (shouldSaveAs && await SaveWorkspaceBeforeCloseAsync(vm))
                {
                    _closeConfirmed = true;
                    Close();
                }

                return;
            }

            var shouldSave = await ConfirmationDialog.ShowAsync(
                this,
                AppPreferencesService.Translate("Loc.App.CloseConfirmTitle"),
                AppPreferencesService.Translate("Loc.App.CloseConfirmMessage"),
                AppPreferencesService.Translate("Loc.Common.Save"),
                AppPreferencesService.Translate("Loc.Workspace.CloseWithoutSaving"),
                isDanger: true);

            if (shouldSave && !await SaveWorkspaceBeforeCloseAsync(vm))
                return;

            _closeConfirmed = true;
            Close();
        }
        finally
        {
            if (!_closeConfirmed)
                _isClosing = false;
        }
    }

    private async Task<bool> SaveWorkspaceBeforeCloseAsync(MainWindowViewModel vm)
    {
        var file = await StorageProvider.SaveFilePickerAsync(WorkspaceFilePicker.CreateSaveOptions());

        return file is not null && await vm.SaveWorkspaceToFileAsync(file);
    }
}
