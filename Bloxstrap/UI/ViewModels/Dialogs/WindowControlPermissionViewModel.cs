using System.Windows;
using System.Windows.Input;
using Bloxstrap.Integrations;
using CommunityToolkit.Mvvm.Input;

namespace Bloxstrap.UI.ViewModels.Dialogs;

internal class WindowControlPermissionViewModel : NotifyPropertyChangedViewModel
{
    private readonly ActivityWatcher _activityWatcher;

    public List<ActivityData>? GameHistory { get; private set; }

    public GenericTriState LoadState { get; private set; } = GenericTriState.Unknown;

    public string Error { get; private set; } = String.Empty;

    public ICommand CloseWindowCommand => new RelayCommand(RequestClose);
        
    public EventHandler? RequestCloseEvent;

    public WindowControlPermissionViewModel(ActivityWatcher activityWatcher)
    {
        _activityWatcher = activityWatcher;

        LoadData();
    }

    private async void LoadData()
    {
        LoadState = GenericTriState.Unknown;
        OnPropertyChanged(nameof(LoadState));

        var activity = _activityWatcher.Data;
        if (activity.UniverseDetails is null)
        {
            try
            {
                await UniverseDetails.FetchSingle(activity.UniverseId);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("WindowControlPermissionViewModel", ex);
                Frontend.ShowMessageBox($"{Strings.ActivityWatcher_RichPresenceLoadFailed}\n\n{ex.Message}", MessageBoxImage.Warning);
                return;
            }

            activity.UniverseDetails = UniverseDetails.LoadFromCache(activity.UniverseId);
        }

        UniverseDetails? universe = activity.UniverseDetails!;

        List<ActivityData> thingy = new();

        ActivityData data = new ActivityData();
        data.UniverseDetails = universe;
        thingy.Add(data);

        GameHistory = new(thingy);

        OnPropertyChanged(nameof(GameHistory));

        LoadState = GenericTriState.Successful;
        OnPropertyChanged(nameof(LoadState));
    }

    private void RequestClose() => RequestCloseEvent?.Invoke(this, EventArgs.Empty);
}