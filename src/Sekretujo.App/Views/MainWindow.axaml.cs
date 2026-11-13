using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Sekretujo.App.ViewModels;

namespace Sekretujo.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AddHandler(PointerMovedEvent, OnActivity, handledEventsToo: true);
        AddHandler(KeyDownEvent, OnActivity, handledEventsToo: true);
        AddHandler(GotFocusEvent, OnGotFocus, handledEventsToo: true);
    }

    private void OnActivity(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.NotifyActivity();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        // Only a user-initiated close waits for the prompt; an OS or application shutdown must not be held up.
        if (e.CloseReason == WindowCloseReason.WindowClosing && DataContext is MainWindowViewModel vm)
        {
            if (vm.ConfirmDiscardBefore(Close))
            {
                e.Cancel = true;
            }
            else if (vm.HasClipboardSecret)
            {
                // Clipboard calls are async, so this close is cancelled and repeated once the secret is gone.
                e.Cancel = true;
                _ = CloseAfterClearingClipboard(vm);
            }
        }
        base.OnClosing(e);
    }

    private async Task CloseAfterClearingClipboard(MainWindowViewModel vm)
    {
        await vm.ClearClipboardAsync();
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        (DataContext as MainWindowViewModel)?.Shutdown();
        base.OnClosed(e);
    }

    private void OnGotFocus(object? sender, RoutedEventArgs e)
    {
        if (e.Source is TextBox textBox)
            KeyboardPanel.SetActiveTextBox(textBox);
    }
}
