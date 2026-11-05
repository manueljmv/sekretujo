# Tipo de entrada «tarjeta» — plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Objetivo:** añadir `CardItem` como tercer tipo de entrada, con su pantalla de detalle (diseño B), la integración en la shell y la lógica de validación en Core.

**Arquitectura:** el modelo y la lógica pura (`CardInfo`) viven en `Sekretujo.Core` y tienen tests. `EntryDetailViewModel` sustituye el booleano `IsNote` por un enum `EntryKind` y añade los campos de tarjeta. La vista añade un bloque visible solo cuando `IsCard`. La shell añade el botón, el filtro y el subtítulo de la lista.

**Stack:** .NET 10, Avalonia, FluentAvalonia, CommunityToolkit.Mvvm, xUnit.

**Spec:** `docs/superpowers/specs/2026-11-05-credit-card-entry-design.md`

## Restricciones globales

- Discriminador JSON: `"card"`.
- `Sekretujo.App` tiene `ImplicitUsings` desactivado: hay que poner `using System;` y los demás de forma explícita.
- Todos los colores nuevos van en `Styles/Theme.axaml`, en los dos diccionarios `Dark` y `Light`. No se ponen colores en línea.
- Los textos de la interfaz van en español, con los literales exactos de este plan.
- Los avisos no bloquean el guardado, y con el campo vacío no se muestra ningún aviso.
- Número, CVV y PIN se copian con `ClipboardAutoClearService.CopyAndClear`. Titular, caducidad y teléfono se copian con `IClipboard.SetTextAsync`.
- El dibujo de la tarjeta siempre muestra el número enmascarado.

## Puntos de revisión

1. **Número pegado con espacios o guiones:** se copia solo con dígitos y se valida sobre esos dígitos (tests en la tarea 2).
2. **Tarjeta que caduca en el mes actual:** no cuenta como caducada (test en la tarea 2).
3. **Formatos de caducidad límite:** `8/28`, `08/2028` y `08 / 28` son válidos; `13/28`, `00/28` y `0828` no lo son (tests en la tarea 2).
4. **Número con menos de 4 dígitos:** `LastFour` devuelve vacío, la lista muestra «sin número» y el dibujo sale enmascarado por completo (tests en la tarea 2).
5. **Descartar después de revelar o editar campos de tarjeta:** se restauran los valores y los tres campos vuelven a ocultarse (paso manual en la tarea 5).

---

### Tarea 1: modelo `CardItem`

**Ficheros:**
- Crear: `src/Sekretujo.Core/Models/CardItem.cs`
- Modificar: `src/Sekretujo.Core/Models/VaultEntry.cs`
- Test: `tests/Sekretujo.Core.Tests/VaultDataSerializationTests.cs`

**Interfaces:**
- Produce: `public sealed class CardItem : VaultEntry` con las propiedades `string` `Holder`, `Number`, `Expiry`, `Cvv`, `Pin`, `BankPhone` y `Notes`, todas inicializadas a `string.Empty`.

- [ ] **Paso 1: escribir el test que falla**

`Serialize_Then_Deserialize_RoundTripsCardItem`: crear un `VaultData` con `new CardItem { Title = "Visa", Holder = "M J M", Number = "4111 1111 1111 1111", Expiry = "08/28", Cvv = "123", Pin = "9876", BankPhone = "900102801", Notes = "límite" }`. Serializar y deserializar, y comprobar con `Assert.IsType<CardItem>` que los 8 valores son iguales.

- [ ] **Paso 2: ejecutar el test y comprobar que falla**

Run: `dotnet test tests/Sekretujo.Core.Tests --filter "FullyQualifiedName~RoundTripsCardItem"`
Esperado: fallo de compilación, porque `CardItem` no existe.

- [ ] **Paso 3: implementarlo**

Crear `CardItem` con el mismo estilo que `NoteItem` y añadir `[JsonDerivedType(typeof(CardItem), "card")]` en `VaultEntry`.

- [ ] **Paso 4: ejecutar todos los tests de Core**

Run: `dotnet test tests/Sekretujo.Core.Tests`
Esperado: todo en verde.

- [ ] **Paso 5: commit** `feat: add CardItem vault entry type`

### Tarea 2: helper `CardInfo`

**Ficheros:**
- Crear: `src/Sekretujo.Core/Services/CardInfo.cs`
- Test: `tests/Sekretujo.Core.Tests/CardInfoTests.cs`

**Interfaces:**
- Produce:
  - `public enum ExpiryStatus { Valid, Expired, InvalidFormat }`
  - `public static class CardInfo` con:
    - `string Digits(string number)`: solo los caracteres `0-9`.
    - `string LastFour(string number)`: los últimos 4 de `Digits`, o `""` si hay menos de 4.
    - `string MaskedNumber(string number)`: devuelve `"•••• •••• •••• " + LastFour`, o `"•••• •••• •••• ••••"` si `LastFour` está vacío.
    - `bool IsValidNumber(string number)`: `Digits` tiene entre 12 y 19 caracteres y pasa Luhn.
    - `ExpiryStatus GetExpiryStatus(string expiry, DateOnly today)`.

- [ ] **Paso 1: escribir los tests que fallan** (xUnit, `[Theory]` donde convenga)

  - `Digits("4111-1111 1111 1111") == "4111111111111111"` y `Digits("ab12") == "12"`.
  - `LastFour("4111 1111 1111 1234") == "1234"` y `LastFour("12") == ""`.
  - `MaskedNumber("4111111111111234") == "•••• •••• •••• 1234"` y `MaskedNumber("") == "•••• •••• •••• ••••"`.
  - `IsValidNumber`:
    - `true` para `"4111 1111 1111 1111"` y `"4111-1111-1111-1111"`;
    - `false` para `"4111 1111 1111 1112"` (falla Luhn), `"0000"` (demasiado corto) y `"00000000000000000000"` (20 dígitos).
  - `GetExpiryStatus` con `today = new DateOnly(2026, 11, 5)`:
    - `Valid` para `"11/26"` (mes actual), `"8/28"`, `"08/2028"` y `"08 / 28"`;
    - `Expired` para `"10/26"` y `"10/2026"`;
    - `InvalidFormat` para `"13/28"`, `"00/28"`, `"0828"`, `"08/2"` y `""`.

- [ ] **Paso 2: ejecutar los tests y comprobar que fallan**

Run: `dotnet test tests/Sekretujo.Core.Tests --filter "FullyQualifiedName~CardInfoTests"`
Esperado: fallo de compilación.

- [ ] **Paso 3: implementar `CardInfo`**

Caducidad:
- Quitar los espacios y partir por `/`. Debe salir mes de 1 o 2 cifras (1–12) y año de 2 o 4 cifras; con 2 cifras, el año es `2000 + AA`.
- `Expired` si `(año, mes)` es anterior a `(today.Year, today.Month)`.

- [ ] **Paso 4: ejecutar todos los tests de Core**

Run: `dotnet test tests/Sekretujo.Core.Tests`
Esperado: todo en verde.

- [ ] **Paso 5: commit** `feat: add CardInfo validation helpers`

### Tarea 3: soporte de tarjeta en `EntryDetailViewModel`

**Ficheros:**
- Modificar: `src/Sekretujo.App/ViewModels/EntryDetailViewModel.cs`
- Modificar: `src/Sekretujo.App/Views/EntryDetailView.axaml`, solo la línea 55: `!IsNote` → `IsPassword` (la 249 sigue usando `IsNote`)

**Interfaces:**
- Consume: `CardItem` y `CardInfo` de las tareas 1 y 2.
- Produce, para la tarea 4:
  - `public enum EntryKind { Password, Note, Card }`, en el mismo fichero.
  - Propiedad observable `Kind`, y `bool IsPassword`, `IsNote`, `IsCard` derivadas de `Kind`, que notifican al cambiar `Kind`. Sustituyen al campo `_isNote`.
  - Propiedades observables `string`: `CardHolder`, `CardNumber`, `CardExpiry`, `CardCvv`, `CardPin`, `BankPhone`. Las notas de la tarjeta usan la propiedad `Notes` que ya existe.
  - Propiedades observables `bool`: `IsCardNumberVisible`, `IsCardCvvVisible`, `IsCardPinVisible`.
  - Propiedades derivadas solo de lectura, que notifican cuando cambian sus fuentes:
    - `CardDisplayTitle` = `Title.ToUpperInvariant()`;
    - `CardMaskedNumber` = `CardInfo.MaskedNumber(CardNumber)`;
    - `CardNumberWarning` y `CardExpiryWarning`, de tipo `string`.
  - Comandos:
    - `CopyCardNumberCommand`, `CopyCardCvvCommand` y `CopyCardPinCommand` (`CopyAndClear`; el número se copia como `CardInfo.Digits(CardNumber)`);
    - `CopyCardHolderCommand`, `CopyCardExpiryCommand` y `CopyBankPhoneCommand` (`SetTextAsync`).

    Todos llaman a `ShowCopied(nameof(<propiedad>))`, por ejemplo `nameof(CardNumber)`.

- [ ] **Paso 1: implementarlo**

- `TypeLabel`: `Password` → «Contraseña», `Note` → «Nota», `Card` → «Tarjeta».
- `LoadFrom`:
  - añadir la rama `CardItem`, que copia los 7 campos y fija `Kind = EntryKind.Card`;
  - poner los tres `IsCard*Visible` a `false` para cualquier tipo de entrada.
- `Save`: un `switch` sobre `Kind`. La rama `Card` construye un `CardItem` con los mismos campos comunes que las otras ramas (`Id`, `Title`, `IsFavorite`, `Tags`, `CreatedAt`, `ModifiedAt`).
- Los `partial void On<Campo>Changed` de los 6 campos nuevos llaman a `MarkDirty()`.
- Avisos, calculados con `DateOnly.FromDateTime(DateTime.Today)`:
  - `CardNumberWarning`: vacío si `CardNumber` está en blanco o `IsValidNumber`; si no, `"Número de tarjeta no válido."`.
  - `CardExpiryWarning`: vacío si `CardExpiry` está en blanco o el estado es `Valid`. Con `InvalidFormat`, `"Formato de caducidad no válido (MM/AA)."`. Con `Expired`, `"Tarjeta caducada."`.

- [ ] **Paso 2: compilar**

Run: `dotnet build Sekretujo.slnx`
Esperado: sin errores.

- [ ] **Paso 3: commit** `feat: card fields and commands in entry detail view model`

### Tarea 4: vista de detalle de la tarjeta y estilo del dibujo

**Ficheros:**
- Modificar: `src/Sekretujo.App/Styles/Theme.axaml`
- Modificar: `src/Sekretujo.App/Views/EntryDetailView.axaml`

**Interfaces:**
- Consume: las propiedades y los comandos de la tarea 3.

- [ ] **Paso 1: recursos y estilo en `Theme.axaml`**

- En cada diccionario de tema, un `LinearGradientBrush x:Key="BrushCardVisual"` en diagonal (de arriba-izquierda a abajo-derecha) y un `SolidColorBrush x:Key="BrushCardVisualText"`:
  - `Dark`: de #2E2A4D a #8B7CF6, texto #F1F1F4.
  - `Light`: de #6E56CF a #5A45B8, texto #FFFFFF.
- Estilos:
  - `Border.cardVisual`: `Background` = `BrushCardVisual`, `CornerRadius` 14, `Padding` 20, `Height` 170.
  - `Border.cardVisual TextBlock`: `Foreground` = `BrushCardVisualText`.

- [ ] **Paso 2: bloque de tarjeta en `EntryDetailView.axaml`**

Va en un `Grid ColumnDefinitions="*,340" IsVisible="{Binding IsCard}"` dentro del `ScrollViewer` de la fila 2, junto al grid de contraseña.

- **Columna 0:**
  - `Border.cardVisual` con cuatro textos:
    - `CardDisplayTitle`, pequeño, arriba;
    - `CardMaskedNumber`, en monospace y tamaño 18;
    - abajo, en la misma fila, `CardHolder` a la izquierda y `CardExpiry` a la derecha.
  - Debajo, un `Border.card` con el título `EDITAR`:
    - Número: campo con `PasswordChar="•"` y `RevealPassword="{Binding IsCardNumberVisible}"`, más los botones 👁 y copiar, iguales que los de Contraseña.
    - Una fila con Titular (2*) y Caducidad (1*), ambos con botón de copiar.
    - Una fila con CVV y PIN (1* cada uno), ambos con `PasswordChar`, 👁 y copiar.
    - Bajo Número, un `TextBlock` en `BrushWarning`, tamaño 12, con `CardNumberWarning`; solo visible si el aviso no está vacío. Bajo Caducidad, otro igual con `CardExpiryWarning`.
- **Columna 1:**
  - `Border.card` con el título `BANCO` y el campo «Teléfono» con botón de copiar.
  - `Border.card` con el título `NOTAS`, enlazado a `Notes`, con `AcceptsReturn` y `Height` 90.
- Los botones de copiar y de 👁 siguen el mismo patrón que los de Contraseña: icono `copy.png` o la marca `Path.copied` según `CopiedField`, e iconos `visible.png`/`invisible.png`.

- [ ] **Paso 3: compilar**

Run: `dotnet build Sekretujo.slnx`
Esperado: sin errores.

- [ ] **Paso 4: commit** `feat: card detail view with card visual`

### Tarea 5: integración en la shell y la lista, y verificación manual

**Ficheros:**
- Modificar: `src/Sekretujo.App/ViewModels/ShellViewModel.cs`
- Modificar: `src/Sekretujo.App/Views/ShellView.axaml`
- Modificar: `src/Sekretujo.App/ViewModels/EntryListItemViewModel.cs`

**Interfaces:**
- Consume: `CardItem` y `CardInfo.LastFour`.
- Produce: `EntryTypeFilter.Cards` y `NewCardCommand`.

- [ ] **Paso 1: implementarlo**

- `EntryTypeFilter`: añadir `Cards`.
- `NewCard()`: llama a `StartNewEntry(new Core.Models.CardItem { Title = "Nueva tarjeta" })`.
- `StartNewEntry`: añadir la condición `|| TypeFilter == EntryTypeFilter.Cards && entry is not Core.Models.CardItem`.
- `Refresh`: añadir el caso `EntryTypeFilter.Cards => query.Where(e => e is Core.Models.CardItem)`.
- `ShellView.axaml`:
  - Un botón «Tarjeta» con el icono `plus.png`, después del botón «Nota» y con su mismo estilo, enlazado a `NewCardCommand`.
  - Una pestaña «Tarjetas» después de «Notas», con el mismo patrón que las otras y `EntryTypeFilter.Cards`.
- `EntryListItemViewModel`:
  - `TypeLabel` e `Icon` pasan a ser un `switch` por tipo: tarjeta → «Tarjeta» y `"\U0001F4B3"`.
  - `Subtitle` de la tarjeta: `"•••• " + LastFour`, o `"sin número"` si `LastFour` está vacío.

- [ ] **Paso 2: compilar y ejecutar los tests**

Run: `dotnet build Sekretujo.slnx && dotnet test tests/Sekretujo.Core.Tests`
Esperado: sin errores y todo en verde.

- [ ] **Paso 3: verificación manual** (`dotnet run --project src/Sekretujo.App`)

1. Crear una tarjeta con todos los campos y guardar. Bloquear, desbloquear y comprobar que los datos siguen ahí.
2. El dibujo se actualiza mientras se escribe y sigue enmascarado aunque se revele el campo Número.
3. Copiar el número `4111-1111 1111 1111` y pegarlo: llega `4111111111111111`. El portapapeles se vacía pasados los segundos configurados en Ajustes. Hacer lo mismo con CVV y PIN.
4. Con el número `4111 1111 1111 1112` sale el aviso de número; con `10/26`, «Tarjeta caducada.»; con `13/28`, el aviso de formato. Guardar funciona en los tres casos.
5. Revelar CVV, editar el PIN y pulsar Descartar: se restauran los valores y los tres campos vuelven a ocultarse.
6. Con el filtro «Notas» activo, pulsar «+ Tarjeta»: el filtro pasa a «Todas» y se abre el formulario. La pestaña «Tarjetas» filtra bien.
7. En la lista se ven el subtítulo «•••• 1111» y el icono 💳. Probar también con el tema claro.
8. Una bóveda creada antes de este cambio sigue abriéndose.

- [ ] **Paso 4: commit** `feat: card entries in shell toolbar, filter and list`
