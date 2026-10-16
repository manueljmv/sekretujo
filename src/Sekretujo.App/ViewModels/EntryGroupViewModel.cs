using System.Collections.ObjectModel;

namespace Sekretujo.App.ViewModels;

public sealed class EntryGroupViewModel
{
    public EntryGroupViewModel(string tagName) => TagName = tagName;

    public string TagName { get; }
    public ObservableCollection<EntryListItemViewModel> Items { get; } = new();
}
