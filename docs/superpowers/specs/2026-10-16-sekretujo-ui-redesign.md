# Sekretujo — UI Redesign (dark violet)

Source: mockups in `maqueta/` (`1 · Desbloqueo.png`, `2 · Contraseña con TOTP.png`, `3 · Nota.png`).
Supersedes the visual look described in `2026-10-15-sekretujo-design.md` §4 — architecture and
data flow from that spec are unchanged.

## What changed

- New `src/Sekretujo.App/Styles/Theme.axaml`: dark violet palette (`BrushBackground`,
  `BrushSurface`, `BrushAccent`, etc.) and reusable style classes — `Border.card`,
  `Button.accent`/`.ghost`/`.danger`, `ToggleButton.chip`/`.tab`/`.star`. Registered via
  `StyleInclude` in `App.axaml` after `FluentAvaloniaTheme`.
- `UnlockView`: card layout, lock badge, master-password reveal toggle, strength bar, footer
  crypto tagline.
- `ShellView`: search box, Todas/Contraseñas/Notas segmented filter (`ShellViewModel.TypeFilter`
  + `SetTypeFilterCommand`, bound via `ObjectConverters.Equal` since `IsChecked` needs one-way
  binding), favorites toggle, live "Autobloqueo en M:SS" pill (`AutoLockService.Remaining`),
  restyled entry list (type icon, subtitle, favorite star).
- `EntryDetailView`: two-column layout for passwords (Credenciales+Generador /
  TOTP+Sitio web+Notas), single Contenido card for notes, password-strength bar, copy/reveal/
  open-url icon buttons, unsaved-changes indicator (`EntryDetailViewModel.IsDirty` +
  `DiscardCommand` — UI affordance only, save is still explicit via Guardar; see CLAUDE.md).

## Verification

`dotnet build` (0 warnings/errors), `dotnet test tests/Sekretujo.Core.Tests` (31/31). Visually
verified against the three mockups by running the app under an isolated Xvfb display and
comparing screenshots (no automated UI tests, per design spec §9).
