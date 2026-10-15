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
            if (DataContext is EntryDetailViewModel vm && TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
                vm.AttachClipboard(clipboard);
        };
    }
}
