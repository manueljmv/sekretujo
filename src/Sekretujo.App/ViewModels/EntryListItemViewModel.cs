using System;
using System.Collections.Generic;
using Sekretujo.Core.Models;

namespace Sekretujo.App.ViewModels;

public sealed class EntryListItemViewModel
{
    public EntryListItemViewModel(VaultEntry entry)
    {
        Id = entry.Id;
        Title = entry.Title;
        IsFavorite = entry.IsFavorite;
        IsPassword = entry is PasswordItem;
        Tags = entry.Tags;
    }

    public Guid Id { get; }
    public string Title { get; }
    public bool IsFavorite { get; }
    public bool IsPassword { get; }
    public List<string> Tags { get; }
}
