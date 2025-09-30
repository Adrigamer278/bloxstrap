using System.Collections.ObjectModel;
using System.Windows.Input;

using Microsoft.Win32;

using CommunityToolkit.Mvvm.Input;
using System.Windows;

namespace Bloxstrap.UI.ViewModels.Settings
{
    public class IntegrationsViewModel : NotifyPropertyChangedViewModel
    {
        public ICommand AddIntegrationCommand => new RelayCommand(AddIntegration);

        public ICommand DeleteIntegrationCommand => new RelayCommand(DeleteIntegration);

        public ICommand BrowseIntegrationLocationCommand => new RelayCommand(BrowseIntegrationLocation);

        private void AddIntegration()
        {
            CustomIntegrations.Add(new CustomIntegration()
            {
                Name = Strings.Menu_Integrations_Custom_NewIntegration
            });

            SelectedCustomIntegrationIndex = CustomIntegrations.Count - 1;

            OnPropertyChanged(nameof(SelectedCustomIntegrationIndex));
            OnPropertyChanged(nameof(IsCustomIntegrationSelected));
        }

        private void DeleteIntegration()
        {
            if (SelectedCustomIntegration is null)
                return;

            CustomIntegrations.Remove(SelectedCustomIntegration);

            if (CustomIntegrations.Count > 0)
            {
                SelectedCustomIntegrationIndex = CustomIntegrations.Count - 1;
                OnPropertyChanged(nameof(SelectedCustomIntegrationIndex));
            }

            OnPropertyChanged(nameof(IsCustomIntegrationSelected));
        }

        private void BrowseIntegrationLocation()
        {
            if (SelectedCustomIntegration is null)
                return;

            var dialog = new OpenFileDialog
            {
                Filter = $"{Strings.Menu_AllFiles}|*.*"
            };

            if (dialog.ShowDialog() != true)
                return;

            SelectedCustomIntegration.Name = dialog.SafeFileName;
            SelectedCustomIntegration.Location = dialog.FileName;
            OnPropertyChanged(nameof(SelectedCustomIntegration));
        }

        public bool ActivityTrackingEnabled
        {
            get => App.Settings.Prop.EnableActivityTracking;
            set
            {
                App.Settings.Prop.EnableActivityTracking = value;

                if (!value)
                {
                    ShowServerDetailsEnabled = value;
                    DisableAppPatchEnabled = value;
                    DiscordActivityEnabled = value;
                    WindowControlEnabled = value;
                    DiscordActivityJoinEnabled = value;

                    OnPropertyChanged(nameof(ShowServerDetailsEnabled));
                    OnPropertyChanged(nameof(DisableAppPatchEnabled));
                    OnPropertyChanged(nameof(DiscordActivityEnabled));
                    OnPropertyChanged(nameof(WindowControlEnabled));
                    OnPropertyChanged(nameof(DiscordActivityJoinEnabled));
                }
            }
        }

        public bool ShowServerDetailsEnabled
        {
            get => App.Settings.Prop.ShowServerDetails;
            set => App.Settings.Prop.ShowServerDetails = value;
        }

        public bool DiscordActivityEnabled
        {
            get => App.Settings.Prop.UseDiscordRichPresence;
            set
            {
                App.Settings.Prop.UseDiscordRichPresence = value;

                if (!value)
                {
                    DiscordActivityJoinEnabled = value;
                    DiscordAccountOnProfile = value;
                    OnPropertyChanged(nameof(DiscordActivityJoinEnabled));
                    OnPropertyChanged(nameof(DiscordAccountOnProfile));
                }
            }
        }

        public bool WindowControlEnabled
        {
            get => App.Settings.Prop.UseWindowControl;
            set => App.Settings.Prop.UseWindowControl = value;
        }

        public bool MoveWindowControlEnabled
        {
            get => App.Settings.Prop.MoveWindowAllowed;
            set => App.Settings.Prop.MoveWindowAllowed = value;
        }

        public bool TitleControlEnabled
        {
            get => App.Settings.Prop.TitleControlAllowed;
            set => App.Settings.Prop.TitleControlAllowed = value;
        }

        public bool TransparencyControlEnabled
        {
            get => App.Settings.Prop.WindowTransparencyAllowed;
            set => App.Settings.Prop.WindowTransparencyAllowed = value;
        }

        public bool WindowAllowAllOption
        {
            get => App.Settings.Prop.WindowAllowAll;
            set => App.Settings.Prop.WindowAllowAll = value;
        }

        public int WindowReadFPSInterval
        {
            get => App.Settings.Prop.WindowReadFPS;
            set => App.Settings.Prop.WindowReadFPS = value;
        }

        public bool DiscordActivityJoinEnabled
        {
            get => !App.Settings.Prop.HideRPCButtons;
            set => App.Settings.Prop.HideRPCButtons = !value;
        }

        public bool DiscordAccountOnProfile
        {
            get => App.Settings.Prop.ShowAccountOnRichPresence;
            set => App.Settings.Prop.ShowAccountOnRichPresence = value;
        }

        public bool DisableAppPatchEnabled
        {
            get => App.Settings.Prop.UseDisableAppPatch;
            set => App.Settings.Prop.UseDisableAppPatch = value;
        }

        public bool MultiInstanceLaunchingEnabled
        {
            get => App.Settings.Prop.MultiInstanceLaunching;
            set => App.Settings.Prop.MultiInstanceLaunching = value;
        }

        public ObservableCollection<CustomIntegration> CustomIntegrations
        {
            get => App.Settings.Prop.CustomIntegrations;
            set => App.Settings.Prop.CustomIntegrations = value;
        }

        public CustomIntegration? SelectedCustomIntegration { get; set; }
        public int SelectedCustomIntegrationIndex { get; set; }
        public bool IsCustomIntegrationSelected => SelectedCustomIntegration is not null;

        // window stuff

        public IEnumerable<WindowMonitorStyle> WindowMonitorStyles { get; } = Enum.GetValues(typeof(WindowMonitorStyle)).Cast<WindowMonitorStyle>();

        public WindowMonitorStyle MonitorStyle
        {
            get => App.Settings.Prop.WindowMonitorStyle;
            set => App.Settings.Prop.WindowMonitorStyle = value;
        }
        
        // universe stuff
        public ICommand DeleteUniverseCommand => new RelayCommand(DeleteUniverse);
        public ICommand SwapDisplayedUniversesCommand => new RelayCommand(SwapDisplayedUniverses);

        private void DeleteUniverse()
        {
            if (SelectedUniverse is null)
                return;

            CurrentDisplayedUniverses.Remove((long)SelectedUniverse);

            if (CurrentDisplayedUniverses.Count > 0)
            {
                SelectedUniverseIndex = CurrentDisplayedUniverses.Count - 1;
                OnPropertyChanged(nameof(SelectedUniverseIndex));
            }

            OnPropertyChanged(nameof(IsUniverseSelected));
        }

        private void SwapDisplayedUniverses()
        {
            displayBlacklist = !displayBlacklist;
            SelectedUniverseIndex = 0;
            SelectedUniverse = CurrentDisplayedUniverses.Count > 0 ? CurrentDisplayedUniverses[SelectedUniverseIndex] : null;

            SelectedUniverseListName = displayBlacklist ? Strings.Menu_Integrations_WindowUniversesList_Blacklisted : Strings.Menu_Integrations_WindowUniversesList_Allowed;

            OnPropertyChanged(nameof(SelectedUniverseListName));
            OnPropertyChanged(nameof(SelectedUniverse));
            OnPropertyChanged(nameof(SelectedUniverseIndex));
            OnPropertyChanged(nameof(IsUniverseSelected));
            OnPropertyChanged(nameof(CurrentDisplayedUniverses));
        }

        public bool displayBlacklist = false;

        public string SelectedUniverseListName { get; set; } = Strings.Menu_Integrations_WindowUniversesList_Allowed;

        public ObservableCollection<long> CurrentDisplayedUniverses
        {
            get
            {
                return displayBlacklist ? WindowBlacklistedUniverses : WindowAllowedUniverses;
            }
            set
            {
                if (displayBlacklist)
                    WindowBlacklistedUniverses = value;
                else
                    WindowAllowedUniverses = value;
            }
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

        private UniverseDetails PlaceholderUniverseDetails = new()
        {
            Thumbnail = new()
            {
                ImageUrl = "Resources/Bloxstrap.ico" // bloxstrap logo lol
            },
            Data = new()
            {
                Name = "Loading...",
                Id = -1,
                Creator = new GameCreator()
                {
                    Name = ""
                }
            }
        };

        private UniverseDetails FailedUniverseDetails = new()
        {
            Thumbnail = new()
            {
                ImageUrl = "Resources/Bloxstrap.ico" // bloxstrap logo lol
            },
            Data = new() {
                Name = "❌ Couldn't get universe data",
                Id = -1,
                Creator = new GameCreator()
                {
                    Name = ""
                }
            }
        };

        public UniverseDetails? SelectedUniverseDetails { get; set; }

        private long? _selectedUniverse;
        public long? SelectedUniverse
        {
            get => _selectedUniverse;
            set
            {
                _selectedUniverse = value;

                if (value is null)
                    return;

                SelectedUniverseDetails = PlaceholderUniverseDetails;
                OnPropertyChanged(nameof(SelectedUniverseDetails));

                Task.Run(async () =>
                {
                    await UniverseDetails.FetchSingle((long)value);
                    if (value == _selectedUniverse)
                    {
                        SelectedUniverseDetails = UniverseDetails.LoadFromCache((long)value);
                    }
                    else
                    {
                        SelectedUniverseDetails = FailedUniverseDetails;
                    }

                    OnPropertyChanged(nameof(SelectedUniverseDetails));
                });
            }
        }
        public int SelectedUniverseIndex { get; set; }
        public bool IsUniverseSelected => _selectedUniverse is not null;
    }
}
