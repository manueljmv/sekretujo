using System;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sekretujo.App.Services;
using Sekretujo.Core.Crypto;
using Sekretujo.Core.Models;
using Sekretujo.Core.Services;
using Sekretujo.Core.Storage;

namespace Sekretujo.App.ViewModels;

public partial class UnlockViewModel : ViewModelBase
{
    private readonly VaultRepository _repository;
    private readonly VaultSession _session;
    private readonly AppSettings _appSettings;
    private readonly AppSettingsStore _appSettingsStore;
    private readonly PasswordStrengthEstimator _strengthEstimator;

    [ObservableProperty]
    private string _vaultPath = string.Empty;

    [ObservableProperty]
    private string _masterPassword = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _masterPasswordStrengthLabel = string.Empty;

    public event Action? Unlocked;

    public UnlockViewModel(VaultRepository repository, VaultSession session, AppSettings appSettings,
        AppSettingsStore appSettingsStore, PasswordStrengthEstimator strengthEstimator)
    {
        _repository = repository;
        _session = session;
        _appSettings = appSettings;
        _appSettingsStore = appSettingsStore;
        _strengthEstimator = strengthEstimator;
        VaultPath = appSettings.LastVaultPath ?? string.Empty;
    }

    partial void OnMasterPasswordChanged(string value)
    {
        MasterPasswordStrengthLabel = string.IsNullOrEmpty(value)
            ? string.Empty
            : _strengthEstimator.Estimate(value) switch
            {
                PasswordStrength.VeryWeak => "Muy débil",
                PasswordStrength.Weak => "Débil",
                PasswordStrength.Medium => "Media",
                PasswordStrength.Strong => "Fuerte",
                _ => string.Empty
            };
    }

    [RelayCommand]
    private void OpenExisting()
    {
        ErrorMessage = string.Empty;
        try
        {
            var data = _repository.Load(VaultPath, MasterPassword);
            _session.Open(VaultPath, MasterPassword, data);
            RememberVaultPath();
            Unlocked?.Invoke();
        }
        catch (VaultUnlockException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (VaultCorruptException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ErrorMessage = $"No se pudo leer el archivo: {ex.Message}";
        }
    }

    [RelayCommand]
    private void CreateNew()
    {
        ErrorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(VaultPath) || string.IsNullOrWhiteSpace(MasterPassword))
        {
            ErrorMessage = "Indica una ruta y una master password.";
            return;
        }

        if (File.Exists(VaultPath))
        {
            ErrorMessage = "Ya existe un archivo en esa ruta. Usa \"Abrir vault\" para abrirlo, o elige otra ruta.";
            return;
        }

        try
        {
            var data = new VaultData();
            _repository.Save(VaultPath, MasterPassword, data);
            _session.Open(VaultPath, MasterPassword, data);
            RememberVaultPath();
            Unlocked?.Invoke();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ErrorMessage = $"No se pudo crear el archivo: {ex.Message}";
        }
    }

    private void RememberVaultPath()
    {
        _appSettings.LastVaultPath = VaultPath;
        _appSettingsStore.Save(_appSettings);
    }
}
