using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sekretujo.App.Services;
using Sekretujo.Core.Services;

namespace Sekretujo.App.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly ImportExportService _importExport;
    private readonly VaultSession _session;

    [ObservableProperty] private int _autoLockMinutes = 5;
    [ObservableProperty] private int _clipboardClearSeconds = 20;
    [ObservableProperty] private string _filePath = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public event Action? Closed;
    public event Action? SettingsChanged;

    public SettingsViewModel(ImportExportService importExport, VaultSession session)
    {
        _importExport = importExport;
        _session = session;
    }

    [RelayCommand]
    private void Close() => Closed?.Invoke();

    [RelayCommand]
    private void ApplyTimeouts() => SettingsChanged?.Invoke();

    [RelayCommand]
    private void ExportCsv()
    {
        if (_session.Data is null) return;
        var passwords = _session.Data.Entries.OfType<Core.Models.PasswordItem>();
        File.WriteAllText(FilePath, _importExport.ExportCsv(passwords));
        StatusMessage = $"Exportado a {FilePath}";
    }

    [RelayCommand]
    private void ImportCsv() => Import(() => _importExport.ImportCsv(File.ReadAllText(FilePath)));

    [RelayCommand]
    private void ImportBitwarden() => Import(() => _importExport.ImportBitwardenJson(File.ReadAllText(FilePath)));

    [RelayCommand]
    private void ImportKeePass() => Import(() => _importExport.ImportKeePassCsv(File.ReadAllText(FilePath)));

    private void Import(Func<List<Core.Models.PasswordItem>> loader)
    {
        if (_session.Data is null) return;
        try
        {
            var items = loader();
            _session.Data.Entries.AddRange(items);
            StatusMessage = $"Importadas {items.Count} entradas.";
            SettingsChanged?.Invoke();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al importar: {ex.Message}";
        }
    }
}
