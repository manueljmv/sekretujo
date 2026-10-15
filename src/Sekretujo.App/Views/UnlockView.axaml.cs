using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Sekretujo.App.ViewModels;

namespace Sekretujo.App.Views;

public partial class UnlockView : UserControl
{
    public UnlockView() => InitializeComponent();

    private static readonly FilePickerFileType VaultFileType = new("Vault")
    {
        Patterns = new[] { "*.vault" }
    };

    private async void OnBrowseOpenClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not UnlockViewModel vm) return;
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null) return;

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Abrir vault",
            AllowMultiple = false,
            FileTypeFilter = new[] { VaultFileType }
        });

        if (files.Count == 0) return;
        vm.VaultPath = files[0].Path.LocalPath;
        vm.OpenExistingCommand.Execute(null);
    }

    private async void OnBrowseSaveClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not UnlockViewModel vm) return;
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null) return;

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Crear vault nuevo",
            DefaultExtension = "vault",
            FileTypeChoices = new[] { VaultFileType },
            ShowOverwritePrompt = false
        });

        if (file is null) return;
        vm.VaultPath = file.Path.LocalPath;
        vm.CreateNewCommand.Execute(null);
    }
}
