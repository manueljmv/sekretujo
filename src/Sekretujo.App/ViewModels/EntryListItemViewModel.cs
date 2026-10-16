using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using Sekretujo.Core.Models;

namespace Sekretujo.App.ViewModels;

public sealed partial class EntryListItemViewModel : ViewModelBase
{
    public EntryListItemViewModel(VaultEntry entry)
    {
        Id = entry.Id;
        Title = entry.Title;
        IsFavorite = entry.IsFavorite;
        IsPassword = entry is PasswordItem;
        Tags = entry.Tags;

        TypeLabel = IsPassword ? "Contraseña" : "Nota";
        Icon = IsPassword ? "\U0001F511" : "\U0001F4C4";
        Subtitle = entry switch
        {
            PasswordItem password => string.IsNullOrWhiteSpace(password.Username) ? "sin usuario" : password.Username,
            NoteItem note => string.IsNullOrWhiteSpace(note.Body) ? "vacía" : Truncate(note.Body, 24),
            _ => string.Empty
        };
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength] + "...";

    public Guid Id { get; }
    public string Title { get; }
    public bool IsFavorite { get; }
    public bool IsPassword { get; }
    public List<string> Tags { get; }
    public string TypeLabel { get; }
    public string Subtitle { get; }
    public string Icon { get; }

    [ObservableProperty]
    private bool _isSelected;
}
