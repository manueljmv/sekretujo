using System;
using System.Threading.Tasks;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sekretujo.App.Services;
using Sekretujo.Core.Models;
using Sekretujo.Core.Services;

namespace Sekretujo.App.ViewModels;

public partial class EntryDetailViewModel : ViewModelBase
{
    private readonly PasswordGeneratorService _generator;
    private readonly TotpService _totp;
    private readonly ClipboardAutoClearService _clipboardAutoClear;
    private IClipboard? _clipboard;

    [ObservableProperty] private Guid _id;
    [ObservableProperty] private bool _isNote;
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _url = string.Empty;
    [ObservableProperty] private string _notes = string.Empty;
    [ObservableProperty] private string? _totpSecret;
    [ObservableProperty] private string _body = string.Empty;
    [ObservableProperty] private string _totpCodeDisplay = string.Empty;
    [ObservableProperty] private string _totpErrorMessage = string.Empty;

    public event Action<VaultEntry>? Saved;
    public event Action<Guid>? DeleteRequested;

    public EntryDetailViewModel(PasswordGeneratorService generator, TotpService totp,
        ClipboardAutoClearService clipboardAutoClear)
    {
        _generator = generator;
        _totp = totp;
        _clipboardAutoClear = clipboardAutoClear;
    }

    public void AttachClipboard(IClipboard clipboard) => _clipboard = clipboard;

    public void LoadFrom(VaultEntry entry)
    {
        Id = entry.Id;
        Title = entry.Title;
        if (entry is PasswordItem password)
        {
            IsNote = false;
            Username = password.Username;
            Password = password.Password;
            Url = password.Url;
            Notes = password.Notes;
            TotpSecret = password.TotpSecret;
            RefreshTotp();
        }
        else if (entry is NoteItem note)
        {
            IsNote = true;
            Body = note.Body;
        }
    }

    private void RefreshTotp()
    {
        TotpErrorMessage = string.Empty;
        TotpCodeDisplay = string.Empty;
        if (string.IsNullOrWhiteSpace(TotpSecret)) return;

        try
        {
            var code = _totp.GetCode(TotpSecret);
            TotpCodeDisplay = $"{code.Code} ({code.SecondsRemaining}s)";
        }
        catch (InvalidTotpSecretException)
        {
            TotpErrorMessage = "Código TOTP inválido.";
        }
    }

    [RelayCommand]
    private void GeneratePassword() =>
        Password = _generator.Generate(new PasswordGeneratorOptions { Length = 24 });

    [RelayCommand]
    private async Task CopyPassword()
    {
        if (_clipboard is not null)
            await _clipboardAutoClear.CopyAndClear(_clipboard, Password, TimeSpan.FromSeconds(20));
    }

    [RelayCommand]
    private async Task CopyTotp()
    {
        if (_clipboard is null || string.IsNullOrWhiteSpace(TotpSecret)) return;
        try
        {
            var code = _totp.GetCode(TotpSecret);
            await _clipboardAutoClear.CopyAndClear(_clipboard, code.Code, TimeSpan.FromSeconds(20));
        }
        catch (InvalidTotpSecretException)
        {
            TotpErrorMessage = "Código TOTP inválido.";
        }
    }

    [RelayCommand]
    private void Save()
    {
        VaultEntry entry = IsNote
            ? new NoteItem { Id = Id, Title = Title, Body = Body, ModifiedAt = DateTimeOffset.UtcNow }
            : new PasswordItem
            {
                Id = Id,
                Title = Title,
                Username = Username,
                Password = Password,
                Url = Url,
                Notes = Notes,
                TotpSecret = TotpSecret,
                ModifiedAt = DateTimeOffset.UtcNow
            };
        Saved?.Invoke(entry);
    }

    [RelayCommand]
    private void Delete() => DeleteRequested?.Invoke(Id);
}
