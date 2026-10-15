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
    private readonly AppSettingsStore _appSettingsStore;
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
    [ObservableProperty] private bool _isConfirmingDelete;
    [ObservableProperty] private int _generatorLength;
    [ObservableProperty] private bool _generatorUseUppercase;
    [ObservableProperty] private bool _generatorUseLowercase;
    [ObservableProperty] private bool _generatorUseDigits;
    [ObservableProperty] private bool _generatorUseSymbols;

    public event Action<VaultEntry>? Saved;
    public event Action<Guid>? DeleteRequested;

    public EntryDetailViewModel(PasswordGeneratorService generator, TotpService totp,
        ClipboardAutoClearService clipboardAutoClear, AppSettings appSettings, AppSettingsStore appSettingsStore)
    {
        _generator = generator;
        _totp = totp;
        _clipboardAutoClear = clipboardAutoClear;
        _appSettings = appSettings;
        _appSettingsStore = appSettingsStore;
        _totpTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _totpTimer.Tick += (_, _) => RefreshTotp();

        GeneratorLength = appSettings.GeneratorLength;
        GeneratorUseUppercase = appSettings.GeneratorUseUppercase;
        GeneratorUseLowercase = appSettings.GeneratorUseLowercase;
        GeneratorUseDigits = appSettings.GeneratorUseDigits;
        GeneratorUseSymbols = appSettings.GeneratorUseSymbols;
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
    private void GeneratePassword()
    {
        var options = new PasswordGeneratorOptions
        {
            Length = GeneratorLength,
            UseUppercase = GeneratorUseUppercase,
            UseLowercase = GeneratorUseLowercase,
            UseDigits = GeneratorUseDigits,
            UseSymbols = GeneratorUseSymbols
        };
        Password = _generator.Generate(options);

        _appSettings.GeneratorLength = GeneratorLength;
        _appSettings.GeneratorUseUppercase = GeneratorUseUppercase;
        _appSettings.GeneratorUseLowercase = GeneratorUseLowercase;
        _appSettings.GeneratorUseDigits = GeneratorUseDigits;
        _appSettings.GeneratorUseSymbols = GeneratorUseSymbols;
        _appSettingsStore.Save(_appSettings);
    }

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
    private void Delete() => IsConfirmingDelete = true;

    [RelayCommand]
    private void ConfirmDelete() => DeleteRequested?.Invoke(Id);

    [RelayCommand]
    private void CancelDelete() => IsConfirmingDelete = false;

    public void Dispose() => _totpTimer.Stop();
}
