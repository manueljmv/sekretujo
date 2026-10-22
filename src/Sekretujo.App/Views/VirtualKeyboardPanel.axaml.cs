using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Sekretujo.Core.Services;

namespace Sekretujo.App.Views;

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

    public void SetActiveTextBox(TextBox? textBox)
    {
        if (_activeTextBox is not null)
            _activeTextBox.DetachedFromVisualTree -= OnActiveTextBoxDetached;
        _activeTextBox = textBox;
        if (_activeTextBox is not null)
            _activeTextBox.DetachedFromVisualTree += OnActiveTextBoxDetached;
    }

    private void OnActiveTextBoxDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (ReferenceEquals(sender, _activeTextBox))
            _activeTextBox = null;
    }

    private void OnToggleExpandClicked(object? sender, RoutedEventArgs e)
    {
        var expanding = !KeysPanel.IsVisible;
        KeysPanel.IsVisible = expanding;
        if (expanding) ResetLayout();
    }

    private void ResetLayout()
    {
        _assignment = VirtualKeyboardLayout.CreateStandard();
        _shiftOn = false;
        ShiftToggle.IsChecked = false;
        RefreshLabels();
    }

    private void BuildKeys()
    {
        var rows = new[] { Row0Panel, Row1Panel, Row2Panel, Row3Panel };
        for (var i = 0; i < _assignment.Count; i++)
        {
            var index = i;
            var key = _assignment[i];
            var button = new Button
            {
                Content = key.Normal,
                Width = 34,
                Height = 34,
                Padding = default,
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                Focusable = false
            };
            button.Click += (_, _) => OnKeyClick(index);
            rows[key.Row].Children.Add(button);
            _slots.Add(button);
        }
    }

    private void RefreshLabels()
    {
        for (var i = 0; i < _slots.Count; i++)
            _slots[i].Content = _shiftOn ? _assignment[i].Shifted : _assignment[i].Normal;
    }

    private void OnKeyClick(int index) =>
        InsertAtCaret(_shiftOn ? _assignment[index].Shifted : _assignment[index].Normal);

    private void OnShiftToggled(object? sender, RoutedEventArgs e)
    {
        _shiftOn = ShiftToggle.IsChecked == true;
        RefreshLabels();
    }

    private void OnShuffleClicked(object? sender, RoutedEventArgs e)
    {
        VirtualKeyboardLayout.Shuffle(_assignment);
        RefreshLabels();
    }

    private void OnRestoreClicked(object? sender, RoutedEventArgs e)
    {
        _assignment = VirtualKeyboardLayout.CreateStandard();
        RefreshLabels();
    }

    private void OnBackspaceClicked(object? sender, RoutedEventArgs e) => DeleteBeforeCaret();

    private void OnSpaceClicked(object? sender, RoutedEventArgs e) => InsertAtCaret(" ");

    private void InsertAtCaret(string text)
    {
        if (_activeTextBox is null) return;
        var tb = _activeTextBox;
        var start = Math.Min(tb.SelectionStart, tb.SelectionEnd);
        var end = Math.Max(tb.SelectionStart, tb.SelectionEnd);
        var oldText = tb.Text ?? string.Empty;
        tb.Text = oldText.Remove(start, end - start).Insert(start, text);
        tb.CaretIndex = start + text.Length;
        tb.SelectionStart = tb.SelectionEnd = tb.CaretIndex;
    }

    private void DeleteBeforeCaret()
    {
        if (_activeTextBox is null) return;
        var tb = _activeTextBox;
        if (tb.SelectionStart != tb.SelectionEnd)
        {
            InsertAtCaret(string.Empty);
            return;
        }
        if (tb.CaretIndex == 0) return;
        var oldText = tb.Text ?? string.Empty;
        tb.Text = oldText.Remove(tb.CaretIndex - 1, 1);
        tb.CaretIndex -= 1;
        tb.SelectionStart = tb.SelectionEnd = tb.CaretIndex;
    }
}
