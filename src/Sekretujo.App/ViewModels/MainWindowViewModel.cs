using System;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Sekretujo.App.Services;

namespace Sekretujo.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IServiceProvider _services;
    private readonly AutoLockService _autoLock;
    private readonly VaultSession _session;
    private readonly AppSettings _appSettings;

    [ObservableProperty]
    private ViewModelBase? _currentView;

    public MainWindowViewModel(IServiceProvider services, AutoLockService autoLock, VaultSession session,
        AppSettings appSettings)
    {
        _services = services;
        _autoLock = autoLock;
        _session = session;
        _appSettings = appSettings;
        _autoLock.TimedOut += () => Dispatcher.UIThread.Post(ShowUnlock);
        ShowUnlock();
    }

    private void ShowUnlock()
    {
        _autoLock.Stop();
        _session.Close();
        var unlock = _services.GetRequiredService<UnlockViewModel>();
        unlock.Unlocked += ShowShell;
        CurrentView = unlock;
    }

    private void ShowShell()
    {
        var shell = _services.GetRequiredService<ShellViewModel>();
        shell.Locked += ShowUnlock;
        CurrentView = shell;
        _autoLock.Start(TimeSpan.FromMinutes(_appSettings.AutoLockMinutes));
    }

    public void NotifyActivity() => _autoLock.NotifyActivity();
}
