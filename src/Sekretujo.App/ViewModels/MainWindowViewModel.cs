using CommunityToolkit.Mvvm.ComponentModel;

namespace Sekretujo.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private ViewModelBase? _currentView;
}
