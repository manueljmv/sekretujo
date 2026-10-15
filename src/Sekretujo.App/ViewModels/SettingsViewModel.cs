using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sekretujo.App.Services;
using Sekretujo.Core.Services;
using Sekretujo.Core.Storage;

namespace Sekretujo.App.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly ImportExportService _importExport;
    private readonly VaultSession _session;
    private readonly VaultRepository _repository;
    private readonly AutoLockService _autoLock;
    private readonly AppSettings _appSettings;

    [ObservableProperty] private int _autoLockMinutes;
    [ObservableProperty] private int _clipboardClearSeconds;
    [ObservableProperty] private string _filePath = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public event Action? Closed;
    public event Action? SettingsChanged;

    public SettingsViewModel(ImportExportService importExport, VaultSession session,
        VaultRepository repository, AutoLockService autoLock, AppSettings appSettings)
    {
        _importExport = importExport;
        _session = session;
        _repository = repository;
        _autoLock = autoLock;
        _appSettings = appSettings;
        _autoLockMinutes = appSettings.AutoLockMinutes;
        _clipboardClearSeconds = appSettings.ClipboardClearSeconds;
    }

    [RelayCommand]
    private void Close() => Closed?.Invoke();

    [RelayCommand]
    private void ApplyTimeouts()
    {
        _appSettings.AutoLockMinutes = AutoLockMinutes;
        _appSettings.ClipboardClearSeconds = ClipboardClearSeconds;
        _autoLock.Start(TimeSpan.FromMinutes(AutoLockMinutes));
        StatusMessage = "Ajustes aplicados.";
        SettingsChanged?.Invoke();
    }

    [RelayCommand]
    private void ExportCsv()
    {
        if (_session.Data is null) return;
        if (string.IsNullOrWhiteSpace(FilePath))
        {
            StatusMessage = "Indica una ruta de archivo.";
            return;
        }
        try
        {
            var passwords = _session.Data.Entries.OfType<Core.Models.PasswordItem>();
            File.WriteAllText(FilePath, _importExport.ExportCsv(passwords));
            StatusMessage = $"Exportado a {FilePath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al exportar: {ex.Message}";
        }
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
            _repository.Save(_session.FilePath!, _session.MasterPassword!, _session.Data);
            StatusMessage = $"Importadas {items.Count} entradas.";
            SettingsChanged?.Invoke();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al importar: {ex.Message}";
        }
    }
}
