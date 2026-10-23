using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Sekretujo.App.ViewModels;

public sealed partial class EntryGroupViewModel : ObservableObject
{
    public EntryGroupViewModel(string tagName) => TagName = tagName;

    public string TagName { get; }
    public ObservableCollection<EntryListItemViewModel> Items { get; } = new();

    [ObservableProperty] private bool _isExpanded = true;

    [RelayCommand]
    private void ToggleExpanded() => IsExpanded = !IsExpanded;
}
