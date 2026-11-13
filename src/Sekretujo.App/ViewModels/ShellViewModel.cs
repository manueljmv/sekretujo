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
    Notes,
    Cards
}

public partial class ShellViewModel : ViewModelBase, IDisposable
{
    private readonly VaultSession _session;
    private readonly VaultRepository _repository;
    private readonly IServiceProvider _services;
    private readonly AutoLockService _autoLock;
    private readonly DispatcherTimer _autoLockDisplayTimer;
    private readonly AppSettings _appSettings;
    private readonly AppSettingsStore _appSettingsStore;
    private readonly HashSet<string> _collapsedTags;
    private bool _collapsedTagsChanged;
    private bool _keepDetail;
    private Action? _pendingAfterDiscard;

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
    private bool _isConfirmingDiscard;

    [ObservableProperty]
    private bool _isAutoLockActive;

    [ObservableProperty]
    private double _autoLockProgress;

    [ObservableProperty]
    private string _autoLockTooltip = string.Empty;

    public ObservableCollection<EntryListItemViewModel> Entries { get; } = new();
    public ObservableCollection<EntryGroupViewModel> Groups { get; } = new();

    public event Action? Locked;

    public ShellViewModel(VaultSession session, VaultRepository repository, IServiceProvider services,
        AutoLockService autoLock, AppSettings appSettings, AppSettingsStore appSettingsStore)
    {
        _session = session;
        _repository = repository;
        _services = services;
        _autoLock = autoLock;
        _appSettings = appSettings;
        _appSettingsStore = appSettingsStore;
        _collapsedTags = new HashSet<string>(session.Data?.CollapsedTags ?? new List<string>(),
            StringComparer.OrdinalIgnoreCase);
        TypeFilter = appSettings.LastTypeFilter;
        SearchText = session.Data?.SearchText ?? string.Empty;
        _autoLockDisplayTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _autoLockDisplayTimer.Tick += (_, _) => RefreshAutoLockDisplay();
        _autoLockDisplayTimer.Start();
        RefreshAutoLockDisplay();
        Refresh();
    }

    private void RefreshAutoLockDisplay()
    {
        var remaining = _autoLock.Remaining;
        var duration = _autoLock.Duration;
        IsAutoLockActive = remaining is not null && duration > TimeSpan.Zero;
        AutoLockProgress = IsAutoLockActive ? 100 * remaining!.Value / duration : 0;
        AutoLockTooltip = remaining is { } value
            ? $"Autobloqueo en {(int)value.TotalMinutes}:{value.Seconds:D2}"
            : string.Empty;
    }

    partial void OnSearchTextChanged(string value) => Refresh();
    partial void OnFavoritesOnlyChanged(bool value) => Refresh();
    partial void OnTypeFilterChanged(EntryTypeFilter value)
    {
        Refresh();
        if (_appSettings.LastTypeFilter == value) return;
        _appSettings.LastTypeFilter = value;
        _appSettingsStore.Save(_appSettings);
    }

    [RelayCommand]
    private void SetTypeFilter(EntryTypeFilter filter) => TypeFilter = filter;

    [RelayCommand]
    private void SelectEntry(EntryListItemViewModel entry)
    {
        var id = entry.Id;
        if (id == Detail?.Id) return;
        RunAfterDiscardConfirmation(() => SelectedEntry = Entries.FirstOrDefault(e => e.Id == id));
    }

    // Returns true when the action was deferred until the user confirms discarding the unsaved edits.
    public bool ConfirmDiscardBefore(Action action)
    {
        if (Detail is not { IsDirty: true }) return false;
        _pendingAfterDiscard = action;
        IsConfirmingDiscard = true;
        return true;
    }

    private void RunAfterDiscardConfirmation(Action action)
    {
        if (!ConfirmDiscardBefore(action)) action();
    }

    [RelayCommand]
    private void ConfirmDiscard()
    {
        var action = _pendingAfterDiscard;
        CancelDiscard();
        Detail?.DiscardCommand.Execute(null);
        action?.Invoke();
    }

    [RelayCommand]
    private void CancelDiscard()
    {
        _pendingAfterDiscard = null;
        IsConfirmingDiscard = false;
    }

    partial void OnSelectedEntryChanged(EntryListItemViewModel? value)
    {
        foreach (var item in Entries)
            item.IsSelected = ReferenceEquals(item, value);

        // Refresh rebuilds every list item, so a new instance for the same entry must not recreate the open form
        // (and drop its unsaved edits).
        if (value is null ? _keepDetail : Detail?.Id == value.Id) return;

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
    private void Lock() => Locked?.Invoke();

    // Collapsed groups and search text are only written on lock/close, so browsing never rewrites the vault file.
    public void Dispose()
    {
        _autoLockDisplayTimer.Stop();
        Detail?.Dispose();
        if (_session.Data is null) return;
        if (!_collapsedTagsChanged && SearchText == _session.Data.SearchText) return;
        _session.Data.CollapsedTags = _collapsedTags.ToList();
        _session.Data.SearchText = SearchText;
        _collapsedTagsChanged = false;
        Persist();
    }

    [RelayCommand]
    private void NewPassword() => RunAfterDiscardConfirmation(
        () => StartNewEntry(new Core.Models.PasswordItem { Title = "Nueva entrada" }));

    [RelayCommand]
    private void NewNote() => RunAfterDiscardConfirmation(
        () => StartNewEntry(new Core.Models.NoteItem { Title = "Nueva nota" }));

    [RelayCommand]
    private void NewCard() => RunAfterDiscardConfirmation(
        () => StartNewEntry(new Core.Models.CardItem { Title = "Nueva tarjeta" }));

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
        if (SelectedEntry?.Id == entry.Id) return;

        // The new entry is hidden by the active filters, so it can't be selected and no form shows.
        SearchText = string.Empty;
        FavoritesOnly = false;
        if (TypeFilter == EntryTypeFilter.Passwords && entry is not Core.Models.PasswordItem
            || TypeFilter == EntryTypeFilter.Notes && entry is not Core.Models.NoteItem
            || TypeFilter == EntryTypeFilter.Cards && entry is not Core.Models.CardItem)
            TypeFilter = EntryTypeFilter.All;
        Refresh(selectAfterRefresh: entry.Id);
    }

    private void OnEntrySaved(Core.Models.VaultEntry updated)
    {
        if (_session.Data is null) return;
        var index = _session.Data.Entries.FindIndex(e => e.Id == updated.Id);
        if (index < 0) return;

        var previous = _session.Data.Entries[index];
        _session.Data.Entries[index] = updated;
        if (!Persist())
        {
            _session.Data.Entries[index] = previous;
            return;
        }
        Detail?.MarkSaved(updated);
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

    private bool Persist()
    {
        if (_session.Data is null || _session.FilePath is null || _session.Key is null) return false;
        try
        {
            _repository.Save(_session.FilePath, _session.Key, _session.Data);
            StatusMessage = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"No se pudo guardar el vault: {ex.Message}";
            return false;
        }
    }

    public void Refresh() => Refresh(selectAfterRefresh: null);

    private void Refresh(Guid? selectAfterRefresh)
    {
        var preserveId = selectAfterRefresh ?? Detail?.Id;

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
            EntryTypeFilter.Cards => query.Where(e => e is Core.Models.CardItem),
            _ => query
        };
        if (!string.IsNullOrWhiteSpace(SearchText))
            query = query.Where(e => e.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                || e.Tags.Any(tag => tag.Contains(SearchText, StringComparison.OrdinalIgnoreCase)));

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

        var selected = preserveId is { } id ? Entries.FirstOrDefault(e => e.Id == id) : null;
        _keepDetail = selected is null && Detail is { IsDirty: true };
        SelectedEntry = selected;
        _keepDetail = false;
    }

    private void OnGroupPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(EntryGroupViewModel.IsExpanded) || sender is not EntryGroupViewModel group)
            return;
        _collapsedTagsChanged |= group.IsExpanded
            ? _collapsedTags.Remove(group.TagName)
            : _collapsedTags.Add(group.TagName);
    }
}
