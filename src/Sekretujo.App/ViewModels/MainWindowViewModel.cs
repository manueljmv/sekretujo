using System;
using System.Threading.Tasks;
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
    private readonly ClipboardAutoClearService _clipboardAutoClear;

    [ObservableProperty]
    private ViewModelBase? _currentView;

    [ObservableProperty]
    private string _windowTitle = "Sekretujo";

    public MainWindowViewModel(IServiceProvider services, AutoLockService autoLock, VaultSession session,
        AppSettings appSettings, ClipboardAutoClearService clipboardAutoClear)
    {
        _clipboardAutoClear = clipboardAutoClear;
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
        (CurrentView as IDisposable)?.Dispose();
        _session.Close();
        _ = _clipboardAutoClear.ClearNowAsync();
        var unlock = _services.GetRequiredService<UnlockViewModel>();
        unlock.Unlocked += ShowShell;
        CurrentView = unlock;
        WindowTitle = "Sekretujo";
    }

    private void ShowShell()
    {
        var shell = _services.GetRequiredService<ShellViewModel>();
        shell.Locked += ShowUnlock;
        CurrentView = shell;
        _autoLock.Start(TimeSpan.FromMinutes(_appSettings.AutoLockMinutes));
        WindowTitle = _session.FilePath is null ? "Sekretujo" : $"Sekretujo — {_session.FilePath}";
    }

    public void NotifyActivity() => _autoLock.NotifyActivity();

    public bool ConfirmDiscardBefore(Action action) =>
        CurrentView is ShellViewModel shell && shell.ConfirmDiscardBefore(action);

    public bool HasClipboardSecret => _clipboardAutoClear.HasPending;

    public Task ClearClipboardAsync() => _clipboardAutoClear.ClearNowAsync();

    public void Shutdown()
    {
        _autoLock.Stop();
        (CurrentView as IDisposable)?.Dispose();
        CurrentView = null;
        _session.Close();
    }
}
