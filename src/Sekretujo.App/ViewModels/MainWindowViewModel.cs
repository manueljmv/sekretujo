using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Sekretujo.App.Services;

namespace Sekretujo.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IServiceProvider _services;
    private readonly AutoLockService _autoLock;

    [ObservableProperty]
    private ViewModelBase? _currentView;

    public MainWindowViewModel(IServiceProvider services, AutoLockService autoLock)
    {
        _services = services;
        _autoLock = autoLock;
        _autoLock.TimedOut += () => ShowUnlock();
        ShowUnlock();
    }

    private void ShowUnlock()
    {
        _autoLock.Stop();
        var unlock = _services.GetRequiredService<UnlockViewModel>();
        unlock.Unlocked += ShowShell;
        CurrentView = unlock;
    }

    private void ShowShell()
    {
        var shell = _services.GetRequiredService<ShellViewModel>();
        shell.Locked += ShowUnlock;
        CurrentView = shell;
        _autoLock.Start(TimeSpan.FromMinutes(5));
    }

    public void NotifyActivity() => _autoLock.NotifyActivity();
}
