using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sekretujo.App.Services;
using Sekretujo.Core.Crypto;
using Sekretujo.Core.Models;
using Sekretujo.Core.Storage;

namespace Sekretujo.App.ViewModels;

public partial class UnlockViewModel : ViewModelBase
{
    private readonly VaultRepository _repository;
    private readonly VaultSession _session;

    [ObservableProperty]
    private string _vaultPath = string.Empty;

    [ObservableProperty]
    private string _masterPassword = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public event Action? Unlocked;

    public UnlockViewModel(VaultRepository repository, VaultSession session)
    {
        _repository = repository;
        _session = session;
    }

    [RelayCommand]
    private void OpenExisting()
    {
        ErrorMessage = string.Empty;
        try
        {
            var data = _repository.Load(VaultPath, MasterPassword);
            _session.Open(VaultPath, MasterPassword, data);
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

        var data = new VaultData();
        _repository.Save(VaultPath, MasterPassword, data);
        _session.Open(VaultPath, MasterPassword, data);
        Unlocked?.Invoke();
    }
}
