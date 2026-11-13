using System;
using System.Collections.Generic;
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

    // These handlers are async void: an exception escaping them has no caller and would terminate the app.
    private static string PickerFailedMessage(Exception ex) =>
        $"No se pudo abrir el selector de archivos: {ex.Message}. Escribe la ruta a mano.";

    private async void OnBrowseOpenClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not UnlockViewModel vm) return;
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null) return;

        IReadOnlyList<IStorageFile> files;
        try
        {
            files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Abrir vault",
                AllowMultiple = false,
                FileTypeFilter = new[] { VaultFileType }
            });
        }
        catch (Exception ex)
        {
            vm.ErrorMessage = PickerFailedMessage(ex);
            return;
        }

        if (files.Count == 0) return;
        vm.VaultPath = files[0].Path.LocalPath;
        vm.OpenExistingCommand.Execute(null);
    }

    private async void OnBrowseSaveClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not UnlockViewModel vm) return;
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null) return;

        IStorageFile? file;
        try
        {
            file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Crear vault nuevo",
                DefaultExtension = "vault",
                FileTypeChoices = new[] { VaultFileType },
                ShowOverwritePrompt = false
            });
        }
        catch (Exception ex)
        {
            vm.ErrorMessage = PickerFailedMessage(ex);
            return;
        }

        if (file is null) return;
        vm.BeginCreateNew(file.Path.LocalPath);
    }
}
