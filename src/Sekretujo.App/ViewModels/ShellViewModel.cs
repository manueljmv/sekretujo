using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sekretujo.App.Services;

namespace Sekretujo.App.ViewModels;

public partial class ShellViewModel : ViewModelBase
{
    private readonly VaultSession _session;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _favoritesOnly;

    [ObservableProperty]
    private EntryListItemViewModel? _selectedEntry;

    public ObservableCollection<EntryListItemViewModel> Entries { get; } = new();

    public event Action? Locked;

    public ShellViewModel(VaultSession session)
    {
        _session = session;
        Refresh();
    }

    partial void OnSearchTextChanged(string value) => Refresh();
    partial void OnFavoritesOnlyChanged(bool value) => Refresh();

    [RelayCommand]
    private void Lock()
    {
        _session.Close();
        Locked?.Invoke();
    }

    public void Refresh()
    {
        Entries.Clear();
        if (_session.Data is null) return;

        var query = _session.Data.Entries.AsEnumerable();
        if (FavoritesOnly)
            query = query.Where(e => e.IsFavorite);
        if (!string.IsNullOrWhiteSpace(SearchText))
            query = query.Where(e => e.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

        foreach (var entry in query)
            Entries.Add(new EntryListItemViewModel(entry));
    }
}
