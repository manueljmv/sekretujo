# Sekretujo — Design Spec

## 1. Purpose

Gestor de contraseñas y notas, de escritorio, multiplataforma (Linux + Windows), con UI
moderna estilo Fluent/Windows 11. Vault cifrado 100% local — sin servidor, sin cuentas,
sin sincronización remota.

**Audiencia:** uso personal (Manuel). Volumen esperado: cientos a pocos miles de entradas.

**Éxito:** abrir la app, desbloquear con master password, buscar/crear/editar entradas de
password o nota, generar contraseñas seguras, ver códigos TOTP, exportar/importar datos,
en Linux y Windows, con builds self-contained.

## 2. Non-goals

- Sin sincronización multi-dispositivo ni servidor propio.
- Sin soporte oficial de macOS/mobile (puede funcionar por ser Avalonia, pero no se testea).
- Sin compartición de vaults entre usuarios.
- Sin tests de UI automatizados (solo Core).

## 3. Stack

| Capa | Elección | Motivo |
|---|---|---|
| UI framework | Avalonia UI | Cross-platform real (Linux/Windows), nativo, sin WebView |
| Theming | FluentAvalonia | Look Fluent/Windows 11 (Mica, esquinas redondeadas) listo para usar |
| MVVM | CommunityToolkit.Mvvm | Ligero, source-generators, sin curva de ReactiveUI |
| Runtime | .NET 10 (LTS) | Soporte largo plazo, última LTS |
| Crypto KDF | Argon2id (`Konscious.Security.Cryptography`) | Resistente a GPU/ASIC, estándar actual |
| Crypto cifrado | AES-256-GCM (`System.Security.Cryptography`) | AEAD, nativo en .NET, sin libs externas |
| TOTP | `Otp.NET` | Implementación RFC 6238 madura |
| Serialización | `System.Text.Json` | Nativo, sin dependencia extra |
| Tests | xUnit | Estándar .NET |

## 4. Arquitectura

```
Sekretujo.Core/
  Models/        VaultEntry, PasswordItem, NoteItem, Tag
  Crypto/        CryptoService (Argon2id + AES-256-GCM), KeyDerivation
  Storage/       VaultRepository (load/save del archivo .vault), VaultSerializer
  Services/      TotpService, PasswordGeneratorService, ImportExportService, ClipboardService (interfaz)
  Sekretujo.Core.Tests/

Sekretujo.App/
  Views/         MainWindow, EntryListView, EntryDetailView, UnlockView, SettingsView
  ViewModels/    correspondientes a cada View
  Services/      ClipboardService (impl. Avalonia), AutoLockService
  App.axaml      tema FluentAvalonia
```

Separación: `Core` no conoce Avalonia ni UI — es testeable en aislamiento. `App` consume
`Core` vía los servicios/repositorios, sin lógica de negocio en ViewModels más allá de
orquestación.

## 5. Formato de almacenamiento

Un único archivo `.vault` por base de datos de usuario:

```
[16 bytes salt] [12 bytes nonce] [ciphertext AES-256-GCM] [16 bytes auth tag]
```

- El `ciphertext` descifra a un JSON con la estructura completa del vault (entries, tags,
  metadata, versión de esquema).
- Clave de cifrado = Argon2id(master_password, salt, params fijos razonables: ~64MB
  memoria, 3 iteraciones — ajustable si el hardware del usuario es limitado).
- Vault completo se descifra en memoria al desbloquear y se vuelve a cifrar/escribir al
  guardar cambios (autosave tras cada edición, o guardado explícito — a decidir en el plan).
- Ventaja sobre SQLite/SQLCipher: cero dependencias nativas por plataforma, build
  self-contained trivial en Linux y Windows.
- Límite aceptado: no hay queries incrementales — irrelevante para el volumen esperado.

## 6. Modelo de datos (resumen)

- `VaultEntry` (base): Id, Title, Tags[], IsFavorite, CreatedAt, ModifiedAt
- `PasswordItem : VaultEntry`: Username, Password, Url, TotpSecret?, Notes
- `NoteItem : VaultEntry`: Body (texto libre)
- `Tag`: Id, Name

## 7. Funcionalidades

- **CRUD** de entradas password/nota.
- **Generador de contraseñas**: longitud y charset configurables (mayúsculas, minúsculas,
  dígitos, símbolos), basado en `RandomNumberGenerator` (CSPRNG).
- **Búsqueda/filtro** instantáneo por título, usuario, tag, favoritos.
- **TOTP**: si `PasswordItem.TotpSecret` está presente, se muestra código de 6 dígitos con
  countdown de 30s (`Otp.NET`).
- **Clipboard**: copiar password/TOTP con auto-limpieza tras N segundos configurables.
- **Import/export**: CSV genérico (mapeo configurable de columnas) + parsers dedicados para
  exports de Bitwarden (JSON) y KeePass (CSV).
- **Auto-lock**: el vault se re-cierra (se limpia de memoria) tras N minutos de inactividad,
  configurable, con opción de desactivar.

## 8. Error handling

- Master password incorrecta → el AEAD tag check falla al descifrar → mensaje genérico
  "contraseña incorrecta o archivo corrupto" (no distinguir las dos causas, evita leak de
  información).
- Archivo `.vault` corrupto/truncado → error claro antes de intentar escribir, nunca
  sobrescribir un archivo que no se pudo leer correctamente.
- Falta de espacio en disco al guardar → guardar primero a archivo temporal y renombrar
  (operación atómica), para no corromper el vault existente en un fallo a mitad de escritura.

## 9. Testing

- `Sekretujo.Core.Tests` (xUnit): CryptoService (round-trip cifrado/descifrado, detección de
  tamper vía AEAD tag), VaultSerializer, PasswordGeneratorService (entropía/charset),
  ImportExportService (parsers CSV/Bitwarden/KeePass con fixtures de muestra).
- Sin tests de UI automatizados (YAGNI para un proyecto personal).

## 10. Distribución

- `dotnet publish -r linux-x64 --self-contained` y `-r win-x64 --self-contained` (TFM `net10.0`).
- Empaquetado concreto (AppImage/.deb para Linux, instalador/portable para Windows) se
  define en el plan de implementación, no bloquea el diseño.

## 11. Fuera de alcance de este spec (decisiones del plan de implementación)

- Política exacta de autosave vs guardado explícito.
- Parámetros finales de Argon2id (balance seguridad/rendimiento en hardware real).
- Formato exacto de empaquetado por SO.
