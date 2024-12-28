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

            IAppData AppData = App.LaunchSettings.RobloxLaunchMode != LaunchMode.Player ? new RobloxStudioData() : new RobloxPlayerData();
            string bloxstrapRobloxFolder = Path.Combine(AppData.Directory, "content\\bloxstrap");
            string imagePath = Path.Combine(bloxstrapRobloxFolder, _activityWatcher.Data.UniverseId.ToString() + ".png");
            System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(1, 1);
            bitmap.SetPixel(0, 0, System.Drawing.Color.Transparent);
            Directory.CreateDirectory(bloxstrapRobloxFolder);
            bitmap.Save(imagePath, System.Drawing.Imaging.ImageFormat.Png);
        }
        App.Logger.WriteLine("AskPerms", "bro said yes");
        Close();
    }
}