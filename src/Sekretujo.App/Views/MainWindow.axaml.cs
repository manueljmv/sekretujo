using System;
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

    private void OnGotFocus(object? sender, RoutedEventArgs e)
    {
        if (e.Source is TextBox textBox)
            KeyboardPanel.SetActiveTextBox(textBox);
    }
}
