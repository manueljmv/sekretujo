using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Sekretujo.App.Services;
using Sekretujo.Core.Storage;

namespace Sekretujo.App.ViewModels;

public enum EntryTypeFilter
{
    All,
    Passwords,
    Notes
}

public partial class ShellViewModel : ViewModelBase, IDisposable
{
    private readonly VaultSession _session;
    private readonly VaultRepository _repository;
    private readonly IServiceProvider _services;
    private readonly AutoLockService _autoLock;
    private readonly DispatcherTimer _autoLockDisplayTimer;
    private readonly HashSet<string> _collapsedTags = new(StringComparer.OrdinalIgnoreCase);

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _favoritesOnly;

    [ObservableProperty]
    private EntryTypeFilter _typeFilter = EntryTypeFilter.All;

    [ObservableProperty]
    private EntryListItemViewModel? _selectedEntry;

    [ObservableProperty]
    private EntryDetailViewModel? _detail;

    [ObservableProperty]
    private SettingsViewModel? _settingsOverlay;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _autoLockDisplay = string.Empty;

    public ObservableCollection<EntryListItemViewModel> Entries { get; } = new();
    public ObservableCollection<EntryGroupViewModel> Groups { get; } = new();

    public event Action? Locked;

    public ShellViewModel(VaultSession session, VaultRepository repository, IServiceProvider services,
        AutoLockService autoLock)
    {
        _session = session;
        _repository = repository;
        _services = services;
        _autoLock = autoLock;
        _autoLockDisplayTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _autoLockDisplayTimer.Tick += (_, _) => RefreshAutoLockDisplay();
        _autoLockDisplayTimer.Start();
        RefreshAutoLockDisplay();
        Refresh();
    }

    private void RefreshAutoLockDisplay()
    {
        var remaining = _autoLock.Remaining;
        AutoLockDisplay = remaining is { } value
            ? $"Autobloqueo en {(int)value.TotalMinutes}:{value.Seconds:D2}"
            : string.Empty;
    }

    partial void OnSearchTextChanged(string value) => Refresh();
    partial void OnFavoritesOnlyChanged(bool value) => Refresh();
    partial void OnTypeFilterChanged(EntryTypeFilter value) => Refresh();

    [RelayCommand]
    private void SetTypeFilter(EntryTypeFilter filter) => TypeFilter = filter;

    [RelayCommand]
    private void SelectEntry(EntryListItemViewModel entry) => SelectedEntry = entry;

    partial void OnSelectedEntryChanged(EntryListItemViewModel? value)
    {
        foreach (var item in Entries)
            item.IsSelected = ReferenceEquals(item, value);

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

    public void Dispose()
    {
        _autoLockDisplayTimer.Stop();
        Detail?.Dispose();
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
        Groups.Clear();
        if (_session.Data is null)
        {
            SelectedEntry = null;
            return;
        }

        var query = _session.Data.Entries.AsEnumerable();
        if (FavoritesOnly)
            query = query.Where(e => e.IsFavorite);
        query = TypeFilter switch
        {
            EntryTypeFilter.Passwords => query.Where(e => e is Core.Models.PasswordItem),
            EntryTypeFilter.Notes => query.Where(e => e is Core.Models.NoteItem),
            _ => query
        };
        if (!string.IsNullOrWhiteSpace(SearchText))
            query = query.Where(e => e.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

        var items = query.Select(e => new EntryListItemViewModel(e)).ToList();
        foreach (var item in items)
            Entries.Add(item);

        const string untagged = "Sin etiqueta";
        var byTag = items
            .SelectMany(item => item.Tags.Count == 0
                ? new[] { (Tag: untagged, Item: item) }
                : item.Tags.Select(tag => (Tag: tag, Item: item)))
            .GroupBy(x => x.Tag, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => string.Equals(g.Key, untagged, StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var group in byTag)
        {
            var groupVm = new EntryGroupViewModel(group.Key) { IsExpanded = !_collapsedTags.Contains(group.Key) };
            foreach (var (_, item) in group)
                groupVm.Items.Add(item);
            groupVm.PropertyChanged += OnGroupPropertyChanged;
            Groups.Add(groupVm);
        }

        SelectedEntry = preserveId is { } id ? Entries.FirstOrDefault(e => e.Id == id) : null;
    }

    private void OnGroupPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(EntryGroupViewModel.IsExpanded) || sender is not EntryGroupViewModel group)
            return;
        if (group.IsExpanded)
            _collapsedTags.Remove(group.TagName);
        else
            _collapsedTags.Add(group.TagName);
    }
}
