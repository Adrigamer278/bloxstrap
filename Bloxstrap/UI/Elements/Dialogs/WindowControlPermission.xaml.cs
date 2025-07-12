using System.Windows;
using Bloxstrap.AppData;
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

    public WindowControlPermission(ActivityWatcher watcher)
    {
        _activityWatcher = watcher;
        var viewModel = new WindowControlPermissionViewModel(watcher);

        viewModel.RequestCloseEvent += (_, _) => Close();

        DataContext = viewModel;
        InitializeComponent();
    }

    private void OKButton_Click(object sender, RoutedEventArgs e)
    {
        Result = MessageBoxResult.OK;
        if (!App.Settings.Prop.WindowControlAllowedUniverses.Contains(_activityWatcher.Data.UniverseId)) {
            App.Settings.Prop.WindowControlAllowedUniverses.Add(_activityWatcher.Data.UniverseId);
            App.Settings.Save();

            if (_activityWatcher.watcher.WindowController != null)  {
                _activityWatcher.watcher.WindowController.updateExposedPerms();
            }
        }
        App.Logger.WriteLine("AskPerms", "bro visited his yes");
        Close();
    }
}