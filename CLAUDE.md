# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Sekretujo: a cross-platform (Linux/Windows) desktop password manager and notes app. Avalonia + FluentAvalonia UI, local AES-256-GCM/Argon2id-encrypted vault, no server/sync. See `docs/superpowers/specs/2026-10-15-sekretujo-design.md` for the full design, `docs/superpowers/plans/2026-10-15-sekretujo-core-and-app.md` for the implementation plan/task breakdown, and `docs/superpowers/specs/2026-10-16-sekretujo-ui-redesign.md` for the dark-violet visual redesign.

## Commands

```bash
# Build everything
dotnet build Sekretujo.slnx

# Run the app
dotnet run --project src/Sekretujo.App

# Run all Core tests
dotnet test tests/Sekretujo.Core.Tests

# Run a single test
dotnet test tests/Sekretujo.Core.Tests --filter "FullyQualifiedName~CryptoServiceTests"
dotnet test tests/Sekretujo.Core.Tests --filter "FullyQualifiedName~Encrypt_Then_Decrypt_WithCorrectPassword_RoundTrips"

# Publish self-contained builds (linux-x64 + win-x64) to dist/
./publish.sh
```

There is no lint command configured. `Sekretujo.App` has `ImplicitUsings` disabled (unlike `Sekretujo.Core`, which has it enabled) — new files in `src/Sekretujo.App` need explicit `using System;` etc. for types like `Action`, `Guid`, `TimeSpan`.

## Architecture

**`Sekretujo.Core`** (`src/Sekretujo.Core`) has zero Avalonia/UI references and is fully unit-tested (`tests/Sekretujo.Core.Tests`). Everything the app does to vault data goes through it:

- `Crypto/CryptoService` — Argon2id key derivation + AES-256-GCM encrypt/decrypt. Wrong-password and tampered-ciphertext both surface as `VaultUnlockException` with the same message, to avoid leaking which failure occurred.
- `Storage/VaultRepository` — owns the on-disk `.vault` file layout (`[16B salt][12B nonce][ciphertext][16B tag]`) and does atomic saves (write to `<path>.tmp`, then `File.Move` with overwrite). `VaultSerializer` handles the JSON layer underneath it, using `[JsonPolymorphic]`/`[JsonDerivedType]` to (de)serialize `PasswordItem`/`NoteItem` through the abstract `VaultEntry` base. Both corrupt-file and wrong-password paths resolve to a narrow set of domain exceptions (`VaultCorruptException`, `VaultUnlockException`) — never let a raw parsing/crypto exception escape `Deserialize`/`Decrypt`.
- `Services/` — `PasswordGeneratorService` (CSPRNG via `RandomNumberGenerator`, no modulo bias), `TotpService` (wraps Otp.NET; the whole decode+compute path is caught and rethrown as `InvalidTotpSecretException`, since a secret that decodes to too few bytes throws from inside `Totp`'s constructor, not from the Base32 decode step), `ImportExportService` (hand-rolled CSV parser that tracks quote state across the *entire* input rather than line-by-line, so a quoted field containing a literal newline round-trips correctly; CSV export neutralizes leading `=+-@`/tab characters against spreadsheet formula injection on every column *except* Password, since mutating a password on export/import would silently corrupt it), `PasswordStrengthEstimator` (pure heuristic, no dependencies).

**`Sekretujo.App`** (`src/Sekretujo.App`) is Avalonia + FluentAvalonia + CommunityToolkit.Mvvm, DI-wired in `ServiceConfiguration.BuildServiceProvider()` (no ASP.NET host — a plain `ServiceCollection`). `MainWindowViewModel` is the root: it swaps `CurrentView` between `UnlockViewModel` and `ShellViewModel` via `App.axaml`'s `DataTemplates` (view-model-first navigation, no view code deciding what to show).

- `VaultSession` (singleton) holds the currently-open `VaultData` + file path + master password in memory; `AutoLockService` (singleton, a `System.Threading.Timer`) fires `TimedOut` on inactivity. Because the timer callback runs on a background thread, `MainWindowViewModel` marshals it back via `Avalonia.Threading.Dispatcher.UIThread.Post` before touching any UI-bound property, and always closes `VaultSession` on lock/timeout so decrypted data doesn't linger in memory after the lock screen shows.
- Every mutation to the open vault (new/edited/deleted entries, CSV/Bitwarden/KeePass import) must call `VaultRepository.Save` immediately after mutating `VaultSession.Data` — saving is still explicit (user clicks Guardar), never on a timer or on navigation; `ShellViewModel.Persist()` and `SettingsViewModel.Import` are the two places that do this. `EntryDetailViewModel.IsDirty` is a UI-only affordance (unsaved-changes dot + enabling "Descartar") set by field-changed handlers and cleared on `Save`/`LoadFrom` — it does not defer or batch the actual save. `ShellViewModel.Refresh()` re-selects the previously-selected entry by `Id` after rebuilding `Entries`, so that saving or searching doesn't visually collapse the detail pane.
- `EntryDetailViewModel`, `SettingsViewModel`, and `UnlockViewModel` are all `AddTransient` — a fresh instance is created every time the user navigates to them (new entry selected, settings opened). `EntryDetailViewModel` owns a `DispatcherTimer` for the live TOTP countdown and implements `IDisposable` to stop it; callers (`ShellViewModel`) must dispose the outgoing `Detail` before assigning a new one, or the timer leaks and keeps ticking.
- `AppSettings` (auto-lock minutes, clipboard-clear seconds, last vault path, remembered password-generator options) is loaded/saved as JSON by `AppSettingsStore` to `%AppData%/Sekretujo/settings.json` (via `Environment.SpecialFolder.ApplicationData`, which resolves to `~/.config` on Linux). Settings persistence is deliberately best-effort — `AppSettingsStore` swallows I/O failures rather than letting a settings-file problem block using the app.
- Views that need something Avalonia-specific that the ViewModel shouldn't own directly (clipboard access, file pickers, launching URLs) get it from code-behind via `TopLevel.GetTopLevel(this)` and hand it to the ViewModel through a plain method (e.g. `EntryDetailViewModel.AttachClipboard`/`AttachLauncher`), rather than injecting Avalonia UI types through DI.
- `src/Sekretujo.App/Styles/Theme.axaml` is the dark-violet visual theme (colors, `Border.card`, `Button.accent`/`.ghost`/`.danger`, `ToggleButton.chip`/`.tab`/`.star`), registered via `StyleInclude` in `App.axaml` after `FluentAvaloniaTheme`. All new UI should reuse these style classes rather than inlining colors/brushes.

## Known project-wide constraints

- No automated UI/ViewModel tests by design (see spec §9) — Avalonia wiring is verified by `dotnet build` + a manual run, not by a test suite. Keep all business logic that *can* be tested in `Sekretujo.Core`.
- `dotnet new sln` on the .NET 10 SDK in this environment produces `Sekretujo.slnx`, not a `.sln` file — always reference it by its actual name.
- The NuGet package for FluentAvalonia is published as `FluentAvaloniaUI`, not `FluentAvalonia`.
