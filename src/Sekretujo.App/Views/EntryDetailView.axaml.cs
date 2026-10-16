using Avalonia.Controls;
using Sekretujo.App.ViewModels;

namespace Sekretujo.App.Views;

public partial class EntryDetailView : UserControl
{
    public EntryDetailView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is not EntryDetailViewModel vm) return;
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.Clipboard is { } clipboard) vm.AttachClipboard(clipboard);
            if (topLevel?.Launcher is { } launcher) vm.AttachLauncher(launcher);
        };
    }
}
