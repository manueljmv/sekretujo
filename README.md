# Sekretujo

Gestor de contraseñas y notas de escritorio, multiplataforma (Linux/Windows), sin servidor ni sincronización. Todos los datos se guardan localmente en un vault cifrado.

## Características

- Entradas de tipo contraseña y nota, con favoritos, etiquetas y búsqueda/filtrado.
- Cifrado AES-256-GCM del vault, con derivación de clave Argon2id a partir de la contraseña maestra.
- Generador de contraseñas (CSPRNG) con opciones configurables y estimador de fortaleza.
- Códigos TOTP (2FA) para las entradas que lo requieran.
- Importación/exportación CSV, y compatibilidad de importación desde Bitwarden y KeePass.
- Autobloqueo por inactividad y bloqueo manual.

## Stack técnico

- .NET 10, Avalonia + FluentAvalonia para la UI, CommunityToolkit.Mvvm.
- `Sekretujo.Core`: lógica de dominio (cripto, almacenamiento, servicios), sin dependencias de UI, con tests unitarios.
- `Sekretujo.App`: aplicación de escritorio (DI vía `ServiceCollection`, sin host de ASP.NET).

## Comandos

```bash
# Compilar todo
dotnet build Sekretujo.slnx

# Ejecutar la app
dotnet run --project src/Sekretujo.App

# Ejecutar los tests de Core
dotnet test tests/Sekretujo.Core.Tests

# Generar builds self-contained (linux-x64 + win-x64) en dist/
./publish.sh
```

## Documentación

- [`docs/superpowers/specs/2026-10-15-sekretujo-design.md`](docs/superpowers/specs/2026-10-15-sekretujo-design.md) — diseño completo.
- [`docs/superpowers/plans/2026-10-15-sekretujo-core-and-app.md`](docs/superpowers/plans/2026-10-15-sekretujo-core-and-app.md) — plan de implementación.
- [`docs/superpowers/specs/2026-10-16-sekretujo-ui-redesign.md`](docs/superpowers/specs/2026-10-16-sekretujo-ui-redesign.md) — rediseño visual dark-violet.
- [`CLAUDE.md`](CLAUDE.md) — guía de arquitectura para desarrollo con Claude Code.
