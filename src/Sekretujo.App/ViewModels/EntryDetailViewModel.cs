using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sekretujo.App.Services;
using Sekretujo.Core.Models;
using Sekretujo.Core.Services;

namespace Sekretujo.App.ViewModels;

public enum EntryKind
{
    Password,
    Note,
    Card
}

public partial class EntryDetailViewModel : ViewModelBase, IDisposable
{
    private readonly PasswordGeneratorService _generator;
    private readonly TotpService _totp;
    private readonly ClipboardAutoClearService _clipboardAutoClear;
    private readonly AppSettings _appSettings;
    private readonly AppSettingsStore _appSettingsStore;
    private readonly PasswordStrengthEstimator _strengthEstimator;
    private readonly DispatcherTimer _totpTimer;
    private IClipboard? _clipboard;
    private ILauncher? _launcher;
    private VaultEntry? _originalEntry;
    private DateTimeOffset _originalCreatedAt = DateTimeOffset.UtcNow;
    private bool _isLoading;
    private int _copiedVersion;

    [ObservableProperty] private Guid _id;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPassword), nameof(IsNote), nameof(IsCard), nameof(TypeLabel))]
    private EntryKind _kind;
    [ObservableProperty] private bool _isFavorite;
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _cardHolder = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CardNumberWarning))]
    private string _cardNumber = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CardExpiryWarning))]
    private string _cardExpiry = string.Empty;
    [ObservableProperty] private string _cardCvv = string.Empty;
    [ObservableProperty] private string _cardPin = string.Empty;
    [ObservableProperty] private string _bankPhone = string.Empty;
    [ObservableProperty] private bool _isCardNumberVisible;
    [ObservableProperty] private bool _isCardCvvVisible;
    [ObservableProperty] private bool _isCardPinVisible;
    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _url = string.Empty;
    [ObservableProperty] private string _notes = string.Empty;
    [ObservableProperty] private string _tagsText = string.Empty;
    [ObservableProperty] private string? _totpSecret;
    [ObservableProperty] private string _body = string.Empty;
    [ObservableProperty] private string _totpCode = string.Empty;
    [ObservableProperty] private int _totpSecondsRemaining;
    [ObservableProperty] private string _totpErrorMessage = string.Empty;
    [ObservableProperty] private Bitmap? _totpQrCode;
    [ObservableProperty] private bool _isTotpQrVisible;
    [ObservableProperty] private bool _isConfirmingDelete;
    [ObservableProperty] private bool _isPasswordVisible;
    [ObservableProperty] private bool _isDirty;
    [ObservableProperty] private string? _copiedField;
    [ObservableProperty] private string _passwordStrengthLabel = string.Empty;
    [ObservableProperty] private double _passwordStrengthValue;
    [ObservableProperty] private IBrush _passwordStrengthColor = Brushes.Transparent;
    [ObservableProperty] private int _generatorLength;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GeneratePasswordCommand))]
    private bool _generatorUseUppercase;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GeneratePasswordCommand))]
    private bool _generatorUseLowercase;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GeneratePasswordCommand))]
    private bool _generatorUseDigits;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GeneratePasswordCommand))]
    private bool _generatorUseSymbols;

    public const int TotpPeriodSeconds = 30;

    public bool IsPassword => Kind == EntryKind.Password;
    public bool IsNote => Kind == EntryKind.Note;
    public bool IsCard => Kind == EntryKind.Card;

    public string TypeLabel => Kind switch
    {
        EntryKind.Note => "Nota",
        EntryKind.Card => "Tarjeta",
        _ => "Contraseña"
    };

    public string CardNumberWarning =>
        string.IsNullOrWhiteSpace(CardNumber) || CardInfo.IsValidNumber(CardNumber)
            ? string.Empty
            : "Número de tarjeta no válido.";

    public string CardExpiryWarning =>
        string.IsNullOrWhiteSpace(CardExpiry)
            ? string.Empty
            : CardInfo.GetExpiryStatus(CardExpiry, DateOnly.FromDateTime(DateTime.Today)) switch
            {
                ExpiryStatus.InvalidFormat => "Formato de caducidad no válido (MM/AA).",
                ExpiryStatus.Expired => "Tarjeta caducada.",
                _ => string.Empty
            };

    public event Action<VaultEntry>? Saved;
    public event Action<Guid>? DeleteRequested;

    public EntryDetailViewModel(PasswordGeneratorService generator, TotpService totp,
        ClipboardAutoClearService clipboardAutoClear, AppSettings appSettings, AppSettingsStore appSettingsStore,
        PasswordStrengthEstimator strengthEstimator)
    {
        _generator = generator;
        _totp = totp;
        _clipboardAutoClear = clipboardAutoClear;
        _appSettings = appSettings;
        _appSettingsStore = appSettingsStore;
        _strengthEstimator = strengthEstimator;
        _totpTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _totpTimer.Tick += (_, _) => RefreshTotp();

        GeneratorLength = appSettings.GeneratorLength;
        GeneratorUseUppercase = appSettings.GeneratorUseUppercase;
        GeneratorUseLowercase = appSettings.GeneratorUseLowercase;
        GeneratorUseDigits = appSettings.GeneratorUseDigits;
        GeneratorUseSymbols = appSettings.GeneratorUseSymbols;
    }

    public void AttachClipboard(IClipboard clipboard) => _clipboard = clipboard;
    public void AttachLauncher(ILauncher launcher) => _launcher = launcher;

    public void LoadFrom(VaultEntry entry)
    {
        _isLoading = true;
        _originalEntry = entry;
        IsTotpQrVisible = false;
        Id = entry.Id;
        Title = entry.Title;
        IsFavorite = entry.IsFavorite;
        TagsText = string.Join(", ", entry.Tags);
        _originalCreatedAt = entry.CreatedAt;
        IsCardNumberVisible = false;
        IsCardCvvVisible = false;
        IsCardPinVisible = false;

        if (entry is PasswordItem password)
        {
            Kind = EntryKind.Password;
            Username = password.Username;
            Password = password.Password;
            Url = password.Url;
            Notes = password.Notes;
            TotpSecret = password.TotpSecret;
            RefreshTotp();
        }
        else if (entry is NoteItem note)
        {
            Kind = EntryKind.Note;
            Body = note.Body;
        }
        else if (entry is CardItem card)
        {
            Kind = EntryKind.Card;
            IsCardNumberVisible = string.IsNullOrEmpty(card.Number);
            CardHolder = card.Holder;
            CardNumber = card.Number;
            CardExpiry = card.Expiry;
            CardCvv = card.Cvv;
            CardPin = card.Pin;
            BankPhone = card.BankPhone;
            Notes = card.Notes;
        }

        IsDirty = false;
        _isLoading = false;
    }

    partial void OnTotpSecretChanged(string? value)
    {
        RefreshTotp();
        UpdateTotpQrCode();
        if (string.IsNullOrWhiteSpace(value)) _totpTimer.Stop();
        else _totpTimer.Start();
        MarkDirty();
    }

    partial void OnPasswordChanged(string value)
    {
        MarkDirty();
        if (string.IsNullOrEmpty(value))
        {
            PasswordStrengthLabel = string.Empty;
            PasswordStrengthValue = 0;
            PasswordStrengthColor = Brushes.Transparent;
            return;
        }

        (PasswordStrengthLabel, PasswordStrengthValue, PasswordStrengthColor) =
            _strengthEstimator.Estimate(value) switch
            {
                PasswordStrength.VeryWeak => ("Muy débil", 25d, (IBrush)Brushes.IndianRed),
                PasswordStrength.Weak => ("Débil", 50d, (IBrush)Brushes.DarkOrange),
                PasswordStrength.Medium => ("Media", 75d, (IBrush)Brushes.Goldenrod),
                PasswordStrength.Strong => ("Muy fuerte", 100d, (IBrush)Brushes.ForestGreen),
                _ => (string.Empty, 0d, (IBrush)Brushes.Transparent)
            };
    }

    partial void OnTitleChanged(string value)
    {
        MarkDirty();
        UpdateTotpQrCode();
    }
    partial void OnUsernameChanged(string value) => MarkDirty();
    partial void OnUrlChanged(string value) => MarkDirty();
    partial void OnNotesChanged(string value) => MarkDirty();
    partial void OnBodyChanged(string value) => MarkDirty();
    partial void OnTagsTextChanged(string value) => MarkDirty();
    partial void OnIsFavoriteChanged(bool value) => MarkDirty();
    partial void OnCardHolderChanged(string value) => MarkDirty();
    partial void OnCardNumberChanged(string value) => MarkDirty();
    partial void OnCardExpiryChanged(string value) => MarkDirty();
    partial void OnCardCvvChanged(string value) => MarkDirty();
    partial void OnCardPinChanged(string value) => MarkDirty();
    partial void OnBankPhoneChanged(string value) => MarkDirty();

    private void MarkDirty()
    {
        if (!_isLoading) IsDirty = true;
    }

    private void RefreshTotp()
    {
        TotpErrorMessage = string.Empty;
        TotpCode = string.Empty;
        TotpSecondsRemaining = 0;
        if (string.IsNullOrWhiteSpace(TotpSecret)) { _totpTimer.Stop(); return; }

        try
        {
            var code = _totp.GetCode(TotpSecret);
            TotpCode = $"{code.Code[..(code.Code.Length / 2)]} {code.Code[(code.Code.Length / 2)..]}";
            TotpSecondsRemaining = code.SecondsRemaining;
        }
        catch (InvalidTotpSecretException)
        {
            TotpErrorMessage = "Código TOTP inválido.";
            _totpTimer.Stop();
        }
    }

    private void UpdateTotpQrCode()
    {
        TotpQrCode?.Dispose();
        TotpQrCode = null;
        if (string.IsNullOrWhiteSpace(TotpSecret)) return;
        try
        {
            var png = _totp.GenerateQrCodePng(TotpSecret, Title);
            using var stream = new MemoryStream(png);
            TotpQrCode = new Bitmap(stream);
        }
        catch (InvalidTotpSecretException)
        {
            // RefreshTotp already surfaces TotpErrorMessage for an invalid secret.
        }
    }

    private bool CanGeneratePassword() =>
        GeneratorUseUppercase || GeneratorUseLowercase || GeneratorUseDigits || GeneratorUseSymbols;

    [RelayCommand(CanExecute = nameof(CanGeneratePassword))]
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
        if (_clipboard is null) return;
        await _clipboardAutoClear.CopyAndClear(_clipboard, Password, TimeSpan.FromSeconds(_appSettings.ClipboardClearSeconds));
        await ShowCopied(nameof(Password));
    }

    [RelayCommand]
    private async Task CopyUsername()
    {
        if (_clipboard is null) return;
        await _clipboard.SetTextAsync(Username);
        await ShowCopied(nameof(Username));
    }

    [RelayCommand]
    private async Task OpenUrl()
    {
        if (_launcher is null) return;
        if (WebUrl.Normalize(Url) is { } uri)
            await _launcher.LaunchUriAsync(uri);
    }

    [RelayCommand]
    private async Task CopyTotp()
    {
        if (_clipboard is null || string.IsNullOrWhiteSpace(TotpSecret)) return;
        try
        {
            var code = _totp.GetCode(TotpSecret);
            await _clipboardAutoClear.CopyAndClear(_clipboard, code.Code, TimeSpan.FromSeconds(_appSettings.ClipboardClearSeconds));
            await ShowCopied(nameof(TotpCode));
        }
        catch (InvalidTotpSecretException)
        {
            TotpErrorMessage = "Código TOTP inválido.";
        }
    }

    [RelayCommand]
    private async Task CopyTotpSecret()
    {
        if (_clipboard is null || string.IsNullOrWhiteSpace(TotpSecret)) return;
        await _clipboardAutoClear.CopyAndClear(_clipboard, TotpSecret, TimeSpan.FromSeconds(_appSettings.ClipboardClearSeconds));
        await ShowCopied(nameof(TotpSecret));
    }

    [RelayCommand]
    private Task CopyCardNumber() => CopySecret(CardInfo.Digits(CardNumber), nameof(CardNumber));

    [RelayCommand]
    private Task CopyCardCvv() => CopySecret(CardCvv, nameof(CardCvv));

    [RelayCommand]
    private Task CopyCardPin() => CopySecret(CardPin, nameof(CardPin));

    [RelayCommand]
    private Task CopyCardHolder() => CopyPlain(CardHolder, nameof(CardHolder));

    [RelayCommand]
    private Task CopyCardExpiry() => CopyPlain(CardExpiry, nameof(CardExpiry));

    [RelayCommand]
    private Task CopyBankPhone() => CopyPlain(BankPhone, nameof(BankPhone));

    private async Task CopySecret(string value, string field)
    {
        if (_clipboard is null) return;
        await _clipboardAutoClear.CopyAndClear(_clipboard, value, TimeSpan.FromSeconds(_appSettings.ClipboardClearSeconds));
        await ShowCopied(field);
    }

    private async Task CopyPlain(string value, string field)
    {
        if (_clipboard is null) return;
        await _clipboard.SetTextAsync(value);
        await ShowCopied(field);
    }

    private async Task ShowCopied(string field)
    {
        var version = ++_copiedVersion;
        CopiedField = field;
        await Task.Delay(TimeSpan.FromSeconds(1.5));
        if (version == _copiedVersion) CopiedField = null;
    }

    [RelayCommand]
    private void Save()
    {
        var tags = TagsText.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        VaultEntry entry = Kind switch
        {
            EntryKind.Note => new NoteItem
            {
                Id = Id,
                Title = Title,
                Body = Body,
                IsFavorite = IsFavorite,
                Tags = tags,
                CreatedAt = _originalCreatedAt,
                ModifiedAt = DateTimeOffset.UtcNow
            },
            EntryKind.Card => new CardItem
            {
                Id = Id,
                Title = Title,
                Holder = CardHolder,
                Number = CardNumber,
                Expiry = CardExpiry,
                Cvv = CardCvv,
                Pin = CardPin,
                BankPhone = BankPhone,
                Notes = Notes,
                IsFavorite = IsFavorite,
                Tags = tags,
                CreatedAt = _originalCreatedAt,
                ModifiedAt = DateTimeOffset.UtcNow
            },
            _ => new PasswordItem
            {
                Id = Id,
                Title = Title,
                Username = Username,
                Password = Password,
                Url = Url,
                Notes = Notes,
                TotpSecret = TotpSecret,
                IsFavorite = IsFavorite,
                Tags = tags,
                CreatedAt = _originalCreatedAt,
                ModifiedAt = DateTimeOffset.UtcNow
            }
        };
        Saved?.Invoke(entry);
    }

    public void MarkSaved(VaultEntry saved)
    {
        _originalEntry = saved;
        IsDirty = false;
    }

    [RelayCommand]
    private void Discard()
    {
        if (_originalEntry is not null) LoadFrom(_originalEntry);
    }

    [RelayCommand]
    private void Delete() => IsConfirmingDelete = true;

    [RelayCommand]
    private void ConfirmDelete() => DeleteRequested?.Invoke(Id);

    [RelayCommand]
    private void CancelDelete() => IsConfirmingDelete = false;

    public void Dispose()
    {
        _totpTimer.Stop();
        TotpQrCode?.Dispose();
    }
}
