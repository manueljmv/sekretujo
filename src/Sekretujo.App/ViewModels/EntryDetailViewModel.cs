using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sekretujo.App.Services;
using Sekretujo.Core.Models;
using Sekretujo.Core.Services;

namespace Sekretujo.App.ViewModels;

public partial class EntryDetailViewModel : ViewModelBase, IDisposable
{
    private readonly PasswordGeneratorService _generator;
    private readonly TotpService _totp;
    private readonly ClipboardAutoClearService _clipboardAutoClear;
    private readonly AppSettings _appSettings;
    private readonly DispatcherTimer _totpTimer;
    private IClipboard? _clipboard;
    private List<string> _originalTags = new();
    private DateTimeOffset _originalCreatedAt = DateTimeOffset.UtcNow;

    [ObservableProperty] private Guid _id;
    [ObservableProperty] private bool _isNote;
    [ObservableProperty] private bool _isFavorite;
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
        ClipboardAutoClearService clipboardAutoClear, AppSettings appSettings)
    {
        _generator = generator;
        _totp = totp;
        _clipboardAutoClear = clipboardAutoClear;
        _appSettings = appSettings;
        _totpTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _totpTimer.Tick += (_, _) => RefreshTotp();
    }

    public void AttachClipboard(IClipboard clipboard) => _clipboard = clipboard;

    public void LoadFrom(VaultEntry entry)
    {
        Id = entry.Id;
        Title = entry.Title;
        IsFavorite = entry.IsFavorite;
        _originalTags = entry.Tags;
        _originalCreatedAt = entry.CreatedAt;

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

    partial void OnTotpSecretChanged(string? value)
    {
        RefreshTotp();
        if (string.IsNullOrWhiteSpace(value)) _totpTimer.Stop();
        else _totpTimer.Start();
    }

    private void RefreshTotp()
    {
        TotpErrorMessage = string.Empty;
        TotpCodeDisplay = string.Empty;
        if (string.IsNullOrWhiteSpace(TotpSecret)) { _totpTimer.Stop(); return; }

        try
        {
            var code = _totp.GetCode(TotpSecret);
            TotpCodeDisplay = $"{code.Code} ({code.SecondsRemaining}s)";
        }
        catch (InvalidTotpSecretException)
        {
            TotpErrorMessage = "Código TOTP inválido.";
            _totpTimer.Stop();
        }
    }

    [RelayCommand]
    private void GeneratePassword() =>
        Password = _generator.Generate(new PasswordGeneratorOptions { Length = 24 });

    [RelayCommand]
    private async Task CopyPassword()
    {
        if (_clipboard is not null)
            await _clipboardAutoClear.CopyAndClear(_clipboard, Password, TimeSpan.FromSeconds(_appSettings.ClipboardClearSeconds));
    }

    [RelayCommand]
    private async Task CopyTotp()
    {
        if (_clipboard is null || string.IsNullOrWhiteSpace(TotpSecret)) return;
        try
        {
            var code = _totp.GetCode(TotpSecret);
            await _clipboardAutoClear.CopyAndClear(_clipboard, code.Code, TimeSpan.FromSeconds(_appSettings.ClipboardClearSeconds));
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
            ? new NoteItem
            {
                Id = Id,
                Title = Title,
                Body = Body,
                IsFavorite = IsFavorite,
                Tags = _originalTags,
                CreatedAt = _originalCreatedAt,
                ModifiedAt = DateTimeOffset.UtcNow
            }
            : new PasswordItem
            {
                Id = Id,
                Title = Title,
                Username = Username,
                Password = Password,
                Url = Url,
                Notes = Notes,
                TotpSecret = TotpSecret,
                IsFavorite = IsFavorite,
                Tags = _originalTags,
                CreatedAt = _originalCreatedAt,
                ModifiedAt = DateTimeOffset.UtcNow
            };
        Saved?.Invoke(entry);
    }

    [RelayCommand]
    private void Delete() => DeleteRequested?.Invoke(Id);

    public void Dispose() => _totpTimer.Stop();
}
