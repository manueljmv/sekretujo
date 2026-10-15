using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Sekretujo.App.Services;

namespace Sekretujo.App.ViewModels;

public partial class ShellViewModel : ViewModelBase
{
    private readonly VaultSession _session;
    private readonly IServiceProvider _services;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _favoritesOnly;

    [ObservableProperty]
    private EntryListItemViewModel? _selectedEntry;

    [ObservableProperty]
    private EntryDetailViewModel? _detail;

    public ObservableCollection<EntryListItemViewModel> Entries { get; } = new();

    public event Action? Locked;

    public ShellViewModel(VaultSession session, IServiceProvider services)
    {
        _session = session;
        _services = services;
        Refresh();
    }

    partial void OnSearchTextChanged(string value) => Refresh();
    partial void OnFavoritesOnlyChanged(bool value) => Refresh();

    partial void OnSelectedEntryChanged(EntryListItemViewModel? value)
    {
        if (value is null || _session.Data is null) { Detail = null; return; }

        var entry = _session.Data.Entries.First(e => e.Id == value.Id);
        var detail = _services.GetRequiredService<EntryDetailViewModel>();
        detail.LoadFrom(entry);
        detail.Saved += OnEntrySaved;
        detail.DeleteRequested += OnEntryDeleted;
        Detail = detail;
    }

    [RelayCommand]
    private void Lock()
    {
        _session.Close();
        Locked?.Invoke();
    }

    [RelayCommand]
    private void NewPassword() => StartNewEntry(new Core.Models.PasswordItem { Title = "Nueva entrada" });

    [RelayCommand]
    private void NewNote() => StartNewEntry(new Core.Models.NoteItem { Title = "Nueva nota" });

    private void StartNewEntry(Core.Models.VaultEntry entry)
    {
        _session.Data!.Entries.Add(entry);
        Refresh();
        SelectedEntry = Entries.First(e => e.Id == entry.Id);
    }

    private void OnEntrySaved(Core.Models.VaultEntry updated)
    {
        if (_session.Data is null) return;
        var index = _session.Data.Entries.FindIndex(e => e.Id == updated.Id);
        if (index >= 0) _session.Data.Entries[index] = updated;
        Refresh();
    }

    private void OnEntryDeleted(Guid id)
    {
        _session.Data?.Entries.RemoveAll(e => e.Id == id);
        Detail = null;
        Refresh();
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
