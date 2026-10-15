using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace Sekretujo.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IServiceProvider _services;

    [ObservableProperty]
    private ViewModelBase? _currentView;

    public MainWindowViewModel(IServiceProvider services)
    {
        _services = services;
        var unlock = services.GetRequiredService<UnlockViewModel>();
        unlock.Unlocked += () => CurrentView = services.GetRequiredService<UnlockViewModel>();
        CurrentView = unlock;
    }
}
