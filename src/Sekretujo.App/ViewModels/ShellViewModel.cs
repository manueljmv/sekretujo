using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Sekretujo.App.Services;
using Sekretujo.Core.Storage;

namespace Sekretujo.App.ViewModels;

public partial class ShellViewModel : ViewModelBase
{
    private readonly VaultSession _session;
    private readonly VaultRepository _repository;
    private readonly IServiceProvider _services;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _favoritesOnly;

    [ObservableProperty]
    private EntryListItemViewModel? _selectedEntry;

    [ObservableProperty]
    private EntryDetailViewModel? _detail;

    [ObservableProperty]
    private SettingsViewModel? _settingsOverlay;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public ObservableCollection<EntryListItemViewModel> Entries { get; } = new();

    public event Action? Locked;

    public ShellViewModel(VaultSession session, VaultRepository repository, IServiceProvider services)
    {
        _session = session;
        _repository = repository;
        _services = services;
        Refresh();
    }

    partial void OnSearchTextChanged(string value) => Refresh();
    partial void OnFavoritesOnlyChanged(bool value) => Refresh();

    partial void OnSelectedEntryChanged(EntryListItemViewModel? value)
    {
        Detail?.Dispose();

        if (value is null || _session.Data is null) { Detail = null; return; }

        var entry = _session.Data.Entries.FirstOrDefault(e => e.Id == value.Id);
        if (entry is null) { Detail = null; return; }

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

    [RelayCommand]
    private void OpenSettings()
    {
        var settings = _services.GetRequiredService<SettingsViewModel>();
        settings.Closed += () => SettingsOverlay = null;
        settings.SettingsChanged += Refresh;
        SettingsOverlay = settings;
    }

    private void StartNewEntry(Core.Models.VaultEntry entry)
    {
        _session.Data!.Entries.Add(entry);
        Persist();
        Refresh(selectAfterRefresh: entry.Id);
    }

    private void OnEntrySaved(Core.Models.VaultEntry updated)
    {
        if (_session.Data is null) return;
        var index = _session.Data.Entries.FindIndex(e => e.Id == updated.Id);
        if (index >= 0) _session.Data.Entries[index] = updated;
        Persist();
        Refresh(selectAfterRefresh: updated.Id);
    }

    private void OnEntryDeleted(Guid id)
    {
        _session.Data?.Entries.RemoveAll(e => e.Id == id);
        Detail?.Dispose();
        Detail = null;
        Persist();
        Refresh();
    }

    private void Persist()
    {
        if (_session.Data is null || _session.FilePath is null || _session.MasterPassword is null) return;
        try
        {
            _repository.Save(_session.FilePath, _session.MasterPassword, _session.Data);
            StatusMessage = string.Empty;
        }
        catch (Exception ex)
        {
            StatusMessage = $"No se pudo guardar el vault: {ex.Message}";
        }
    }

    public void Refresh() => Refresh(selectAfterRefresh: null);

    private void Refresh(Guid? selectAfterRefresh)
    {
        var preserveId = selectAfterRefresh ?? SelectedEntry?.Id;

        Entries.Clear();
        if (_session.Data is null)
        {
            SelectedEntry = null;
            return;
        }

        var query = _session.Data.Entries.AsEnumerable();
        if (FavoritesOnly)
            query = query.Where(e => e.IsFavorite);
        if (!string.IsNullOrWhiteSpace(SearchText))
            query = query.Where(e => e.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

        foreach (var entry in query)
            Entries.Add(new EntryListItemViewModel(entry));

        SelectedEntry = preserveId is { } id ? Entries.FirstOrDefault(e => e.Id == id) : null;
    }
}
