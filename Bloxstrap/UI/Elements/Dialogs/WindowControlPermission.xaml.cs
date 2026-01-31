using System.Windows;
using System.Collections.ObjectModel;
using Bloxstrap.Integrations;
using Bloxstrap.UI.ViewModels.Dialogs;

namespace Bloxstrap.UI.Elements.Dialogs;

/// <summary>
/// Interaction logic for WindowControlPermission.xaml
/// </summary>
public partial class WindowControlPermission
{
    public MessageBoxResult Result = MessageBoxResult.Cancel;

    public ActivityWatcher _activityWatcher;

    private WindowControlPermissionViewModel viewModel;

    public WindowControlPermission(ActivityWatcher activityWatcher)
    {
        _activityWatcher = activityWatcher;
        viewModel = new WindowControlPermissionViewModel(activityWatcher);

        viewModel.RequestCloseEvent += (_, _) => Close();

        DataContext = viewModel;
        InitializeComponent();
    }

    private void OKButton_Click(object sender, RoutedEventArgs e)
    {
        Result = MessageBoxResult.OK;
        if (!WindowAllowedUniverses.Contains(_activityWatcher.Data.UniverseId))
        {
            WindowAllowedUniverses.Add(_activityWatcher.Data.UniverseId);
            App.Settings.Save();

            if (_activityWatcher.watcher.WindowController != null)
            {
                _activityWatcher.watcher.WindowController.updateExposedPerms();
            }
        }
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (viewModel.BlacklistFromAsking)
        {
            if ((!WindowAllowedUniverses.Contains(_activityWatcher.Data.UniverseId)) && (!WindowBlacklistedUniverses.Contains(_activityWatcher.Data.UniverseId)))
            {
                WindowBlacklistedUniverses.Add(_activityWatcher.Data.UniverseId);
                App.Settings.Save();
            }
        }
        Close();
    }

    public ObservableCollection<long> WindowAllowedUniverses
    {
        get => App.Settings.Prop.WindowAllowedUniverses;
        set => App.Settings.Prop.WindowAllowedUniverses = value;
    }
        
     public ObservableCollection<long> WindowBlacklistedUniverses
    {
        get => App.Settings.Prop.WindowBlacklistedUniverses;
        set => App.Settings.Prop.WindowBlacklistedUniverses = value;
    }
}