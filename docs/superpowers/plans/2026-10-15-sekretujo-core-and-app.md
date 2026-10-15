# Sekretujo Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build Sekretujo, a cross-platform (Linux/Windows) desktop password manager and
notes app with a local encrypted vault and a modern Fluent-styled UI.

**Architecture:** A dependency-free `Sekretujo.Core` class library holds all domain logic
(models, crypto, storage, services) and is fully unit-tested. `Sekretujo.App` is an
Avalonia + FluentAvalonia desktop app that consumes `Core` through plain C# services, with
CommunityToolkit.Mvvm view models and no business logic in the UI layer.

**Tech Stack:** .NET 10, Avalonia UI, FluentAvalonia, CommunityToolkit.Mvvm,
Konscious.Security.Cryptography (Argon2id), System.Security.Cryptography (AES-256-GCM),
Otp.NET, System.Text.Json, xUnit.

**Spec:** `docs/superpowers/specs/2026-10-15-sekretujo-design.md`

## Global Constraints

- Target framework: `net10.0` everywhere.
- UI: Avalonia + FluentAvalonia theme (Fluent/Windows 11 look, Mica/rounded corners).
- MVVM: CommunityToolkit.Mvvm (`ObservableObject`, `[ObservableProperty]`, `[RelayCommand]`).
- Vault storage: a single encrypted `.vault` file per vault — no SQLite/SQLCipher, no
  server, no remote sync.
- Encryption: AES-256-GCM; key derivation: Argon2id. File layout: `[16B salt][12B nonce]
  [ciphertext][16B tag]`.
- No automated UI tests (per spec, §9) — UI tasks are verified by manual run checklists.
- Must build and run self-contained on `linux-x64` and `win-x64`.

## Review Focus

- Master password incorrecta al abrir un vault existente debe fallar con un mensaje claro
  y nunca sobrescribir el archivo original. → Task 5.
- Archivo `.vault` corrupto o truncado (menos de 44 bytes, o JSON inválido tras descifrar)
  debe dar un error legible, no una excepción cruda sin manejar. → Task 5.
- Un `TotpSecret` en formato Base32 inválido no debe tirar la UI abajo; debe mostrarse como
  código inválido. → Task 7 (servicio) y Task 12 (wiring en UI).
  Generador de contraseñas con todos los charsets desactivados o longitud ≤ 0 debe
  rechazar con un mensaje claro en vez de devolver una contraseña vacía. → Task 6.
- CSV de import con campos que contienen comas, comillas o saltos de línea (notas de texto
  libre) debe parsear sin desalinear columnas. → Task 8.

---

## File Structure

```
Sekretujo.sln
src/
  Sekretujo.Core/
    Models/VaultEntry.cs, PasswordItem.cs, NoteItem.cs, Tag.cs, VaultData.cs
    Crypto/CryptoService.cs, EncryptedPayload.cs, VaultUnlockException.cs
    Storage/VaultSerializer.cs, VaultRepository.cs, VaultCorruptException.cs
    Services/PasswordGeneratorService.cs, TotpService.cs, ImportExportService.cs
  Sekretujo.App/
    App.axaml(.cs), ServiceConfiguration.cs
    Services/VaultSession.cs, ClipboardAutoClearService.cs, AutoLockService.cs
    ViewModels/ViewModelBase.cs, MainWindowViewModel.cs, UnlockViewModel.cs,
      ShellViewModel.cs, EntryListItemViewModel.cs, EntryDetailViewModel.cs,
      SettingsViewModel.cs
    Views/MainWindow.axaml(.cs), UnlockView.axaml(.cs), ShellView.axaml(.cs),
      SettingsView.axaml(.cs)
tests/
  Sekretujo.Core.Tests/
    SmokeTests.cs, VaultDataSerializationTests.cs, CryptoServiceTests.cs,
    VaultRepositoryTests.cs, PasswordGeneratorServiceTests.cs, TotpServiceTests.cs,
    ImportExportServiceTests.cs
```

---

### Task 1: Solution and Core project scaffolding

**Files:**
- Create: `Sekretujo.sln`
- Create: `src/Sekretujo.Core/Sekretujo.Core.csproj`
- Create: `tests/Sekretujo.Core.Tests/Sekretujo.Core.Tests.csproj`
- Create: `tests/Sekretujo.Core.Tests/SmokeTests.cs`

**Interfaces:**
- Produces: a buildable solution with `Sekretujo.Core` and `Sekretujo.Core.Tests`
  projects, both targeting `net10.0`, test project referencing `Sekretujo.Core`.

- [ ] **Step 1: Create the solution and projects**

```bash
cd /home/manuel/projects/dotnet/sekretujo
dotnet new sln -n Sekretujo
dotnet new classlib -n Sekretujo.Core -o src/Sekretujo.Core -f net10.0
dotnet new xunit -n Sekretujo.Core.Tests -o tests/Sekretujo.Core.Tests -f net10.0
dotnet sln add src/Sekretujo.Core/Sekretujo.Core.csproj tests/Sekretujo.Core.Tests/Sekretujo.Core.Tests.csproj
dotnet add tests/Sekretujo.Core.Tests/Sekretujo.Core.Tests.csproj reference src/Sekretujo.Core/Sekretujo.Core.csproj
rm src/Sekretujo.Core/Class1.cs
```

- [ ] **Step 2: Write a trivial smoke test**

```csharp
// tests/Sekretujo.Core.Tests/SmokeTests.cs
using Xunit;

namespace Sekretujo.Core.Tests;

public class SmokeTests
{
    [Fact]
    public void ProjectReferences_AreWired()
    {
        Assert.True(true);
    }
}
```

- [ ] **Step 3: Run the tests and verify they pass**

Run: `dotnet test tests/Sekretujo.Core.Tests`
Expected: `Passed! - Failed: 0, Passed: 1`

- [ ] **Step 4: Commit**

```bash
git add Sekretujo.sln src/Sekretujo.Core tests/Sekretujo.Core.Tests
git commit -m "chore: scaffold Sekretujo.Core and test project"
```

---

### Task 2: Domain models and vault document serialization

**Files:**
- Create: `src/Sekretujo.Core/Models/VaultEntry.cs`
- Create: `src/Sekretujo.Core/Models/PasswordItem.cs`
- Create: `src/Sekretujo.Core/Models/NoteItem.cs`
- Create: `src/Sekretujo.Core/Models/Tag.cs`
- Create: `src/Sekretujo.Core/Models/VaultData.cs`
- Create: `src/Sekretujo.Core/Storage/VaultSerializer.cs`
- Create: `src/Sekretujo.Core/Storage/VaultCorruptException.cs`
- Test: `tests/Sekretujo.Core.Tests/VaultDataSerializationTests.cs`

**Interfaces:**
- Produces: `VaultEntry` (abstract base: `Id`, `Title`, `Tags`, `IsFavorite`, `CreatedAt`,
  `ModifiedAt`), `PasswordItem : VaultEntry` (`Username`, `Password`, `Url`,
  `TotpSecret?`, `Notes`), `NoteItem : VaultEntry` (`Body`), `Tag` (`Id`, `Name`),
  `VaultData` (`SchemaVersion`, `Entries: List<VaultEntry>`, `Tags: List<Tag>`).
- Produces: `VaultSerializer.Serialize(VaultData) -> byte[]`,
  `VaultSerializer.Deserialize(byte[]) -> VaultData` (throws `VaultCorruptException` on
  invalid content).

- [ ] **Step 1: Write the failing serialization round-trip test**

```csharp
// tests/Sekretujo.Core.Tests/VaultDataSerializationTests.cs
using Sekretujo.Core.Models;
using Sekretujo.Core.Storage;
using Xunit;

namespace Sekretujo.Core.Tests;

public class VaultDataSerializationTests
{
    [Fact]
    public void Serialize_Then_Deserialize_RoundTripsMixedEntries()
    {
        var serializer = new VaultSerializer();
        var data = new VaultData
        {
            Tags = { new Tag { Name = "work" } },
            Entries =
            {
                new PasswordItem { Title = "GitHub", Username = "manuel", Password = "s3cr3t", Url = "https://github.com" },
                new NoteItem { Title = "Recovery codes", Body = "1234-5678" }
            }
        };

        var bytes = serializer.Serialize(data);
        var roundTripped = serializer.Deserialize(bytes);

        Assert.Equal(2, roundTripped.Entries.Count);
        var password = Assert.IsType<PasswordItem>(roundTripped.Entries[0]);
        Assert.Equal("GitHub", password.Title);
        Assert.Equal("manuel", password.Username);
        var note = Assert.IsType<NoteItem>(roundTripped.Entries[1]);
        Assert.Equal("1234-5678", note.Body);
        Assert.Equal("work", roundTripped.Tags[0].Name);
    }

    [Fact]
    public void Deserialize_InvalidJson_ThrowsVaultCorruptException()
    {
        var serializer = new VaultSerializer();

        Assert.Throws<VaultCorruptException>(() => serializer.Deserialize(new byte[] { 1, 2, 3 }));
    }
}
```

- [ ] **Step 2: Run test to verify it fails (project doesn't compile yet)**

Run: `dotnet test tests/Sekretujo.Core.Tests`
Expected: FAIL — compile errors, types don't exist.

- [ ] **Step 3: Implement the models**

```csharp
// src/Sekretujo.Core/Models/VaultEntry.cs
using System.Text.Json.Serialization;

namespace Sekretujo.Core.Models;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(PasswordItem), "password")]
[JsonDerivedType(typeof(NoteItem), "note")]
public abstract class VaultEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public bool IsFavorite { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ModifiedAt { get; set; } = DateTimeOffset.UtcNow;
}
```

```csharp
// src/Sekretujo.Core/Models/PasswordItem.cs
namespace Sekretujo.Core.Models;

public sealed class PasswordItem : VaultEntry
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? TotpSecret { get; set; }
    public string Notes { get; set; } = string.Empty;
}
```

```csharp
// src/Sekretujo.Core/Models/NoteItem.cs
namespace Sekretujo.Core.Models;

public sealed class NoteItem : VaultEntry
{
    public string Body { get; set; } = string.Empty;
}
```

```csharp
// src/Sekretujo.Core/Models/Tag.cs
namespace Sekretujo.Core.Models;

public sealed class Tag
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
}
```

```csharp
// src/Sekretujo.Core/Models/VaultData.cs
namespace Sekretujo.Core.Models;

public sealed class VaultData
{
    public int SchemaVersion { get; set; } = 1;
    public List<VaultEntry> Entries { get; set; } = new();
    public List<Tag> Tags { get; set; } = new();
}
```

```csharp
// src/Sekretujo.Core/Storage/VaultCorruptException.cs
namespace Sekretujo.Core.Storage;

public sealed class VaultCorruptException : Exception
{
    public VaultCorruptException(string message) : base(message) { }
}
```

```csharp
// src/Sekretujo.Core/Storage/VaultSerializer.cs
using System.Text.Json;
using Sekretujo.Core.Models;

namespace Sekretujo.Core.Storage;

public sealed class VaultSerializer
{
    public byte[] Serialize(VaultData data) => JsonSerializer.SerializeToUtf8Bytes(data);

    public VaultData Deserialize(byte[] plaintext)
    {
        try
        {
            return JsonSerializer.Deserialize<VaultData>(plaintext)
                   ?? throw new VaultCorruptException("El vault está vacío o malformado.");
        }
        catch (JsonException)
        {
            throw new VaultCorruptException("No se pudo interpretar el contenido del vault.");
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/Sekretujo.Core.Tests`
Expected: `Passed! - Failed: 0, Passed: 3`

- [ ] **Step 5: Commit**

```bash
git add src/Sekretujo.Core/Models src/Sekretujo.Core/Storage tests/Sekretujo.Core.Tests/VaultDataSerializationTests.cs
git commit -m "feat: add vault domain models and JSON serializer"
```

---

### Task 3: CryptoService (Argon2id + AES-256-GCM)

**Files:**
- Create: `src/Sekretujo.Core/Crypto/EncryptedPayload.cs`
- Create: `src/Sekretujo.Core/Crypto/VaultUnlockException.cs`
- Create: `src/Sekretujo.Core/Crypto/CryptoService.cs`
- Test: `tests/Sekretujo.Core.Tests/CryptoServiceTests.cs`

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces: `EncryptedPayload { byte[] Salt, byte[] Nonce, byte[] Ciphertext, byte[] Tag }`,
  `CryptoService.Encrypt(string masterPassword, byte[] plaintext) -> EncryptedPayload`,
  `CryptoService.Decrypt(string masterPassword, EncryptedPayload payload) -> byte[]`
  (throws `VaultUnlockException` on wrong password / tampered ciphertext).

- [ ] **Step 1: Write the failing crypto tests**

```csharp
// tests/Sekretujo.Core.Tests/CryptoServiceTests.cs
using System.Text;
using Sekretujo.Core.Crypto;
using Xunit;

namespace Sekretujo.Core.Tests;

public class CryptoServiceTests
{
    [Fact]
    public void Encrypt_Then_Decrypt_WithCorrectPassword_RoundTrips()
    {
        var crypto = new CryptoService();
        var plaintext = Encoding.UTF8.GetBytes("hello vault");

        var payload = crypto.Encrypt("correct-password", plaintext);
        var decrypted = crypto.Decrypt("correct-password", payload);

        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void Decrypt_WithWrongPassword_ThrowsVaultUnlockException()
    {
        var crypto = new CryptoService();
        var payload = crypto.Encrypt("correct-password", Encoding.UTF8.GetBytes("secret"));

        Assert.Throws<VaultUnlockException>(() => crypto.Decrypt("wrong-password", payload));
    }

    [Fact]
    public void Decrypt_WithTamperedCiphertext_ThrowsVaultUnlockException()
    {
        var crypto = new CryptoService();
        var payload = crypto.Encrypt("correct-password", Encoding.UTF8.GetBytes("secret"));
        payload.Ciphertext[0] ^= 0xFF;

        Assert.Throws<VaultUnlockException>(() => crypto.Decrypt("correct-password", payload));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Sekretujo.Core.Tests`
Expected: FAIL — `CryptoService` does not exist.

- [ ] **Step 3: Add the Argon2 package and implement CryptoService**

```bash
dotnet add src/Sekretujo.Core/Sekretujo.Core.csproj package Konscious.Security.Cryptography.Argon2
```

```csharp
// src/Sekretujo.Core/Crypto/EncryptedPayload.cs
namespace Sekretujo.Core.Crypto;

public sealed class EncryptedPayload
{
    public required byte[] Salt { get; init; }
    public required byte[] Nonce { get; init; }
    public required byte[] Ciphertext { get; set; }
    public required byte[] Tag { get; init; }
}
```

```csharp
// src/Sekretujo.Core/Crypto/VaultUnlockException.cs
namespace Sekretujo.Core.Crypto;

public sealed class VaultUnlockException : Exception
{
    public VaultUnlockException(string message) : base(message) { }
}
```

```csharp
// src/Sekretujo.Core/Crypto/CryptoService.cs
using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace Sekretujo.Core.Crypto;

public sealed class CryptoService
{
    private const int SaltSize = 16;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32;

    public EncryptedPayload Encrypt(string masterPassword, byte[] plaintext)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = DeriveKey(masterPassword, salt);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        using var aesGcm = new AesGcm(key, TagSize);
        aesGcm.Encrypt(nonce, plaintext, ciphertext, tag);

        return new EncryptedPayload { Salt = salt, Nonce = nonce, Ciphertext = ciphertext, Tag = tag };
    }

    public byte[] Decrypt(string masterPassword, EncryptedPayload payload)
    {
        var key = DeriveKey(masterPassword, payload.Salt);
        var plaintext = new byte[payload.Ciphertext.Length];

        using var aesGcm = new AesGcm(key, TagSize);
        try
        {
            aesGcm.Decrypt(payload.Nonce, payload.Ciphertext, payload.Tag, plaintext);
        }
        catch (CryptographicException)
        {
            throw new VaultUnlockException("Contraseña incorrecta o archivo corrupto.");
        }

        return plaintext;
    }

    private static byte[] DeriveKey(string masterPassword, byte[] salt)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(masterPassword))
        {
            Salt = salt,
            DegreeOfParallelism = 4,
            MemorySize = 65536,
            Iterations = 3
        };
        return argon2.GetBytes(KeySize);
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/Sekretujo.Core.Tests`
Expected: `Passed! - Failed: 0, Passed: 6`

- [ ] **Step 5: Commit**

```bash
git add src/Sekretujo.Core/Crypto tests/Sekretujo.Core.Tests/CryptoServiceTests.cs src/Sekretujo.Core/Sekretujo.Core.csproj
git commit -m "feat: add CryptoService with Argon2id + AES-256-GCM"
```

---

### Task 4: VaultRepository (file I/O, atomic writes)

**Files:**
- Create: `src/Sekretujo.Core/Storage/VaultRepository.cs`
- Test: `tests/Sekretujo.Core.Tests/VaultRepositoryTests.cs`

**Interfaces:**
- Consumes: `CryptoService` (Task 3), `VaultSerializer` (Task 2).
- Produces: `VaultRepository(CryptoService, VaultSerializer)`,
  `VaultRepository.Save(string path, string masterPassword, VaultData data) -> void`,
  `VaultRepository.Load(string path, string masterPassword) -> VaultData` (throws
  `VaultUnlockException` on wrong password, `VaultCorruptException` on a too-short or
  unreadable file).

- [ ] **Step 1: Write the failing repository tests**

```csharp
// tests/Sekretujo.Core.Tests/VaultRepositoryTests.cs
using Sekretujo.Core.Crypto;
using Sekretujo.Core.Models;
using Sekretujo.Core.Storage;
using Xunit;

namespace Sekretujo.Core.Tests;

public class VaultRepositoryTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.vault");
    private readonly VaultRepository _repository = new(new CryptoService(), new VaultSerializer());

    [Fact]
    public void Save_Then_Load_WithCorrectPassword_RoundTrips()
    {
        var data = new VaultData { Entries = { new NoteItem { Title = "Note", Body = "content" } } };

        _repository.Save(_path, "correct-password", data);
        var loaded = _repository.Load(_path, "correct-password");

        Assert.Single(loaded.Entries);
        Assert.Equal("Note", loaded.Entries[0].Title);
    }

    [Fact]
    public void Load_WithWrongPassword_ThrowsVaultUnlockException()
    {
        _repository.Save(_path, "correct-password", new VaultData());

        Assert.Throws<VaultUnlockException>(() => _repository.Load(_path, "wrong-password"));
    }

    [Fact]
    public void Load_TruncatedFile_ThrowsVaultCorruptException()
    {
        File.WriteAllBytes(_path, new byte[10]);

        Assert.Throws<VaultCorruptException>(() => _repository.Load(_path, "any-password"));
    }

    [Fact]
    public void Save_NeverLeavesTempFileBehind()
    {
        _repository.Save(_path, "correct-password", new VaultData());

        Assert.False(File.Exists(_path + ".tmp"));
        Assert.True(File.Exists(_path));
    }

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Sekretujo.Core.Tests`
Expected: FAIL — `VaultRepository` does not exist.

- [ ] **Step 3: Implement VaultRepository**

```csharp
// src/Sekretujo.Core/Storage/VaultRepository.cs
using Sekretujo.Core.Crypto;
using Sekretujo.Core.Models;

namespace Sekretujo.Core.Storage;

public sealed class VaultRepository
{
    private const int SaltSize = 16;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int MinFileSize = SaltSize + NonceSize + TagSize;

    private readonly CryptoService _crypto;
    private readonly VaultSerializer _serializer;

    public VaultRepository(CryptoService crypto, VaultSerializer serializer)
    {
        _crypto = crypto;
        _serializer = serializer;
    }

    public void Save(string path, string masterPassword, VaultData data)
    {
        var plaintext = _serializer.Serialize(data);
        var payload = _crypto.Encrypt(masterPassword, plaintext);

        using var buffer = new MemoryStream();
        buffer.Write(payload.Salt);
        buffer.Write(payload.Nonce);
        buffer.Write(payload.Ciphertext);
        buffer.Write(payload.Tag);

        var tempPath = path + ".tmp";
        File.WriteAllBytes(tempPath, buffer.ToArray());
        File.Move(tempPath, path, overwrite: true);
    }

    public VaultData Load(string path, string masterPassword)
    {
        if (!File.Exists(path))
            throw new VaultCorruptException("No se encontró el archivo de vault.");

        var bytes = File.ReadAllBytes(path);
        if (bytes.Length < MinFileSize)
            throw new VaultCorruptException("El archivo de vault está corrupto o incompleto.");

        var salt = bytes[..SaltSize];
        var nonce = bytes[SaltSize..(SaltSize + NonceSize)];
        var tag = bytes[^TagSize..];
        var ciphertext = bytes[(SaltSize + NonceSize)..^TagSize];

        var payload = new EncryptedPayload { Salt = salt, Nonce = nonce, Ciphertext = ciphertext, Tag = tag };
        var plaintext = _crypto.Decrypt(masterPassword, payload);
        return _serializer.Deserialize(plaintext);
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/Sekretujo.Core.Tests`
Expected: `Passed! - Failed: 0, Passed: 10`

- [ ] **Step 5: Commit**

```bash
git add src/Sekretujo.Core/Storage/VaultRepository.cs tests/Sekretujo.Core.Tests/VaultRepositoryTests.cs
git commit -m "feat: add VaultRepository with atomic save/load"
```

---

### Task 5: PasswordGeneratorService

**Files:**
- Create: `src/Sekretujo.Core/Services/PasswordGeneratorService.cs`
- Test: `tests/Sekretujo.Core.Tests/PasswordGeneratorServiceTests.cs`

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces: `PasswordGeneratorOptions { int Length, bool UseUppercase, bool UseLowercase,
  bool UseDigits, bool UseSymbols }`, `PasswordGeneratorService.Generate(options) ->
  string` (throws `ArgumentException` when no charset selected or `Length <= 0`).

- [ ] **Step 1: Write the failing generator tests**

```csharp
// tests/Sekretujo.Core.Tests/PasswordGeneratorServiceTests.cs
using Sekretujo.Core.Services;
using Xunit;

namespace Sekretujo.Core.Tests;

public class PasswordGeneratorServiceTests
{
    [Fact]
    public void Generate_ReturnsStringOfRequestedLength()
    {
        var generator = new PasswordGeneratorService();
        var options = new PasswordGeneratorOptions { Length = 24, UseUppercase = true, UseDigits = true };

        var password = generator.Generate(options);

        Assert.Equal(24, password.Length);
    }

    [Fact]
    public void Generate_WithNoCharsetSelected_ThrowsArgumentException()
    {
        var generator = new PasswordGeneratorService();
        var options = new PasswordGeneratorOptions
        {
            Length = 10,
            UseUppercase = false,
            UseLowercase = false,
            UseDigits = false,
            UseSymbols = false
        };

        Assert.Throws<ArgumentException>(() => generator.Generate(options));
    }

    [Fact]
    public void Generate_WithZeroLength_ThrowsArgumentException()
    {
        var generator = new PasswordGeneratorService();
        var options = new PasswordGeneratorOptions { Length = 0, UseLowercase = true };

        Assert.Throws<ArgumentException>(() => generator.Generate(options));
    }

    [Fact]
    public void Generate_OnlyUsesSelectedCharsets()
    {
        var generator = new PasswordGeneratorService();
        var options = new PasswordGeneratorOptions
        {
            Length = 50,
            UseUppercase = false,
            UseLowercase = false,
            UseDigits = true,
            UseSymbols = false
        };

        var password = generator.Generate(options);

        Assert.All(password, c => Assert.True(char.IsDigit(c)));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Sekretujo.Core.Tests`
Expected: FAIL — `PasswordGeneratorService` does not exist.

- [ ] **Step 3: Implement PasswordGeneratorService**

```csharp
// src/Sekretujo.Core/Services/PasswordGeneratorService.cs
using System.Security.Cryptography;
using System.Text;

namespace Sekretujo.Core.Services;

public sealed class PasswordGeneratorOptions
{
    public int Length { get; set; } = 20;
    public bool UseUppercase { get; set; } = true;
    public bool UseLowercase { get; set; } = true;
    public bool UseDigits { get; set; } = true;
    public bool UseSymbols { get; set; } = true;
}

public sealed class PasswordGeneratorService
{
    private const string Uppercase = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Lowercase = "abcdefghijkmnopqrstuvwxyz";
    private const string Digits = "23456789";
    private const string Symbols = "!@#$%^&*-_=+?";

    public string Generate(PasswordGeneratorOptions options)
    {
        if (options.Length <= 0)
            throw new ArgumentException("La longitud debe ser mayor que cero.", nameof(options));

        var charset = BuildCharset(options);
        if (charset.Length == 0)
            throw new ArgumentException("Debe seleccionar al menos un tipo de carácter.", nameof(options));

        var result = new char[options.Length];
        for (var i = 0; i < options.Length; i++)
        {
            var index = RandomNumberGenerator.GetInt32(charset.Length);
            result[i] = charset[index];
        }
        return new string(result);
    }

    private static string BuildCharset(PasswordGeneratorOptions options)
    {
        var sb = new StringBuilder();
        if (options.UseUppercase) sb.Append(Uppercase);
        if (options.UseLowercase) sb.Append(Lowercase);
        if (options.UseDigits) sb.Append(Digits);
        if (options.UseSymbols) sb.Append(Symbols);
        return sb.ToString();
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/Sekretujo.Core.Tests`
Expected: `Passed! - Failed: 0, Passed: 14`

- [ ] **Step 5: Commit**

```bash
git add src/Sekretujo.Core/Services/PasswordGeneratorService.cs tests/Sekretujo.Core.Tests/PasswordGeneratorServiceTests.cs
git commit -m "feat: add CSPRNG-based password generator"
```

---

### Task 6: TotpService

**Files:**
- Create: `src/Sekretujo.Core/Services/TotpService.cs`
- Create: `src/Sekretujo.Core/Services/InvalidTotpSecretException.cs`
- Test: `tests/Sekretujo.Core.Tests/TotpServiceTests.cs`

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces: `TotpCode { string Code, int SecondsRemaining }`,
  `TotpService.GetCode(string base32Secret, DateTimeOffset? at = null) -> TotpCode`
  (throws `InvalidTotpSecretException` when the secret is not valid Base32).

- [ ] **Step 1: Write the failing TOTP tests**

```csharp
// tests/Sekretujo.Core.Tests/TotpServiceTests.cs
using Sekretujo.Core.Services;
using Xunit;

namespace Sekretujo.Core.Tests;

public class TotpServiceTests
{
    private const string ValidSecret = "JBSWY3DPEHPK3PXP";

    [Fact]
    public void GetCode_ReturnsSixDigitCode()
    {
        var service = new TotpService();

        var result = service.GetCode(ValidSecret, DateTimeOffset.UtcNow);

        Assert.Equal(6, result.Code.Length);
        Assert.All(result.Code, char.IsDigit);
    }

    [Fact]
    public void GetCode_IsDeterministic_ForSameSecretAndTimestamp()
    {
        var service = new TotpService();
        var at = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var first = service.GetCode(ValidSecret, at);
        var second = service.GetCode(ValidSecret, at);

        Assert.Equal(first.Code, second.Code);
    }

    [Fact]
    public void GetCode_DiffersAcrossTimeWindows()
    {
        var service = new TotpService();
        var windowOne = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var windowTwo = windowOne.AddSeconds(60);

        var first = service.GetCode(ValidSecret, windowOne);
        var second = service.GetCode(ValidSecret, windowTwo);

        Assert.NotEqual(first.Code, second.Code);
    }

    [Fact]
    public void GetCode_WithInvalidBase32Secret_ThrowsInvalidTotpSecretException()
    {
        var service = new TotpService();

        Assert.Throws<InvalidTotpSecretException>(() => service.GetCode("not-valid-base32!!"));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Sekretujo.Core.Tests`
Expected: FAIL — `TotpService` does not exist.

- [ ] **Step 3: Add Otp.NET and implement TotpService**

```bash
dotnet add src/Sekretujo.Core/Sekretujo.Core.csproj package Otp.NET
```

```csharp
// src/Sekretujo.Core/Services/InvalidTotpSecretException.cs
namespace Sekretujo.Core.Services;

public sealed class InvalidTotpSecretException : Exception
{
    public InvalidTotpSecretException(string message, Exception inner) : base(message, inner) { }
}
```

```csharp
// src/Sekretujo.Core/Services/TotpService.cs
using OtpNet;

namespace Sekretujo.Core.Services;

public sealed class TotpCode
{
    public required string Code { get; init; }
    public required int SecondsRemaining { get; init; }
}

public sealed class TotpService
{
    public TotpCode GetCode(string base32Secret, DateTimeOffset? at = null)
    {
        byte[] secretBytes;
        try
        {
            secretBytes = Base32Encoding.ToBytes(base32Secret);
        }
        catch (Exception ex)
        {
            throw new InvalidTotpSecretException("El secreto TOTP no es Base32 válido.", ex);
        }

        var totp = new Totp(secretBytes);
        var timestamp = (at ?? DateTimeOffset.UtcNow).UtcDateTime;
        var code = totp.ComputeTotp(timestamp);
        var remaining = totp.RemainingSeconds(timestamp);
        return new TotpCode { Code = code, SecondsRemaining = remaining };
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/Sekretujo.Core.Tests`
Expected: `Passed! - Failed: 0, Passed: 18`

- [ ] **Step 5: Commit**

```bash
git add src/Sekretujo.Core/Services/TotpService.cs src/Sekretujo.Core/Services/InvalidTotpSecretException.cs tests/Sekretujo.Core.Tests/TotpServiceTests.cs src/Sekretujo.Core/Sekretujo.Core.csproj
git commit -m "feat: add TOTP code generation service"
```

---

### Task 7: ImportExportService (CSV, Bitwarden JSON, KeePass CSV)

**Files:**
- Create: `src/Sekretujo.Core/Services/ImportExportService.cs`
- Test: `tests/Sekretujo.Core.Tests/ImportExportServiceTests.cs`

**Interfaces:**
- Consumes: `PasswordItem` (Task 2).
- Produces: `ImportExportService.ExportCsv(IEnumerable<PasswordItem>) -> string`,
  `.ImportCsv(string) -> List<PasswordItem>`, `.ImportBitwardenJson(string) ->
  List<PasswordItem>`, `.ImportKeePassCsv(string) -> List<PasswordItem>`.

- [ ] **Step 1: Write the failing import/export tests**

```csharp
// tests/Sekretujo.Core.Tests/ImportExportServiceTests.cs
using Sekretujo.Core.Models;
using Sekretujo.Core.Services;
using Xunit;

namespace Sekretujo.Core.Tests;

public class ImportExportServiceTests
{
    [Fact]
    public void ExportCsv_Then_ImportCsv_RoundTripsEntryWithEmbeddedComma()
    {
        var service = new ImportExportService();
        var items = new List<PasswordItem>
        {
            new() { Title = "Bank", Username = "me", Password = "p@ss", Url = "https://bank.test",
                     Notes = "contains, a comma and \"quotes\"", Tags = { "finance" } }
        };

        var csv = service.ExportCsv(items);
        var imported = service.ImportCsv(csv);

        Assert.Single(imported);
        Assert.Equal("Bank", imported[0].Title);
        Assert.Equal("contains, a comma and \"quotes\"", imported[0].Notes);
        Assert.Equal("finance", imported[0].Tags[0]);
    }

    [Fact]
    public void ImportBitwardenJson_ParsesLoginItems()
    {
        var service = new ImportExportService();
        const string json = """
        {
          "items": [
            {
              "name": "Example",
              "notes": "a note",
              "login": {
                "username": "alice",
                "password": "hunter2",
                "uris": [ { "uri": "https://example.com" } ]
              }
            }
          ]
        }
        """;

        var imported = service.ImportBitwardenJson(json);

        Assert.Single(imported);
        Assert.Equal("Example", imported[0].Title);
        Assert.Equal("alice", imported[0].Username);
        Assert.Equal("hunter2", imported[0].Password);
        Assert.Equal("https://example.com", imported[0].Url);
    }

    [Fact]
    public void ImportKeePassCsv_ParsesStandardColumns()
    {
        var service = new ImportExportService();
        const string csv = "\"Title\",\"User Name\",\"Password\",\"URL\",\"Notes\"\n" +
                            "\"Email\",\"bob\",\"pw123\",\"https://mail.test\",\"work mail\"\n";

        var imported = service.ImportKeePassCsv(csv);

        Assert.Single(imported);
        Assert.Equal("Email", imported[0].Title);
        Assert.Equal("bob", imported[0].Username);
        Assert.Equal("pw123", imported[0].Password);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Sekretujo.Core.Tests`
Expected: FAIL — `ImportExportService` does not exist.

- [ ] **Step 3: Implement ImportExportService**

```csharp
// src/Sekretujo.Core/Services/ImportExportService.cs
using System.Text;
using System.Text.Json;
using Sekretujo.Core.Models;

namespace Sekretujo.Core.Services;

public sealed class ImportExportService
{
    public string ExportCsv(IEnumerable<PasswordItem> items)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Title,Username,Password,Url,Notes,Tags");
        foreach (var item in items)
        {
            sb.AppendLine(string.Join(',',
                CsvField(item.Title), CsvField(item.Username), CsvField(item.Password),
                CsvField(item.Url), CsvField(item.Notes), CsvField(string.Join(';', item.Tags))));
        }
        return sb.ToString();
    }

    public List<PasswordItem> ImportCsv(string csv)
    {
        var rows = ParseCsvRows(csv);
        var items = new List<PasswordItem>();
        foreach (var fields in rows.Skip(1))
        {
            if (fields.Count == 0 || fields.All(string.IsNullOrEmpty)) continue;
            items.Add(new PasswordItem
            {
                Title = Field(fields, 0),
                Username = Field(fields, 1),
                Password = Field(fields, 2),
                Url = Field(fields, 3),
                Notes = Field(fields, 4),
                Tags = Field(fields, 5) is { Length: > 0 } tags ? tags.Split(';').ToList() : new()
            });
        }
        return items;
    }

    public List<PasswordItem> ImportBitwardenJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        var result = new List<PasswordItem>();
        foreach (var element in document.RootElement.GetProperty("items").EnumerateArray())
        {
            if (!element.TryGetProperty("login", out var login)) continue;

            var uri = login.TryGetProperty("uris", out var uris) && uris.GetArrayLength() > 0
                ? uris[0].GetProperty("uri").GetString() ?? string.Empty
                : string.Empty;

            result.Add(new PasswordItem
            {
                Title = element.GetProperty("name").GetString() ?? string.Empty,
                Username = GetStringOrEmpty(login, "username"),
                Password = GetStringOrEmpty(login, "password"),
                Url = uri,
                Notes = GetStringOrEmpty(element, "notes")
            });
        }
        return result;
    }

    public List<PasswordItem> ImportKeePassCsv(string csv)
    {
        var rows = ParseCsvRows(csv);
        if (rows.Count == 0) return new();

        var headers = rows[0];
        int IndexOf(string name) => headers.FindIndex(h => h.Equals(name, StringComparison.OrdinalIgnoreCase));
        var titleIdx = IndexOf("Title");
        var userIdx = IndexOf("User Name");
        var passIdx = IndexOf("Password");
        var urlIdx = IndexOf("URL");
        var notesIdx = IndexOf("Notes");

        var items = new List<PasswordItem>();
        foreach (var fields in rows.Skip(1))
        {
            if (fields.Count == 0 || fields.All(string.IsNullOrEmpty)) continue;
            items.Add(new PasswordItem
            {
                Title = Field(fields, titleIdx),
                Username = Field(fields, userIdx),
                Password = Field(fields, passIdx),
                Url = Field(fields, urlIdx),
                Notes = Field(fields, notesIdx)
            });
        }
        return items;
    }

    private static string GetStringOrEmpty(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) ? value.GetString() ?? string.Empty : string.Empty;

    private static string Field(List<string> fields, int index) =>
        index >= 0 && index < fields.Count ? fields[index] : string.Empty;

    private static string CsvField(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    private static List<List<string>> ParseCsvRows(string csv)
    {
        var rows = new List<List<string>>();
        using var reader = new StringReader(csv);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (line.Length == 0) continue;
            rows.Add(ParseCsvLine(line));
        }
        return rows;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                    else inQuotes = false;
                }
                else current.Append(c);
            }
            else
            {
                if (c == '"') inQuotes = true;
                else if (c == ',') { fields.Add(current.ToString()); current.Clear(); }
                else current.Append(c);
            }
        }
        fields.Add(current.ToString());
        return fields;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/Sekretujo.Core.Tests`
Expected: `Passed! - Failed: 0, Passed: 21`

- [ ] **Step 5: Commit**

```bash
git add src/Sekretujo.Core/Services/ImportExportService.cs tests/Sekretujo.Core.Tests/ImportExportServiceTests.cs
git commit -m "feat: add CSV/Bitwarden/KeePass import-export service"
```

---

### Task 8: Avalonia app scaffold with FluentAvalonia theme and DI

**Files:**
- Create: `src/Sekretujo.App/Sekretujo.App.csproj` (via template)
- Create: `src/Sekretujo.App/App.axaml`, `App.axaml.cs`
- Create: `src/Sekretujo.App/ServiceConfiguration.cs`
- Create: `src/Sekretujo.App/Services/VaultSession.cs`
- Create: `src/Sekretujo.App/ViewModels/ViewModelBase.cs`
- Create: `src/Sekretujo.App/ViewModels/MainWindowViewModel.cs`
- Create: `src/Sekretujo.App/Views/MainWindow.axaml`, `MainWindow.axaml.cs`

**Interfaces:**
- Consumes: `CryptoService`, `VaultSerializer`, `VaultRepository`,
  `PasswordGeneratorService`, `TotpService`, `ImportExportService` (all from `Core`).
- Produces: `VaultSession` (`Data`, `FilePath`, `MasterPassword`, `IsOpen`, `Open(...)`,
  `Close()`), `ViewModelBase` (empty marker `: ObservableObject`), a running shell that
  will later host `UnlockViewModel` / `ShellViewModel` via `MainWindowViewModel.CurrentView`.

- [ ] **Step 1: Scaffold the Avalonia project**

```bash
cd /home/manuel/projects/dotnet/sekretujo
dotnet new install Avalonia.Templates
dotnet new avalonia.app -n Sekretujo.App -o src/Sekretujo.App -f net10.0
dotnet sln add src/Sekretujo.App/Sekretujo.App.csproj
dotnet add src/Sekretujo.App/Sekretujo.App.csproj reference src/Sekretujo.Core/Sekretujo.Core.csproj
dotnet add src/Sekretujo.App/Sekretujo.App.csproj package FluentAvalonia
dotnet add src/Sekretujo.App/Sekretujo.App.csproj package CommunityToolkit.Mvvm
dotnet add src/Sekretujo.App/Sekretujo.App.csproj package Microsoft.Extensions.DependencyInjection
```

- [ ] **Step 2: Apply the FluentAvalonia theme**

```xml
<!-- src/Sekretujo.App/App.axaml -->
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:styles="using:FluentAvalonia.Styling"
             x:Class="Sekretujo.App.App"
             RequestedThemeVariant="Dark">
  <Application.Styles>
    <styles:FluentAvaloniaTheme />
  </Application.Styles>
</Application>
```

- [ ] **Step 3: Add VaultSession and ViewModelBase**

```csharp
// src/Sekretujo.App/Services/VaultSession.cs
using Sekretujo.Core.Models;

namespace Sekretujo.App.Services;

public sealed class VaultSession
{
    public VaultData? Data { get; private set; }
    public string? FilePath { get; private set; }
    public string? MasterPassword { get; private set; }

    public bool IsOpen => Data is not null;

    public void Open(string filePath, string masterPassword, VaultData data)
    {
        FilePath = filePath;
        MasterPassword = masterPassword;
        Data = data;
    }

    public void Close()
    {
        Data = null;
        FilePath = null;
        MasterPassword = null;
    }
}
```

```csharp
// src/Sekretujo.App/ViewModels/ViewModelBase.cs
using CommunityToolkit.Mvvm.ComponentModel;

namespace Sekretujo.App.ViewModels;

public abstract class ViewModelBase : ObservableObject
{
}
```

- [ ] **Step 4: Wire dependency injection**

```csharp
// src/Sekretujo.App/ServiceConfiguration.cs
using Microsoft.Extensions.DependencyInjection;
using Sekretujo.App.Services;
using Sekretujo.App.ViewModels;
using Sekretujo.Core.Crypto;
using Sekretujo.Core.Services;
using Sekretujo.Core.Storage;

namespace Sekretujo.App;

public static class ServiceConfiguration
{
    public static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddSingleton<CryptoService>();
        services.AddSingleton<VaultSerializer>();
        services.AddSingleton<VaultRepository>();
        services.AddSingleton<PasswordGeneratorService>();
        services.AddSingleton<TotpService>();
        services.AddSingleton<ImportExportService>();
        services.AddSingleton<VaultSession>();
        services.AddSingleton<MainWindowViewModel>();

        return services.BuildServiceProvider();
    }
}
```

```csharp
// src/Sekretujo.App/ViewModels/MainWindowViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;

namespace Sekretujo.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private ViewModelBase? _currentView;
}
```

```csharp
// src/Sekretujo.App/App.axaml.cs
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Sekretujo.App.ViewModels;
using Sekretujo.App.Views;

namespace Sekretujo.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var services = ServiceConfiguration.BuildServiceProvider();
            desktop.MainWindow = new MainWindow
            {
                DataContext = services.GetRequiredService<MainWindowViewModel>()
            };
        }
        base.OnFrameworkInitializationCompleted();
    }
}
```

```xml
<!-- src/Sekretujo.App/Views/MainWindow.axaml -->
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="using:Sekretujo.App.ViewModels"
        x:Class="Sekretujo.App.Views.MainWindow"
        x:DataType="vm:MainWindowViewModel"
        Title="Sekretujo" Width="1000" Height="650">
  <ContentControl Content="{Binding CurrentView}" />
</Window>
```

- [ ] **Step 5: Build and run manually to verify the shell launches**

Run: `dotnet run --project src/Sekretujo.App`
Expected: a window titled "Sekretujo" opens with a dark Fluent theme and no crash (the
content area is empty — `CurrentView` is not set yet, that's expected at this stage).

- [ ] **Step 6: Commit**

```bash
git add src/Sekretujo.App Sekretujo.sln
git commit -m "feat: scaffold Avalonia app with FluentAvalonia theme and DI"
```

---

### Task 9: Unlock flow (create / open vault)

**Files:**
- Create: `src/Sekretujo.App/ViewModels/UnlockViewModel.cs`
- Create: `src/Sekretujo.App/Views/UnlockView.axaml`, `UnlockView.axaml.cs`
- Modify: `src/Sekretujo.App/App.axaml` (register `DataTemplates`)
- Modify: `src/Sekretujo.App/ServiceConfiguration.cs` (register `UnlockViewModel`,
  set initial `CurrentView`)

**Interfaces:**
- Consumes: `VaultRepository.Load/Save` (Task 4 via Core), `VaultSession.Open` (Task 8).
- Produces: `UnlockViewModel` with `VaultPath`, `MasterPassword`, `ErrorMessage`,
  `OpenExistingCommand`, `CreateNewCommand`, and `event Action? Unlocked`.

- [ ] **Step 1: Implement UnlockViewModel**

```csharp
// src/Sekretujo.App/ViewModels/UnlockViewModel.cs
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
```

- [ ] **Step 2: Implement UnlockView**

```xml
<!-- src/Sekretujo.App/Views/UnlockView.axaml -->
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:Sekretujo.App.ViewModels"
             x:Class="Sekretujo.App.Views.UnlockView"
             x:DataType="vm:UnlockViewModel">
  <StackPanel Spacing="12" Width="360" HorizontalAlignment="Center" VerticalAlignment="Center">
    <TextBlock Text="Sekretujo" FontSize="28" FontWeight="Bold" HorizontalAlignment="Center" />
    <TextBox Watermark="Ruta del archivo .vault" Text="{Binding VaultPath}" />
    <TextBox Watermark="Master password" PasswordChar="•" Text="{Binding MasterPassword}" />
    <TextBlock Text="{Binding ErrorMessage}" Foreground="Red" />
    <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Center">
      <Button Content="Abrir vault" Command="{Binding OpenExistingCommand}" />
      <Button Content="Crear vault nuevo" Command="{Binding CreateNewCommand}" />
    </StackPanel>
  </StackPanel>
</UserControl>
```

```csharp
// src/Sekretujo.App/Views/UnlockView.axaml.cs
using Avalonia.Controls;

namespace Sekretujo.App.Views;

public partial class UnlockView : UserControl
{
    public UnlockView() => InitializeComponent();
}
```

- [ ] **Step 3: Register the DataTemplate and wire up the initial view**

```xml
<!-- src/Sekretujo.App/App.axaml, inside <Application> after Application.Styles -->
<Application.DataTemplates>
  <DataTemplate x:DataType="{x:Type vm:UnlockViewModel}">
    <views:UnlockView />
  </DataTemplate>
</Application.DataTemplates>
```

Add the two namespaces to the `<Application>` root tag:
`xmlns:vm="using:Sekretujo.App.ViewModels"` and
`xmlns:views="using:Sekretujo.App.Views"`.

```csharp
// src/Sekretujo.App/ViewModels/MainWindowViewModel.cs — replace the class body
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace Sekretujo.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IServiceProvider _services;

    [ObservableProperty]
    private ViewModelBase? _currentView;

    public MainWindowViewModel(IServiceProvider services)
    {
        _services = services;
        var unlock = services.GetRequiredService<UnlockViewModel>();
        unlock.Unlocked += () => CurrentView = services.GetRequiredService<UnlockViewModel>();
        CurrentView = unlock;
    }
}
```

```csharp
// src/Sekretujo.App/ServiceConfiguration.cs — add this line among the registrations
services.AddTransient<UnlockViewModel>();
services.AddSingleton<IServiceProvider>(provider => provider);
```

> **Note:** `MainWindowViewModel` is registered as a factory consuming `IServiceProvider`
> directly; register it with `services.AddSingleton<MainWindowViewModel>()` as before —
> the DI container injects itself automatically when a constructor asks for
> `IServiceProvider`.

- [ ] **Step 4: Manual verification**

Run: `dotnet run --project src/Sekretujo.App`
Checklist:
1. The window shows the Unlock form.
2. Enter a path like `/tmp/test1.vault` and a password, click "Crear vault nuevo" — no
   crash, and `/tmp/test1.vault` exists on disk afterward (check with `ls -la /tmp/test1.vault`).
3. Restart the app, enter the same path and password, click "Abrir vault" — no error shown.
4. Restart again, enter the same path with a wrong password — `ErrorMessage` shows
   "Contraseña incorrecta o archivo corrupto."

- [ ] **Step 5: Commit**

```bash
git add src/Sekretujo.App
git commit -m "feat: add vault unlock/create flow"
```

---

### Task 10: Entry list with search and filters

**Files:**
- Create: `src/Sekretujo.App/ViewModels/EntryListItemViewModel.cs`
- Create: `src/Sekretujo.App/ViewModels/ShellViewModel.cs`
- Create: `src/Sekretujo.App/Views/ShellView.axaml`, `ShellView.axaml.cs`
- Modify: `src/Sekretujo.App/App.axaml` (register `ShellViewModel` DataTemplate)
- Modify: `src/Sekretujo.App/ServiceConfiguration.cs` (register `ShellViewModel`)
- Modify: `src/Sekretujo.App/ViewModels/MainWindowViewModel.cs` (switch to `ShellViewModel`
  once `UnlockViewModel.Unlocked` fires)
- Modify: `src/Sekretujo.App/ViewModels/UnlockViewModel.cs` (none — already exposes
  `Unlocked`)

**Interfaces:**
- Consumes: `VaultSession.Data` (Task 8).
- Produces: `EntryListItemViewModel { Guid Id, string Title, bool IsFavorite, bool
  IsPassword }`, `ShellViewModel` with `ObservableCollection<EntryListItemViewModel>
  Entries`, `string SearchText`, `bool FavoritesOnly`, `EntryListItemViewModel? SelectedEntry`,
  `event Action? Locked`.

- [ ] **Step 1: Implement EntryListItemViewModel**

```csharp
// src/Sekretujo.App/ViewModels/EntryListItemViewModel.cs
using Sekretujo.Core.Models;

namespace Sekretujo.App.ViewModels;

public sealed class EntryListItemViewModel
{
    public EntryListItemViewModel(VaultEntry entry)
    {
        Id = entry.Id;
        Title = entry.Title;
        IsFavorite = entry.IsFavorite;
        IsPassword = entry is PasswordItem;
        Tags = entry.Tags;
    }

    public Guid Id { get; }
    public string Title { get; }
    public bool IsFavorite { get; }
    public bool IsPassword { get; }
    public List<string> Tags { get; }
}
```

- [ ] **Step 2: Implement ShellViewModel with search/filter logic**

```csharp
// src/Sekretujo.App/ViewModels/ShellViewModel.cs
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sekretujo.App.Services;

namespace Sekretujo.App.ViewModels;

public partial class ShellViewModel : ViewModelBase
{
    private readonly VaultSession _session;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _favoritesOnly;

    [ObservableProperty]
    private EntryListItemViewModel? _selectedEntry;

    public ObservableCollection<EntryListItemViewModel> Entries { get; } = new();

    public event Action? Locked;

    public ShellViewModel(VaultSession session)
    {
        _session = session;
        Refresh();
    }

    partial void OnSearchTextChanged(string value) => Refresh();
    partial void OnFavoritesOnlyChanged(bool value) => Refresh();

    [RelayCommand]
    private void Lock()
    {
        _session.Close();
        Locked?.Invoke();
    }

    public void Refresh()
    {
        Entries.Clear();
        if (_session.Data is null) return;

        var query = _session.Data.Entries.AsEnumerable();
        if (FavoritesOnly)
            query = query.Where(e => e.IsFavorite);
        if (!string.IsNullOrWhiteSpace(SearchText))
            query = query.Where(e => e.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

        foreach (var entry in query)
            Entries.Add(new EntryListItemViewModel(entry));
    }
}
```

- [ ] **Step 3: Implement ShellView**

```xml
<!-- src/Sekretujo.App/Views/ShellView.axaml -->
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:Sekretujo.App.ViewModels"
             x:Class="Sekretujo.App.Views.ShellView"
             x:DataType="vm:ShellViewModel">
  <Grid ColumnDefinitions="300,*" RowDefinitions="Auto,*">
    <StackPanel Grid.Row="0" Grid.ColumnSpan="2" Orientation="Horizontal" Spacing="8" Margin="8">
      <TextBox Watermark="Buscar..." Text="{Binding SearchText}" Width="200" />
      <CheckBox Content="Solo favoritos" IsChecked="{Binding FavoritesOnly}" />
      <Button Content="Bloquear" Command="{Binding LockCommand}" HorizontalAlignment="Right" />
    </StackPanel>
    <ListBox Grid.Row="1" Grid.Column="0" ItemsSource="{Binding Entries}"
              SelectedItem="{Binding SelectedEntry}">
      <ListBox.ItemTemplate>
        <DataTemplate x:DataType="vm:EntryListItemViewModel">
          <StackPanel Orientation="Horizontal" Spacing="6">
            <TextBlock Text="★" IsVisible="{Binding IsFavorite}" />
            <TextBlock Text="{Binding Title}" />
          </StackPanel>
        </DataTemplate>
      </ListBox.ItemTemplate>
    </ListBox>
    <ContentControl Grid.Row="1" Grid.Column="1" Margin="12" />
  </Grid>
</UserControl>
```

```csharp
// src/Sekretujo.App/Views/ShellView.axaml.cs
using Avalonia.Controls;

namespace Sekretujo.App.Views;

public partial class ShellView : UserControl
{
    public ShellView() => InitializeComponent();
}
```

- [ ] **Step 4: Register ShellViewModel and switch to it after unlocking**

```xml
<!-- src/Sekretujo.App/App.axaml — add alongside the UnlockViewModel DataTemplate -->
<DataTemplate x:DataType="{x:Type vm:ShellViewModel}">
  <views:ShellView />
</DataTemplate>
```

```csharp
// src/Sekretujo.App/ServiceConfiguration.cs — add
services.AddTransient<ShellViewModel>();
```

```csharp
// src/Sekretujo.App/ViewModels/MainWindowViewModel.cs — replace constructor body
public MainWindowViewModel(IServiceProvider services)
{
    _services = services;
    ShowUnlock();
}

private void ShowUnlock()
{
    var unlock = _services.GetRequiredService<UnlockViewModel>();
    unlock.Unlocked += ShowShell;
    CurrentView = unlock;
}

private void ShowShell()
{
    var shell = _services.GetRequiredService<ShellViewModel>();
    shell.Locked += ShowUnlock;
    CurrentView = shell;
}
```

- [ ] **Step 5: Manual verification**

Run: `dotnet run --project src/Sekretujo.App`
Checklist:
1. Create/open a vault — the Shell view appears with an empty entry list (new vault has
   no entries yet).
2. Click "Bloquear" — returns to the Unlock view.

- [ ] **Step 6: Commit**

```bash
git add src/Sekretujo.App
git commit -m "feat: add entry list with search and favorites filter"
```

---

### Task 11: Entry detail — create, edit, delete, generator, TOTP, clipboard

**Files:**
- Create: `src/Sekretujo.App/Services/ClipboardAutoClearService.cs`
- Create: `src/Sekretujo.App/ViewModels/EntryDetailViewModel.cs`
- Create: `src/Sekretujo.App/Views/EntryDetailView.axaml`, `EntryDetailView.axaml.cs`
- Modify: `src/Sekretujo.App/Views/ShellView.axaml` (bind the right-hand `ContentControl`
  to the detail view model)
- Modify: `src/Sekretujo.App/ViewModels/ShellViewModel.cs` (own an
  `EntryDetailViewModel? Detail`, create/delete commands, save-back-to-session)
- Modify: `src/Sekretujo.App/ServiceConfiguration.cs` (register
  `ClipboardAutoClearService`, `EntryDetailViewModel`)

**Interfaces:**
- Consumes: `PasswordGeneratorService.Generate`, `TotpService.GetCode`,
  `InvalidTotpSecretException` (Core, Tasks 5–6), `VaultSession.Data` (Task 8).
- Produces: `ClipboardAutoClearService.CopyAndClear(IClipboard clipboard, string value,
  TimeSpan after) -> Task`, `EntryDetailViewModel` with `Title`, `Username`, `Password`,
  `Url`, `Notes`, `TotpSecret`, `IsNote`, `Body`, `TotpCodeDisplay`, `TotpErrorMessage`,
  `GeneratePasswordCommand`, `CopyPasswordCommand`, `CopyTotpCommand`, `SaveCommand`,
  `DeleteCommand`.

- [ ] **Step 1: Implement ClipboardAutoClearService**

```csharp
// src/Sekretujo.App/Services/ClipboardAutoClearService.cs
using Avalonia.Input.Platform;

namespace Sekretujo.App.Services;

public sealed class ClipboardAutoClearService
{
    public async Task CopyAndClear(IClipboard clipboard, string value, TimeSpan after)
    {
        await clipboard.SetTextAsync(value);
        _ = ClearLater(clipboard, value, after);
    }

    private static async Task ClearLater(IClipboard clipboard, string copiedValue, TimeSpan after)
    {
        await Task.Delay(after);
        var current = await clipboard.GetTextAsync();
        if (current == copiedValue)
            await clipboard.ClearAsync();
    }
}
```

- [ ] **Step 2: Implement EntryDetailViewModel**

```csharp
// src/Sekretujo.App/ViewModels/EntryDetailViewModel.cs
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
```

- [ ] **Step 3: Implement EntryDetailView**

```xml
<!-- src/Sekretujo.App/Views/EntryDetailView.axaml -->
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:Sekretujo.App.ViewModels"
             x:Class="Sekretujo.App.Views.EntryDetailView"
             x:DataType="vm:EntryDetailViewModel">
  <StackPanel Spacing="8">
    <TextBox Watermark="Título" Text="{Binding Title}" />
    <StackPanel IsVisible="{Binding !IsNote}" Spacing="8">
      <TextBox Watermark="Usuario" Text="{Binding Username}" />
      <StackPanel Orientation="Horizontal" Spacing="8">
        <TextBox Watermark="Password" Text="{Binding Password}" Width="240" />
        <Button Content="Generar" Command="{Binding GeneratePasswordCommand}" />
        <Button Content="Copiar" Command="{Binding CopyPasswordCommand}" />
      </StackPanel>
      <TextBox Watermark="URL" Text="{Binding Url}" />
      <TextBox Watermark="Notas" Text="{Binding Notes}" AcceptsReturn="True" Height="80" />
      <TextBox Watermark="Secreto TOTP (Base32)" Text="{Binding TotpSecret}" />
      <StackPanel Orientation="Horizontal" Spacing="8">
        <TextBlock Text="{Binding TotpCodeDisplay}" />
        <TextBlock Text="{Binding TotpErrorMessage}" Foreground="Red" />
        <Button Content="Copiar TOTP" Command="{Binding CopyTotpCommand}" />
      </StackPanel>
    </StackPanel>
    <TextBox IsVisible="{Binding IsNote}" Watermark="Contenido" Text="{Binding Body}"
             AcceptsReturn="True" Height="200" />
    <StackPanel Orientation="Horizontal" Spacing="8">
      <Button Content="Guardar" Command="{Binding SaveCommand}" />
      <Button Content="Eliminar" Command="{Binding DeleteCommand}" />
    </StackPanel>
  </StackPanel>
</UserControl>
```

```csharp
// src/Sekretujo.App/Views/EntryDetailView.axaml.cs
using Avalonia.Controls;

namespace Sekretujo.App.Views;

public partial class EntryDetailView : UserControl
{
    public EntryDetailView() => InitializeComponent();
}
```

- [ ] **Step 4: Wire EntryDetail into ShellViewModel**

```csharp
// src/Sekretujo.App/ViewModels/ShellViewModel.cs — add fields/members
using Microsoft.Extensions.DependencyInjection;
// ...
private readonly IServiceProvider _services;

[ObservableProperty]
private EntryDetailViewModel? _detail;

public ShellViewModel(VaultSession session, IServiceProvider services)
{
    _session = session;
    _services = services;
    Refresh();
}

partial void OnSelectedEntryChanged(EntryListItemViewModel? value)
{
    if (value is null || _session.Data is null) { Detail = null; return; }

    var entry = _session.Data.Entries.First(e => e.Id == value.Id);
    var detail = _services.GetRequiredService<EntryDetailViewModel>();
    detail.LoadFrom(entry);
    detail.Saved += OnEntrySaved;
    detail.DeleteRequested += OnEntryDeleted;
    Detail = detail;
}

[RelayCommand]
private void NewPassword() => StartNewEntry(new Core.Models.PasswordItem { Title = "Nueva entrada" });

[RelayCommand]
private void NewNote() => StartNewEntry(new Core.Models.NoteItem { Title = "Nueva nota" });

private void StartNewEntry(Core.Models.VaultEntry entry)
{
    _session.Data!.Entries.Add(entry);
    Refresh();
    SelectedEntry = Entries.First(e => e.Id == entry.Id);
}

private void OnEntrySaved(Core.Models.VaultEntry updated)
{
    if (_session.Data is null) return;
    var index = _session.Data.Entries.FindIndex(e => e.Id == updated.Id);
    if (index >= 0) _session.Data.Entries[index] = updated;
    Refresh();
}

private void OnEntryDeleted(Guid id)
{
    _session.Data?.Entries.RemoveAll(e => e.Id == id);
    Detail = null;
    Refresh();
}
```

```xml
<!-- src/Sekretujo.App/Views/ShellView.axaml — replace the empty ContentControl -->
<ContentControl Grid.Row="1" Grid.Column="1" Margin="12" Content="{Binding Detail}">
  <ContentControl.DataTemplates>
    <DataTemplate x:DataType="vm:EntryDetailViewModel">
      <views:EntryDetailView xmlns:views="using:Sekretujo.App.Views" />
    </DataTemplate>
  </ContentControl.DataTemplates>
</ContentControl>
```

Add two more buttons to the search bar in `ShellView.axaml`:

```xml
<Button Content="Nueva password" Command="{Binding NewPasswordCommand}" />
<Button Content="Nueva nota" Command="{Binding NewNoteCommand}" />
```

```csharp
// src/Sekretujo.App/ServiceConfiguration.cs — add
services.AddSingleton<ClipboardAutoClearService>();
services.AddTransient<EntryDetailViewModel>();
```

Attach the real clipboard once, from the view's code-behind:

```csharp
// src/Sekretujo.App/Views/EntryDetailView.axaml.cs — replace constructor body
using Sekretujo.App.ViewModels;

public EntryDetailView()
{
    InitializeComponent();
    DataContextChanged += (_, _) =>
    {
        if (DataContext is EntryDetailViewModel vm && TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
            vm.AttachClipboard(clipboard);
    };
}
```

- [ ] **Step 5: Manual verification**

Run: `dotnet run --project src/Sekretujo.App`
Checklist:
1. Create a vault, click "Nueva password", fill in fields, click "Generar" (password
   field fills with 24 random chars), click "Guardar" — entry appears in the list.
2. Select it again, click "Copiar" — paste somewhere to confirm it copied; wait 20s and
   confirm the clipboard is cleared.
3. Add a TOTP secret `JBSWY3DPEHPK3PXP` — a 6-digit code with a countdown appears.
4. Enter an invalid TOTP secret like `not-base32` — "Código TOTP inválido." shows instead
   of a crash.
5. Click "Nueva nota", fill `Body`, save — appears in the list without password fields.
6. Click "Eliminar" on a selected entry — it disappears from the list.

- [ ] **Step 6: Commit**

```bash
git add src/Sekretujo.App
git commit -m "feat: add entry detail view with generator, TOTP, and clipboard copy"
```

---

### Task 12: Settings — auto-lock timeout, clipboard timeout, import/export

**Files:**
- Create: `src/Sekretujo.App/ViewModels/SettingsViewModel.cs`
- Create: `src/Sekretujo.App/Views/SettingsView.axaml`, `SettingsView.axaml.cs`
- Modify: `src/Sekretujo.App/ViewModels/ShellViewModel.cs` (add `OpenSettingsCommand`
  toggling a `SettingsViewModel? SettingsOverlay`)
- Modify: `src/Sekretujo.App/Views/ShellView.axaml` (settings button + overlay)
- Modify: `src/Sekretujo.App/ServiceConfiguration.cs` (register `SettingsViewModel`)

**Interfaces:**
- Consumes: `ImportExportService` (Task 7), `VaultSession.Data` (Task 8).
- Produces: `SettingsViewModel` with `int AutoLockMinutes`, `int ClipboardClearSeconds`,
  `ExportCsvCommand`, `ImportCsvCommand`, `ImportBitwardenCommand`, `ImportKeePassCommand`,
  `string StatusMessage`, `event Action? Closed`.

- [ ] **Step 1: Implement SettingsViewModel**

```csharp
// src/Sekretujo.App/ViewModels/SettingsViewModel.cs
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
```

- [ ] **Step 2: Implement SettingsView**

```xml
<!-- src/Sekretujo.App/Views/SettingsView.axaml -->
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:Sekretujo.App.ViewModels"
             x:Class="Sekretujo.App.Views.SettingsView"
             x:DataType="vm:SettingsViewModel">
  <StackPanel Spacing="10" Width="380">
    <TextBlock Text="Ajustes" FontSize="20" FontWeight="Bold" />
    <StackPanel Orientation="Horizontal" Spacing="8">
      <TextBlock Text="Auto-lock (min):" VerticalAlignment="Center" />
      <NumericUpDown Value="{Binding AutoLockMinutes}" Minimum="1" Maximum="120" />
    </StackPanel>
    <StackPanel Orientation="Horizontal" Spacing="8">
      <TextBlock Text="Limpiar portapapeles (s):" VerticalAlignment="Center" />
      <NumericUpDown Value="{Binding ClipboardClearSeconds}" Minimum="5" Maximum="300" />
    </StackPanel>
    <Button Content="Aplicar" Command="{Binding ApplyTimeoutsCommand}" />
    <Separator Margin="0,8" />
    <TextBox Watermark="Ruta de archivo CSV/JSON" Text="{Binding FilePath}" />
    <StackPanel Orientation="Horizontal" Spacing="8">
      <Button Content="Exportar CSV" Command="{Binding ExportCsvCommand}" />
      <Button Content="Importar CSV" Command="{Binding ImportCsvCommand}" />
    </StackPanel>
    <StackPanel Orientation="Horizontal" Spacing="8">
      <Button Content="Importar Bitwarden" Command="{Binding ImportBitwardenCommand}" />
      <Button Content="Importar KeePass" Command="{Binding ImportKeePassCommand}" />
    </StackPanel>
    <TextBlock Text="{Binding StatusMessage}" />
    <Button Content="Cerrar" Command="{Binding CloseCommand}" />
  </StackPanel>
</UserControl>
```

```csharp
// src/Sekretujo.App/Views/SettingsView.axaml.cs
using Avalonia.Controls;

namespace Sekretujo.App.Views;

public partial class SettingsView : UserControl
{
    public SettingsView() => InitializeComponent();
}
```

- [ ] **Step 3: Wire into ShellViewModel/ShellView as an overlay**

```csharp
// src/Sekretujo.App/ViewModels/ShellViewModel.cs — add
[ObservableProperty]
private SettingsViewModel? _settingsOverlay;

[RelayCommand]
private void OpenSettings()
{
    var settings = _services.GetRequiredService<SettingsViewModel>();
    settings.Closed += () => SettingsOverlay = null;
    settings.SettingsChanged += Refresh;
    SettingsOverlay = settings;
}
```

```xml
<!-- src/Sekretujo.App/Views/ShellView.axaml — add button next to "Bloquear" -->
<Button Content="Ajustes" Command="{Binding OpenSettingsCommand}" />
```

```xml
<!-- src/Sekretujo.App/Views/ShellView.axaml — add as an overlay panel, inside the root Grid -->
<Border Grid.RowSpan="2" Grid.ColumnSpan="2" Background="#CC202020"
        IsVisible="{Binding SettingsOverlay, Converter={x:Static ObjectConverters.IsNotNull}}">
  <ContentControl Content="{Binding SettingsOverlay}" HorizontalAlignment="Center" VerticalAlignment="Center">
    <ContentControl.DataTemplates>
      <DataTemplate x:DataType="vm:SettingsViewModel">
        <views:SettingsView xmlns:views="using:Sekretujo.App.Views" />
      </DataTemplate>
    </ContentControl.DataTemplates>
  </ContentControl>
</Border>
```

Add `xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"` import for
`ObjectConverters` is already in Avalonia core namespace — add
`xmlns:local="using:Avalonia.Controls"` is not required; `ObjectConverters` lives in
`Avalonia.Controls.Converters`, already available via the default `using:Avalonia.Controls` import — no extra xmlns needed beyond what `ShellView.axaml` already declares.

```csharp
// src/Sekretujo.App/ServiceConfiguration.cs — add
services.AddTransient<SettingsViewModel>();
```

- [ ] **Step 4: Manual verification**

Run: `dotnet run --project src/Sekretujo.App`
Checklist:
1. Click "Ajustes" — overlay appears centered over the shell.
2. Set a file path, click "Exportar CSV" with at least one password entry — file is
   written and `StatusMessage` confirms it.
3. Click "Importar CSV" pointing at the exported file — entries are added (duplicated is
   expected/acceptable for this manual check) and the list refreshes.
4. Click "Cerrar" — overlay disappears.

- [ ] **Step 5: Commit**

```bash
git add src/Sekretujo.App
git commit -m "feat: add settings overlay with timeouts and import/export"
```

---

### Task 13: Auto-lock on inactivity

**Files:**
- Create: `src/Sekretujo.App/Services/AutoLockService.cs`
- Modify: `src/Sekretujo.App/Views/MainWindow.axaml.cs` (route input activity into the
  service)
- Modify: `src/Sekretujo.App/ViewModels/MainWindowViewModel.cs` (own the
  `AutoLockService`, lock on timeout)
- Modify: `src/Sekretujo.App/ServiceConfiguration.cs` (register `AutoLockService`)

**Interfaces:**
- Consumes: `ShellViewModel.Locked` behavior (Task 10, reused), `SettingsViewModel.
  AutoLockMinutes` (Task 12) read when settings change.
- Produces: `AutoLockService` with `Start(TimeSpan timeout)`, `Stop()`, `NotifyActivity()`,
  `event Action? TimedOut`.

- [ ] **Step 1: Implement AutoLockService**

```csharp
// src/Sekretujo.App/Services/AutoLockService.cs
namespace Sekretujo.App.Services;

public sealed class AutoLockService : IDisposable
{
    private Timer? _timer;
    private TimeSpan _timeout = TimeSpan.FromMinutes(5);

    public event Action? TimedOut;

    public void Start(TimeSpan timeout)
    {
        _timeout = timeout;
        _timer?.Dispose();
        _timer = new Timer(_ => TimedOut?.Invoke(), null, _timeout, Timeout.InfiniteTimeSpan);
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    public void NotifyActivity()
    {
        if (_timer is null) return;
        _timer.Change(_timeout, Timeout.InfiniteTimeSpan);
    }

    public void Dispose() => _timer?.Dispose();
}
```

- [ ] **Step 2: Wire it into MainWindowViewModel**

```csharp
// src/Sekretujo.App/ViewModels/MainWindowViewModel.cs — add field + constructor param
private readonly AutoLockService _autoLock;

public MainWindowViewModel(IServiceProvider services, AutoLockService autoLock)
{
    _services = services;
    _autoLock = autoLock;
    _autoLock.TimedOut += () => ShowUnlock();
    ShowUnlock();
}

private void ShowShell()
{
    var shell = _services.GetRequiredService<ShellViewModel>();
    shell.Locked += ShowUnlock;
    CurrentView = shell;
    _autoLock.Start(TimeSpan.FromMinutes(5));
}

private void ShowUnlock()
{
    _autoLock.Stop();
    var unlock = _services.GetRequiredService<UnlockViewModel>();
    unlock.Unlocked += ShowShell;
    CurrentView = unlock;
}

public void NotifyActivity() => _autoLock.NotifyActivity();
```

- [ ] **Step 3: Forward input activity from the window**

```csharp
// src/Sekretujo.App/Views/MainWindow.axaml.cs
using Avalonia.Controls;
using Avalonia.Input;
using Sekretujo.App.ViewModels;

namespace Sekretujo.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AddHandler(PointerMovedEvent, OnActivity, handledEventsToo: true);
        AddHandler(KeyDownEvent, OnActivity, handledEventsToo: true);
    }

    private void OnActivity(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.NotifyActivity();
    }
}
```

```csharp
// src/Sekretujo.App/ServiceConfiguration.cs — add
services.AddSingleton<AutoLockService>();
```

- [ ] **Step 4: Manual verification**

Checklist (edit `TimeSpan.FromMinutes(5)` to `TimeSpan.FromSeconds(10)` temporarily for a
fast check, then revert):
1. Unlock a vault, stop touching mouse/keyboard for the configured timeout — the app
   returns to the Unlock view automatically.
2. Unlock again, move the mouse periodically before the timeout — the app stays on the
   Shell view.

- [ ] **Step 5: Commit**

```bash
git add src/Sekretujo.App
git commit -m "feat: add idle auto-lock"
```

---

### Task 14: Self-contained publish for Linux and Windows

**Files:**
- Create: `publish.sh`

**Interfaces:**
- Produces: self-contained, single-folder builds under `dist/linux-x64` and
  `dist/win-x64`.

- [ ] **Step 1: Write the publish script**

```bash
#!/usr/bin/env bash
# publish.sh
set -euo pipefail

dotnet publish src/Sekretujo.App/Sekretujo.App.csproj \
  -c Release -r linux-x64 --self-contained true \
  -p:PublishSingleFile=true -o dist/linux-x64

dotnet publish src/Sekretujo.App/Sekretujo.App.csproj \
  -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -o dist/win-x64
```

- [ ] **Step 2: Make it executable and run it**

```bash
chmod +x publish.sh
./publish.sh
```

Expected: `dist/linux-x64/Sekretujo.App` and `dist/win-x64/Sekretujo.App.exe` both exist.

- [ ] **Step 3: Verify the Linux build runs standalone**

```bash
./dist/linux-x64/Sekretujo.App &
sleep 2
pkill -f dist/linux-x64/Sekretujo.App || true
```

Expected: the process starts without missing-runtime errors (confirm visually that the
window opened, then close it manually if `pkill` doesn't catch it in time).

- [ ] **Step 4: Add dist/ to .gitignore and commit the script**

```bash
echo "dist/" >> .gitignore
echo "bin/" >> .gitignore
echo "obj/" >> .gitignore
git add publish.sh .gitignore
git commit -m "chore: add self-contained publish script for linux-x64/win-x64"
```

---

## Self-Review Notes

- **Spec coverage:** §3 stack → Tasks 1,8; §4 architecture/layering → Tasks 1–8 (Core) vs
  9–13 (App); §5 storage format → Tasks 3–4; §6 data model → Task 2; §7 features
  (generator, search, TOTP, clipboard, import/export, auto-lock) → Tasks 5,6,7,10,11,13;
  §8 error handling (wrong password, corrupt file, atomic write) → Task 4; §9 testing →
  Tasks 1–7 automated, 8–13 manual (per spec's no-UI-tests constraint); §10 distribution →
  Task 14.
- **Placeholder scan:** no TBD/TODO; every step has runnable code or an explicit shell
  command.
- **Type consistency:** `VaultRepository(CryptoService, VaultSerializer)` constructor
  matches across Tasks 3, 4, 8; `TotpService.GetCode` signature matches across Tasks 6,
  11; `VaultSession` members match across Tasks 8–13.
- **Review Focus:** all five items have a concrete test (Tasks 4, 5, 6, 7) or manual
  checklist step (Task 11) in the owning task.
