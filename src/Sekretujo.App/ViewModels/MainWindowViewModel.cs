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
        ShowUnlock();
    }

    private void ShowUnlock()
    {
        var unlock = _services.GetRequiredService<UnlockViewModel>();
        unlock.Unlocked += ShowShell;
        CurrentView = unlock;
    }

    private void ShowShell()
    {
        var shell = _services.GetRequiredService<ShellViewModel>();
        shell.Locked += ShowUnlock;
        CurrentView = shell;
    }
}
