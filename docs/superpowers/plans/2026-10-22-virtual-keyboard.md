# Teclado virtual en pantalla Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an on-screen virtual keyboard, usable in any focused text field anywhere in the app (including the unlock screen), to mitigate keystroke-logging while entering sensitive data.

**Architecture:** A pure-data `VirtualKeyboardLayout` (key pairs + Fisher-Yates shuffle) lives in `Sekretujo.Core` and is unit-tested. A self-contained `VirtualKeyboardPanel` `UserControl` in `Sekretujo.App` renders that layout as buttons and writes directly into whichever `TextBox` last received focus in the window. `MainWindow` hosts a persistent toggle button and the panel as an overlay above the `ContentControl` that swaps `UnlockViewModel`/`ShellViewModel`, so both screens get it without duplication.

**Tech Stack:** .NET / C#, Avalonia UI (code-behind, no new ViewModel), xUnit (existing `Sekretujo.Core.Tests`).

**Spec:** `docs/superpowers/specs/2026-10-22-virtual-keyboard-design.md`

## Global Constraints

- Layout is QWERTY US standard only — no ñ, accents, ¿¡ (spec, confirmed).
- No Enter/line-break key (spec, confirmed).
- Shift is a persistent toggle (not momentary); no separate Caps Lock.
- "Revolver" shuffles one single pool of all character keys (letters + digits + symbols together); "Restaurar" resets to the standard order. Control keys (Shift, Backspace, Space, Revolver, Restaurar) never move.
- Shuffled state is not persisted across app restarts.
- Panel buttons (and the toggle button) must be `Focusable="False"` so clicking them never moves focus away from the active `TextBox`.
- No automated UI/ViewModel tests by design (CLAUDE.md §9) — the App-layer tasks are verified with `dotnet build` + a manual run, not a test suite. Only the Core-layer logic gets unit tests.
- `Sekretujo.App` has `ImplicitUsings` disabled — new `.cs` files there need explicit `using System;` etc.

## Review Focus

- No `TextBox` has received focus yet when the user opens the panel and clicks a key — must no-op, not throw.
- A `TextBox` has an active text selection when a key is clicked — the selection must be replaced, not left in place with the new character merely inserted next to it.
- Backspace is clicked with the caret at position 0 and no selection — must no-op, not throw an out-of-range exception.
- Focus moves from one `TextBox` to another while the panel stays open (e.g. Username → Password) — the panel must start writing into the newly focused field, not the old one.
- Clicking "Revolver" or "Restaurar" while Shift is active must preserve the Shift state (still show/type uppercase or the shifted symbol), not silently reset to lowercase.

---

### Task 1: Core — key layout data and shuffle

**Files:**
- Create: `src/Sekretujo.Core/Services/VirtualKeyboardLayout.cs`
- Test: `tests/Sekretujo.Core.Tests/Services/VirtualKeyboardLayoutTests.cs`

**Interfaces:**
- Produces: `public sealed record VirtualKeyboardKey(string Normal, string Shifted, int Row);` and `public static class VirtualKeyboardLayout { public static List<VirtualKeyboardKey> CreateStandard(); public static void Shuffle(IList<VirtualKeyboardKey> keys, Random? random = null); }` — `Task 2` consumes both exactly as named.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void CreateStandard_ReturnsFortySevenKeys()
{
    var keys = VirtualKeyboardLayout.CreateStandard();
    Assert.Equal(47, keys.Count);
}

[Fact]
public void CreateStandard_HasNoDuplicateNormalCharacters()
{
    var keys = VirtualKeyboardLayout.CreateStandard();
    Assert.Equal(keys.Count, keys.Select(k => k.Normal).Distinct().Count());
}

[Fact]
public void Shuffle_WithSeededRandom_PermutesButKeepsSameKeys()
{
    var original = VirtualKeyboardLayout.CreateStandard();
    var shuffled = VirtualKeyboardLayout.CreateStandard();

    VirtualKeyboardLayout.Shuffle(shuffled, new Random(42));

    Assert.Equal(
        original.OrderBy(k => k.Normal).Select(k => k.Normal),
        shuffled.OrderBy(k => k.Normal).Select(k => k.Normal));
    Assert.NotEqual(
        original.Select(k => k.Normal).ToList(),
        shuffled.Select(k => k.Normal).ToList());
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Sekretujo.Core.Tests --filter "FullyQualifiedName~VirtualKeyboardLayoutTests"`
Expected: FAIL (compile error — `VirtualKeyboardLayout` does not exist)

- [ ] **Step 3: Implement `VirtualKeyboardKey` and `VirtualKeyboardLayout` in `src/Sekretujo.Core/Services/VirtualKeyboardLayout.cs`**

`CreateStandard()` returns, in this exact order (slot order matters — Task 2 renders button `i` from `keys[i]`, grouped by `Row` into panel rows of length 13/13/11/10):

- Row 0 (13 keys): `` ` ~ ``, `1 !`, `2 @`, `3 #`, `4 $`, `5 %`, `6 ^`, `7 &`, `8 *`, `9 (`, `0 )`, `- _`, `= +`
- Row 1 (13 keys): `q Q`, `w W`, `e E`, `r R`, `t T`, `y Y`, `u U`, `i I`, `o O`, `p P`, `[ {`, `] }`, `\ |`
- Row 2 (11 keys): `a A`, `s S`, `d D`, `f F`, `g G`, `h H`, `j J`, `k K`, `l L`, `; :`, `' "`
- Row 3 (10 keys): `z Z`, `x X`, `c C`, `v V`, `b B`, `n N`, `m M`, `, <`, `. >`, `/ ?`

`Shuffle` is an in-place Fisher-Yates over the whole list (`random ??= Random.Shared`), ignoring `Row` — it only reorders the list; Task 2 is responsible for keeping each button's screen position fixed and just re-reading the new `keys[i]` into it.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Sekretujo.Core.Tests --filter "FullyQualifiedName~VirtualKeyboardLayoutTests"`
Expected: PASS (3 tests)

- [ ] **Step 5: Commit**

```bash
git add src/Sekretujo.Core/Services/VirtualKeyboardLayout.cs tests/Sekretujo.Core.Tests/Services/VirtualKeyboardLayoutTests.cs
git commit -m "feat: add virtual keyboard key layout and shuffle"
```

---

### Task 2: App — VirtualKeyboardPanel control

**Files:**
- Create: `src/Sekretujo.App/Views/VirtualKeyboardPanel.axaml`
- Create: `src/Sekretujo.App/Views/VirtualKeyboardPanel.axaml.cs`

**Interfaces:**
- Consumes: `VirtualKeyboardLayout.CreateStandard()`, `VirtualKeyboardLayout.Shuffle(IList<VirtualKeyboardKey>, Random?)`, `VirtualKeyboardKey(Normal, Shifted, Row)` from Task 1.
- Produces: `public partial class VirtualKeyboardPanel : UserControl { public void SetActiveTextBox(TextBox? textBox); }` — `Task 3` calls `SetActiveTextBox` from `MainWindow`'s focus handler, and toggles the control's own `IsVisible` (inherited from `Control`) to open/close it.

- [ ] **Step 1: Write the XAML**

`VirtualKeyboardPanel.axaml`: a `UserControl` with no `x:DataType` (no data binding — pure code-behind). Root: `Border` with `Classes="card"`, containing a vertical `StackPanel` with four named, empty, horizontal `StackPanel`s (`x:Name="Row0Panel"` … `Row3Panel`) that Step 3 populates in code, followed by a control row with: a `ToggleButton x:Name="ShiftToggle"` (Content "Shift"), a `Button` (Content "⌫") for backspace, a `Button` (Content "Espacio", wide) for space, a `Button` (Content "Revolver"), a `Button` (Content "Restaurar"). Every control in this row has `Focusable="False"` and a `Click` handler (see Step 3 for handler names).

- [ ] **Step 2: Implement the code-behind fields and construction in `VirtualKeyboardPanel.axaml.cs`**

```csharp
public partial class VirtualKeyboardPanel : UserControl
{
    private List<VirtualKeyboardKey> _assignment = VirtualKeyboardLayout.CreateStandard();
    private readonly List<Button> _slots = new();
    private bool _shiftOn;
    private TextBox? _activeTextBox;

    public VirtualKeyboardPanel()
    {
        InitializeComponent();
        BuildKeys();
    }

    public void SetActiveTextBox(TextBox? textBox) => _activeTextBox = textBox;
}
```

`BuildKeys()` iterates `_assignment` by index, creates one `Button` per key (`Focusable = false`, fixed `Width`/`Height` e.g. 34, a `Click` handler closing over that key's index calling a shared `OnKeyClick(int index)`), appends it to `Row0Panel`/`Row1Panel`/`Row2Panel`/`Row3Panel` by `key.Row`, and stores it in `_slots` at that same index — `_slots[i]` and `_assignment[i]` must always refer to the same button/key pairing.

- [ ] **Step 3: Implement label refresh and the control handlers**

`RefreshLabels()` sets `_slots[i].Content = _shiftOn ? _assignment[i].Shifted : _assignment[i].Normal` for every `i`.

Handlers, wired to the XAML from Step 1:
- `OnShiftToggled`: reads `ShiftToggle.IsChecked == true` into `_shiftOn`, calls `RefreshLabels()`.
- `OnShuffleClicked`: calls `VirtualKeyboardLayout.Shuffle(_assignment)`, then `RefreshLabels()` — does **not** touch `_shiftOn` (Review Focus: shuffling must not reset Shift).
- `OnRestoreClicked`: resets `_assignment = VirtualKeyboardLayout.CreateStandard()`, then `RefreshLabels()` — also does not touch `_shiftOn`.
- `OnBackspaceClicked`: calls a `DeleteBeforeCaret()` helper.
- `OnSpaceClicked`: calls `InsertAtCaret(" ")`.
- `OnKeyClick(int index)`: calls `InsertAtCaret(_shiftOn ? _assignment[index].Shifted : _assignment[index].Normal)`.

`InsertAtCaret(string text)`: no-op if `_activeTextBox is null` (Review Focus: nothing focused yet). Otherwise, read `var tb = _activeTextBox;`, compute `start = Math.Min(tb.SelectionStart, tb.SelectionEnd)` and `end = Math.Max(tb.SelectionStart, tb.SelectionEnd)`, replace that range in `tb.Text ?? string.Empty` with `text` (removing the selection even when `start == end`, i.e. no selection — Review Focus: replace, don't just insert next to it), then set `tb.CaretIndex` to `start + text.Length` and `tb.SelectionStart = tb.SelectionEnd = tb.CaretIndex`.

`DeleteBeforeCaret()`: no-op if `_activeTextBox is null`. If there's a selection (`SelectionStart != SelectionEnd`), delete it the same way `InsertAtCaret` would with `text = ""`. Otherwise, no-op if `CaretIndex == 0` (Review Focus: no out-of-range), else remove the one character before `CaretIndex` and move the caret back by one.

- [ ] **Step 4: Verify the build**

Run: `dotnet build Sekretujo.slnx`
Expected: Build succeeds, no warnings about unused `using`s in the new files.

- [ ] **Step 5: Commit**

```bash
git add src/Sekretujo.App/Views/VirtualKeyboardPanel.axaml src/Sekretujo.App/Views/VirtualKeyboardPanel.axaml.cs
git commit -m "feat: add virtual keyboard panel control"
```

---

### Task 3: App — wire the panel into MainWindow

**Files:**
- Modify: `src/Sekretujo.App/Views/MainWindow.axaml`
- Modify: `src/Sekretujo.App/Views/MainWindow.axaml.cs`

**Interfaces:**
- Consumes: `VirtualKeyboardPanel.SetActiveTextBox(TextBox?)` from Task 2; `Control.IsVisible` (built-in) to open/close the panel.

- [ ] **Step 1: Modify `MainWindow.axaml`**

Add `xmlns:views="using:Sekretujo.App.Views"`. Wrap the existing `<ContentControl Content="{Binding CurrentView}" />` and the new controls in a `Grid` (so they overlap):
- The existing `ContentControl`, unchanged.
- A `Button x:Name="KeyboardToggleButton"` (`Classes="ghost"`, `Focusable="False"`, `Click="OnToggleKeyboardClicked"`, anchored bottom-right with margin, containing a `TextBlock` with a keyboard glyph), always visible.
- A `views:VirtualKeyboardPanel x:Name="KeyboardPanel"` anchored to stretch horizontally / bottom vertically, with `IsVisible="False"` initially.

- [ ] **Step 2: Modify `MainWindow.axaml.cs`**

In the constructor, after the existing two `AddHandler` calls, add:
```csharp
AddHandler(GotFocusEvent, OnGotFocus, handledEventsToo: true);
```
Add the two handlers:
```csharp
private void OnGotFocus(object? sender, GotFocusEventArgs e)
{
    if (e.Source is TextBox textBox)
        KeyboardPanel.SetActiveTextBox(textBox);
}

private void OnToggleKeyboardClicked(object? sender, RoutedEventArgs e)
{
    KeyboardPanel.IsVisible = !KeyboardPanel.IsVisible;
}
```
(Review Focus: because `KeyboardToggleButton` and every control inside `KeyboardPanel` are `Focusable="False"`, focus never leaves the active `TextBox` while the user operates the keyboard, so `OnGotFocus` naturally only ever fires for real `TextBox`es and switches `_activeTextBox` correctly when the user clicks into a different field.)

- [ ] **Step 3: Verify the build**

Run: `dotnet build Sekretujo.slnx`
Expected: Build succeeds.

- [ ] **Step 4: Manual verification run**

Run: `dotnet run --project src/Sekretujo.App`

Check, in order:
1. On the unlock screen, click the keyboard toggle, then click letters/digits/symbols to type the master password into the "Master password" field; unlock succeeds.
2. Inside the app, open a password entry; click into Username, then Password, then Notes — typed text lands in whichever field was clicked last, not a stale one.
3. Toggle Shift on: letter keys show/type uppercase, number-row keys show/type their symbol. Toggle it off: back to lowercase/digits.
4. Click "Revolver": key labels rearrange; typing still inserts the character shown on the button (not the original one). Toggle Shift while shuffled: shifted variants still correct. Click "Restaurar": layout returns to standard QWERTY, Shift state unchanged.
5. Click Backspace with the caret at the very start of an empty/short field: no crash. Select some text in a field, click a key: selection is replaced, not left behind.
6. Click the toggle button to close the panel, then reopen it: still targets the last-focused field.

- [ ] **Step 5: Commit**

```bash
git add src/Sekretujo.App/Views/MainWindow.axaml src/Sekretujo.App/Views/MainWindow.axaml.cs
git commit -m "feat: wire virtual keyboard into MainWindow"
```
