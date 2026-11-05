using Avalonia.Controls;
using Sekretujo.App.ViewModels;
using Sekretujo.Core.Services;

namespace Sekretujo.App.Views;

public partial class EntryDetailView : UserControl
{
    public EntryDetailView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => AttachTopLevelServices();
        AttachedToVisualTree += (_, _) => AttachTopLevelServices();
    }

    private void OnCardExpiryTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox box || box.Text is null) return;
        var formatted = CardInfo.FormatExpiry(box.Text);
        if (formatted == box.Text) return;
        box.Text = formatted;
        box.CaretIndex = formatted.Length;
    }

    private void AttachTopLevelServices()
    {
        if (DataContext is not EntryDetailViewModel vm) return;
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard is { } clipboard) vm.AttachClipboard(clipboard);
        if (topLevel?.Launcher is { } launcher) vm.AttachLauncher(launcher);
    }
}
