using System.Runtime.InteropServices;
using System.Windows.Forms;
using Message = Bloxstrap.Models.BloxstrapRPC.Message;
public struct WindowRect
{
    public int Left { get; set; }
    public int Top { get; set; }
    public int Right { get; set; }
    public int Bottom { get; set; }
}

namespace Bloxstrap.Integrations
{
    public class WindowController : IDisposable
    {
        private readonly List<string> ALLOWED_GLOBALLY = new() // Commands that work everywhere even if the universe doesn't have permission
        {
            "SetWindowTitle",
        };

        private readonly List<string> ALLOWED_NONSTARTED = new() // Commands that work when window is started
        {
            "StartWindow",
            "RequestWindowPermission",
            "SetWindowTitle",
            "SetWallpaper",
            "CacheWallpaper"
        };

        private readonly ActivityWatcher _activityWatcher; // activity watcher
        private UI.Elements.ContextMenu.MenuContainer? _menuContainer;
        private IntPtr _currentWindow; // roblox's hwnd
        private long _windowLong = 0x00000000; // roblox's default windowlong
        private bool _foundWindow = false; // basically hwnd != 0
        private bool enabled = false; // its true if legacy mode is enabled or if startwindow is called
        private bool _changedWallpaper = false;
        private bool? userHasDesktopIconsVisible = null; // Does the user has the desktop icons visible? (WINDOWS SETTING)
        private List<string> filesToClear = new();

        public const uint WM_SETTEXT = 0x000C; // set window title message
        public const int GWL_EXSTYLE = -20; // set new extended window style
        public const long WS_EX_LAYERED = 0x00080000L; // window is a layered window
        public const long WS_EX_TRANSPARENT = 0x00000020L; // window is considered fully transparent
        public const uint LWA_COLORKEY = 0x00000001; // window uses chroma key for transparency
        public const uint LWA_ALPHA = 0x00000002; // window uses alpha for transparency
        public const long WS_BORDER = 0x00800000L;       // standard window border
        public const long WS_CAPTION = 0x00C00000L;     // title bar + border
        private const int GWL_STYLE = -16;
        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;
        private const int SW_SHOWNA = 8;
        private const long WS_THICKFRAME = 0x00040000L; // resizable frame
        private const long WS_SYSMENU = 0x00080000L;
        private const long WS_MINIMIZEBOX = 0x00020000L;
        private const long WS_MAXIMIZEBOX = 0x00010000L;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_FRAMECHANGED = 0x0020;
        private const uint SWP_NOSENDCHANGING = 0x0400;
        private const uint SWP_ASYNCWINDOWPOS = 0x4000;

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        public void SetBorderless(bool borderless)
        {
            if (_currentWindow == IntPtr.Zero) return;

            long style = GetWindowLong(_currentWindow, GWL_STYLE);

            if (borderless)
            {
                style &= ~WS_CAPTION;
                style &= ~WS_THICKFRAME;
                style &= ~WS_BORDER;
                style &= ~WS_SYSMENU;
                style &= ~WS_MINIMIZEBOX;
                style &= ~WS_MAXIMIZEBOX;
            }
            else
            {
                style |= WS_CAPTION;
                style |= WS_THICKFRAME;
                style |= WS_SYSMENU;
                style |= WS_MINIMIZEBOX;
                style |= WS_MAXIMIZEBOX;
            }

            SetWindowLong(_currentWindow, GWL_STYLE, style);
            SetWindowPos(_currentWindow, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_ASYNCWINDOWPOS | SWP_NOSENDCHANGING | SWP_FRAMECHANGED);
        }

        // 1280x720 as default (prob tweak later)
        private const int defaultScreenWidth = 1280;
        private const int defaultScreenHeight = 720;

        // extra monitors offsets
        public int monitorX = 0;
        public int monitorY = 0;

        // size mults (not for ingame use, but for allMonitors)
        public float widthMult = 1;
        public float heightMult = 1;

        // screen size
        private int screenWidth = 0;
        private int screenHeight = 0;

        private bool changedWindow = false;

        // cache last data to prevent bloating
        private int _lastX = 0;
        private int _lastY = 0;
        private int _lastWidth = 0;
        private int _lastHeight = 0;
        private int _lastSCWidth = 0;
        private int _lastSCHeight = 0;
        private byte _lastTransparency = 1;
        private uint _lastWindowAlphaColor = 0x000000;
        private uint _lastWindowCaptionColor = 0x000000;
        private uint _lastWindowBorderColor = 0x000000;
        private uint _lastTransparencyMode = 0x00000001;
        private ulong _lastWallpaperSet = 0;

        private int _startingX = 0;
        private int _startingY = 0;
        private int _startingWidth = 0;
        private int _startingHeight = 0;

        private bool curUniverseAllowed = false;
        private long lastUniverseThatRequested = 0;

        private Theme appTheme = Theme.Default;
        private const int S_OK = 0;

        public WindowController(ActivityWatcher activityWatcher)
        {
            _activityWatcher = activityWatcher;
            _activityWatcher.OnRPCMessage += (_, message) => OnMessage(message);
            _activityWatcher.OnGameLeave += (_, _) => { 
                lastUniverseThatRequested = 0;
                stopWindow();
                clearFileCache();
            };
            _activityWatcher.OnGameJoin += (_, _) => updateExposedPerms();

            _lastSCWidth = defaultScreenWidth;
            _lastSCHeight = defaultScreenHeight;

            // try to find window
            _currentWindow = FindWindow();
            _foundWindow = !(_currentWindow == IntPtr.Zero);

            if (_foundWindow) { onWindowFound(); }

            updateExposedPerms();
        }

        public void requestPermission(long universeId = -1)
        {
            if (universeId == -1) { universeId = _activityWatcher.Data.UniverseId; }
            if (App.Settings.Prop.WindowAllowedUniverses.Contains(universeId)) { return; } // already has perms
            if (App.Settings.Prop.WindowBlacklistedUniverses.Contains(universeId)) { return; } // already has been denied perms
            if (lastUniverseThatRequested == universeId) { return; }
            lastUniverseThatRequested = universeId;

            if (_menuContainer == null)
                _menuContainer = _activityWatcher.watcher._notifyIcon?._menuContainer;

            if (_menuContainer != null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(delegate
                {
                    _menuContainer.ShowWindowPermissionWindow();
                });
            }

        }

        public void clearFileCache()
        {
            foreach (string filePath in filesToClear)
                if (File.Exists(filePath))
                    File.Delete(filePath);

            filesToClear.Clear();
        }

        public void updateExposedPerms()
        { // so other universes dont see other places allowed perms (basically remove all pngs and then add the universe one)
            if (Watcher.robloxPath == null) { return; }

            var idsPath = Path.Combine(Watcher.robloxPath, "content\\bloxstrap");
            if (Directory.Exists(idsPath))
            {
                var directory = new DirectoryInfo(idsPath);
                // clear
                foreach (FileInfo file in directory.GetFiles()) if (file.Name != "enabled.png") file.Delete();
            }
            else { Directory.CreateDirectory(idsPath); }

            var currentUniverse = _activityWatcher.Data.UniverseId;

            curUniverseAllowed = App.Settings.Prop.WindowAllowAll || isGameAllowed(currentUniverse);
            if (!curUniverseAllowed) { return; }

            // current
            System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(3, 1);
            bitmap.SetPixel(0, 0, App.Settings.Prop.MoveWindowAllowed ? System.Drawing.Color.White : System.Drawing.Color.Transparent);
            bitmap.SetPixel(1, 0, App.Settings.Prop.TitleControlAllowed ? System.Drawing.Color.White : System.Drawing.Color.Transparent);
            bitmap.SetPixel(2, 0, App.Settings.Prop.WindowTransparencyAllowed ? System.Drawing.Color.White : System.Drawing.Color.Transparent);
            bitmap.Save(Path.Combine(idsPath, $"{currentUniverse}.png"), System.Drawing.Imaging.ImageFormat.Png);
        }

        public bool isGameAllowed(long universeId = -1)
        {
            if (universeId == -1) { universeId = _activityWatcher.Data.UniverseId; }

            return App.Settings.Prop.WindowAllowedUniverses.Contains(universeId);
        }

        public void updateState(bool state)
        {
            enabled = state;
            if (!enabled)
            { // stop stuff
                stopWindow();
            }
        }

        public void updateWinMonitor()
        {
            if (App.Settings.Prop.WindowMonitorStyle == WindowMonitorStyle.All)
            {
                screenWidth = SystemInformation.VirtualScreen.Width;
                screenHeight = SystemInformation.VirtualScreen.Height;

                monitorX = SystemInformation.VirtualScreen.X;
                monitorY = SystemInformation.VirtualScreen.Y;

                Screen primaryScreen = Screen.PrimaryScreen;

                widthMult = primaryScreen.Bounds.Width / ((float)screenWidth);
                heightMult = primaryScreen.Bounds.Height / ((float)screenHeight);
                return;
            }

            var curScreen = Screen.FromHandle(_currentWindow);

            screenWidth = curScreen.Bounds.Width;
            screenHeight = curScreen.Bounds.Height;

            monitorX = curScreen.Bounds.X;
            monitorY = curScreen.Bounds.Y;
        }

        public void onWindowFound()
        {
            const string LOG_IDENT = "WindowController::onWindowFound";

            saveWindow();

            _windowLong = GetWindowLong(_currentWindow, GWL_EXSTYLE);

            App.Logger.WriteLine(LOG_IDENT, $"Monitor X:{monitorX} Y:{monitorY} W:{screenWidth} H:{screenHeight}");
            App.Logger.WriteLine(LOG_IDENT, $"Window X:{_lastX} Y:{_lastY} W:{_lastWidth} H:{_lastHeight}");

            appTheme = ThemeEx.GetFinal(App.Settings.Prop.Theme);
            if (App.Settings.Prop.CanGameChangeColor && appTheme == Theme.Dark)
            {
                DisableWindowDarkMode();
                _lastWindowCaptionColor = Convert.ToUInt32("1F1F1F", 16);
                DwmSetWindowAttribute(_currentWindow, 35, ref _lastWindowCaptionColor, sizeof(int));
            }
        }

        public void stopWindow()
        {
            _activityWatcher.delay = 250; // reset delay
            resetWindow();
        }

        // not recommended to be used as a save point for in-game movement, just as a save point between manipulation start and end
        public void saveWindow()
        {
            WindowRect winRect = new WindowRect();
            GetWindowRect(_currentWindow, ref winRect);

            // these positions are in virtualscreen space (returns pos in whole screen not in the monitor they are in) 
            _lastX = winRect.Left;
            _lastY = winRect.Top;
            _lastWidth = winRect.Right - winRect.Left;
            _lastHeight = winRect.Bottom - winRect.Top;

            _startingX = _lastX;
            _startingY = _lastY;
            _startingWidth = _lastWidth;
            _startingHeight = _lastHeight;

            updateWinMonitor();
        }

        public void resetWindow()
        {
            if (changedWindow)
            {
                _lastX = _startingX;
                _lastY = _startingY;
                _lastWidth = _startingWidth;
                _lastHeight = _startingHeight;

                _lastTransparency = 1;
                _lastWindowAlphaColor = 0x000000;
                _lastTransparencyMode = LWA_COLORKEY;

                // reset sets to defaults on the monitor it was found at the start
                MoveWindow(_startingX,_startingY,_startingWidth,_startingHeight);
                SetWindowLong(_currentWindow, GWL_EXSTYLE, _windowLong);
                SetBorderless(false);

                changedWindow = false;
            }

            SendMessage(_currentWindow, WM_SETTEXT, IntPtr.Zero, "Roblox");

            ResetDesktopVisibility();
            ResetWallpaper();

            //reset window color
            if (App.Settings.Prop.CanGameChangeColor)
            {
                DisableWindowDarkMode();
                _lastWindowCaptionColor = Convert.ToUInt32(appTheme == Theme.Dark ? "1F1F1F" : "FFFFFF", 16);
                DwmSetWindowAttribute(_currentWindow, 35, ref _lastWindowCaptionColor, sizeof(int));

                _lastWindowBorderColor = Convert.ToUInt32("1F1F1F", 16);
                DwmSetWindowAttribute(_currentWindow, 34, ref _lastWindowBorderColor, sizeof(int));
            }
        }

        void DisableWindowDarkMode()
        {
            uint disableDarkMode = 0;
            int cbAttribute = Marshal.SizeOf<int>();
            if (S_OK != DwmSetWindowAttribute(_currentWindow, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref disableDarkMode, cbAttribute))
            {
                DwmSetWindowAttribute(_currentWindow, DWMWA_USE_IMMERSIVE_DARK_MODE, ref disableDarkMode, cbAttribute);
            }
            UpdateWindow(_currentWindow);
        }

        public void OnMessage(Message message)
        {
            const string LOG_IDENT = "WindowController::OnMessage";

            // try to find window now
            if (!_foundWindow)
            {
                _currentWindow = FindWindow();
                _foundWindow = !(_currentWindow == IntPtr.Zero);

                if (_foundWindow) { onWindowFound(); }
            }

            if (_currentWindow == IntPtr.Zero) { return; }

            if (!curUniverseAllowed && (message.Command != "RequestWindowPermission" || lastUniverseThatRequested == _activityWatcher.Data.UniverseId) && !ALLOWED_GLOBALLY.Contains(message.Command)) {
                App.Logger.WriteLine(LOG_IDENT,"Dropping command:" + message.Command);
                return;
            }
            
            // to avoid people saving the windows position or size to another place when startwindow is called later
            if (!enabled && !ALLOWED_NONSTARTED.Contains(message.Command)) { 
                App.Logger.WriteLine(LOG_IDENT,"Dropping command:" + message.Command);
                return;
            }

            // NOTE: if a command has multiple aliases, use the first one that shows up, the others are just for compatibility and may be removed in the future
            switch (message.Command)
            {
                case "RequestWindowPermission":
                    {
                        requestPermission();
                        break;
                    }
                case "StartWindow":
                    {
                        if (enabled) { return; }

                        updateState(true);
                        _activityWatcher.delay = _activityWatcher.windowLogDelay; // apply delay here, stopWindow will handle it for gameexit handle
                        saveWindow();
                        break;
                    }
                case "StopWindow":
                    {
                        if (!enabled) { return; }

                        updateState(false);
                        break;
                    }
                case "ResetWindow":
                    _lastX = _startingX;
                    _lastY = _startingY;
                    _lastWidth = _startingWidth;
                    _lastHeight = _startingHeight;

                    MoveWindow(_startingX, _startingY, _startingWidth, _startingHeight);
                    break;
                /*case "SaveWindow":
                case "SetWindowDefault": // just like RestoreWindow, this one is getting removed soon
                    saveWindow();
                    break;*/
                case "SetWindow":
                    {
                        if (!App.Settings.Prop.MoveWindowAllowed) { break; }

                        WindowMessage? windowData = Deserialize<WindowMessage>(message);

                        if (windowData is null)
                        {
                            App.Logger.WriteLine(LOG_IDENT, "Failed to parse message! (JSON deserialization returned null)");
                            return;
                        }

                        if (windowData.Reset == true)
                        {
                            resetWindow();
                            return;
                        }

                        if (windowData.ScaleWidth != null)
                            _lastSCWidth = (int)windowData.ScaleWidth;

                        if (windowData.ScaleHeight != null)
                            _lastSCHeight = (int)windowData.ScaleHeight;

                        // scaling (float casting to fix integer division, might change screenWidth to float or something idk)
                        float scaleX = ((float)screenWidth) / _lastSCWidth;
                        float scaleY = ((float)screenHeight) / _lastSCHeight;

                        if (windowData.Width != null)
                            _lastWidth = (int)(windowData.Width * scaleX);

                        if (windowData.Height != null)
                            _lastHeight = (int)(windowData.Height * scaleY);

                        if (windowData.X != null)
                        {
                            var fakeWidthFix = (_lastWidth - _lastWidth * widthMult) / 2;
                            _lastX = (int)(windowData.X * scaleX + fakeWidthFix);
                        }

                        if (windowData.Y != null)
                        {
                            var fakeHeightFix = (_lastHeight - _lastHeight * heightMult) / 2;
                            _lastY = (int)(windowData.Y * scaleY + fakeHeightFix);
                        }

                        changedWindow = true;
                        MoveWindow(_lastX + monitorX, _lastY + monitorY, (int)(_lastWidth * widthMult), (int)(_lastHeight * heightMult));
                        //App.Logger.WriteLine(LOG_IDENT, $"Updated Window Properties");
                        break;
                    }
                case "SetWindowTitle":
                    {
                        if (!App.Settings.Prop.TitleControlAllowed) { return; }

                        string? title = Deserialize<string>(message);

                        if (title == null)
                            title = "Roblox";

                        SendMessage(_currentWindow, WM_SETTEXT, IntPtr.Zero, title);
                        break;
                    }
                case "SetWindowTransparency":
                    {
                        if (!App.Settings.Prop.WindowTransparencyAllowed) { return; }
                        WindowTransparency? windowData = Deserialize<WindowTransparency>(message);

                        if (windowData is null)
                        {
                            App.Logger.WriteLine(LOG_IDENT, "Failed to parse message! (JSON deserialization returned null)");
                            return;
                        }

                        if (windowData.Transparency != null)
                            _lastTransparency = (byte)(windowData.Transparency * 255);

                        if (windowData.Color != null)
                            _lastWindowAlphaColor = Convert.ToUInt32(windowData.Color, 16);

                        if (windowData.UseAlpha != null)
                            _lastTransparencyMode = (windowData.UseAlpha == true) ? LWA_ALPHA : LWA_COLORKEY;

                        changedWindow = true;

                        if (_lastTransparency == 255)
                            SetWindowLong(_currentWindow, GWL_EXSTYLE, _windowLong);
                        else
                        {
                            SetWindowLong(_currentWindow, GWL_EXSTYLE, (_windowLong | WS_EX_LAYERED) & ~WS_EX_TRANSPARENT);
                            SetLayeredWindowAttributes(_currentWindow, _lastWindowAlphaColor, _lastTransparency, _lastTransparencyMode);
                        }

                        break;
                    }
                case "SetWindowBorderless":
                    {
                        if (!App.Settings.Prop.MoveWindowAllowed) { break; }
                        bool? isBorderless = Deserialize<bool>(message);

                        if (isBorderless is null)
                        {
                            App.Logger.WriteLine(LOG_IDENT, "Failed to parse message! (JSON deserialization returned null)");
                            return;
                        }

                        SetBorderless(isBorderless ?? false);
                        changedWindow = true;

                        break;
                    }
                case "CacheWallpaper":
                    {
                        if (!App.Settings.Prop.WallpaperControlEnabled) { break; }

                        ulong? wpAssetId = Deserialize<ulong>(message);

                        if (wpAssetId == null || wpAssetId <= 0)
                        {
                            App.Logger.WriteLine(LOG_IDENT, "Wallpaper asset id is null or not a valid number");
                            return;
                        } 

                        try
                        {
                            RobloxAPI.getAsset((ulong)wpAssetId).ContinueWith((task) =>
                                {
                                    if (task.IsCompletedSuccessfully)
                                    {
                                        if (!filesToClear.Contains(task.Result))
                                            filesToClear.Add(task.Result);
                                    }
                                    else
                                    {
                                        App.Logger.WriteLine(LOG_IDENT, $"Failed to get asset for wallpaper: {task.Exception?.Message}");
                                    }
                                });;
                        } catch (Exception) { }
                        break;
                    }
                case "SetWallpaper":
                    {
                        if (!App.Settings.Prop.WallpaperControlEnabled) { break; }

                        WallpaperMessage? wallpaperData = Deserialize<WallpaperMessage>(message);

                        if (wallpaperData is null)
                        {
                            App.Logger.WriteLine(LOG_IDENT, "WallpaperData returned null or is not a valid format");
                            return;
                        }

                        if (wallpaperData.AssetId == null || wallpaperData.AssetId <= 0 || wallpaperData.Reset == true) // ik it's unsigned but like let me alone bro
                        {
                            ResetWallpaper();
                            return;
                        }

                        _changedWallpaper = true;
                        _lastWallpaperSet = (ulong)wallpaperData.AssetId;

                        App.Logger.WriteLine(LOG_IDENT, $"Changing wallpaper to {wallpaperData.AssetId}");

                        try
                        {
                            RobloxAPI.getAsset(_lastWallpaperSet).ContinueWith((task) =>
                            {
                                if (task.IsCompletedSuccessfully)
                                {
                                    if (!filesToClear.Contains(task.Result))
                                        filesToClear.Add(task.Result);

                                    // cehck if we didn't take too long
                                    if (_changedWallpaper && _lastWallpaperSet == (ulong)wallpaperData.AssetId)
                                        WallpaperController.SetWallpaper(task.Result, wallpaperData.Style);
                                }
                                else
                                {
                                    App.Logger.WriteLine(LOG_IDENT, $"Failed to get asset for wallpaper: {task.Exception?.Message}");
                                }
                            });
                        } catch (Exception e)
                        {
                            App.Logger.WriteLine(LOG_IDENT, $"Failed to set wallpaper: {e.Message}");
                        }
                        break;
                    }
                case "SetDesktopVisibility":
                    {
                        if (!App.Settings.Prop.DesktopControlEnabled) { break; }
                        DesktopVisibilityMessage? desktopData = Deserialize<DesktopVisibilityMessage>(message);

                        if (desktopData is null)
                        {
                            App.Logger.WriteLine(LOG_IDENT, "Failed to parse message! (JSON deserialization returned null)");
                            return;
                        }

                        if (desktopData.Taskbar != null)
                            SetTaskbarVisibility(desktopData.Taskbar.Value);

                        if (desktopData.DesktopIcons != null)
                            SetDesktopIconsVisibility(desktopData.DesktopIcons.Value);

                        break;
                    }
                case "SendNotification":
                    {
                        WindowNotification? notifData = Deserialize<WindowNotification>(message);

                        if (notifData is null)
                        {
                            App.Logger.WriteLine(LOG_IDENT, "Failed to parse message! (JSON deserialization returned null)");
                            return;
                        }

                        _activityWatcher.watcher._notifyIcon?.ShowAlert(notifData.Title ?? "[[MISSING TITLE]]", notifData.Caption ?? "[[MISSING CAPTION]]", notifData.Duration ?? 5, null);
                        break;
                    }
                case "SetWindowColor":
                    {
                        if (!App.Settings.Prop.CanGameChangeColor) { return; }
                        WindowColor? windowData = Deserialize<WindowColor>(message);

                        if (windowData is null)
                        {
                            App.Logger.WriteLine(LOG_IDENT, "Failed to parse message! (JSON deserialization returned null)");
                            return;
                        }

                        if (windowData.Reset == true)
                        {
                            windowData.Caption = appTheme == Theme.Dark ? "1F1F1F" : "FFFFFF";
                            windowData.Border = "1F1F1F";
                            windowData.Reset = false;
                        }

                        DisableWindowDarkMode();

                        if (windowData.Caption is not null)
                        {
                            _lastWindowCaptionColor = Convert.ToUInt32(windowData.Caption, 16);
                            DwmSetWindowAttribute(_currentWindow, 35, ref _lastWindowCaptionColor, sizeof(int));
                        }

                        if (windowData.Border is not null)
                        {
                            _lastWindowBorderColor = Convert.ToUInt32(windowData.Border, 16);
                            DwmSetWindowAttribute(_currentWindow, 34, ref _lastWindowBorderColor, sizeof(int));
                        }

                        break;
                    }
                default:
                    {
                        return;
                    }
            }
        }

        private void SetTaskbarVisibility(bool visible)
        {
            IntPtr taskbar = FindWindow("Shell_TrayWnd", null);

            if (taskbar != IntPtr.Zero)
                ShowWindow(taskbar, visible ? SW_SHOW : SW_HIDE);

            IntPtr secondary = IntPtr.Zero;

            while ((secondary = FindWindowEx(IntPtr.Zero, secondary, "Shell_SecondaryTrayWnd", null)) != IntPtr.Zero)
            {
                ShowWindow(secondary, visible ? SW_SHOW : SW_HIDE);
            }
        }

        private void SetDesktopIconsVisibility(bool visible)
        {
            // If the user has the icons already disabled, ignore
            if (userHasDesktopIconsVisible == false) { return; }

            IntPtr desktopWnd;

            IntPtr progman = FindWindow("Progman", null);
            desktopWnd = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);

            if (desktopWnd == IntPtr.Zero)
            {
                IntPtr workerw = IntPtr.Zero;

                do
                {
                    workerw = FindWindowEx(IntPtr.Zero, workerw, "WorkerW", null);
                    desktopWnd = FindWindowEx(workerw, IntPtr.Zero, "SHELLDLL_DefView", null);
                }
                while (workerw != IntPtr.Zero && desktopWnd == IntPtr.Zero);
            }

            if (desktopWnd != IntPtr.Zero)
            {
                // icon stuff
                IntPtr listView = FindWindowEx(desktopWnd, IntPtr.Zero, "SysListView32", "FolderView");

                if (listView != IntPtr.Zero)
                {
                    if (userHasDesktopIconsVisible == null)
                        userHasDesktopIconsVisible = IsWindowVisible(listView);

                    if (userHasDesktopIconsVisible == true)
                    {
                        ShowWindow(listView, visible ? SW_SHOWNA : SW_HIDE);
                    }
                }
                    
            }
        }

        private void ResetDesktopVisibility()
        {
            SetTaskbarVisibility(true);
            SetDesktopIconsVisibility(true);
        }

        private void ResetWallpaper()
        {
            if (_changedWallpaper)
            {
                WallpaperController.ResetWallpaper();
                _changedWallpaper = false;
            }
        }
        public void Dispose()
        {
            stopWindow();

            if (Watcher.robloxPath != null)
            {
                var idsPath = Path.Combine(Watcher.robloxPath, "content\\bloxstrap");
                if (Directory.Exists(idsPath))
                    Directory.Delete(idsPath, true);
            }

            clearFileCache();

            GC.SuppressFinalize(this);
        }

        private IntPtr FindWindow(string title = "Roblox")
        {
            // In a try/catch block as getting a process that doesn't throws an exception for some reason
            try
            {
                //Check the activity watcher process id first
                Process? processById = Watcher.processId != null ? Process.GetProcessById((int)Watcher.processId) : null;
                if (processById != null)
                    return processById.MainWindowHandle;
            }
            catch { }

            //Check the window title as a fallback
            Process[] tempProcesses;
            tempProcesses = Process.GetProcesses();
            foreach (Process proc in tempProcesses)
                if (proc.MainWindowTitle == title)
                    return proc.MainWindowHandle;

            return IntPtr.Zero;
        }

        private static T? Deserialize<T>(Message message)
        {
            try
            {
                return message.Data.Deserialize<T>();
            }
            catch
            {
                return default;
            }
        }

        private int _lastSetX = -1;
        private int _lastSetY = -1;
        private int _lastSetWidth = -1;
        private int _lastSetHeight = -1;

        private void MoveWindow(int x, int y, int width, int height)
        {
            if (_currentWindow == IntPtr.Zero)
                return;

            bool posChanged = (x != _lastSetX || y != _lastSetY);
            bool sizeChanged = (width != _lastSetWidth || height != _lastSetHeight);

            if (!posChanged && !sizeChanged)
                return; //nothing to do vro

            _lastX = x;
            _lastY = y;
            _lastSetWidth = width;
            _lastSetHeight = height;

            uint uFlags = SWP_NOZORDER | SWP_ASYNCWINDOWPOS | SWP_NOSENDCHANGING;

            if (!posChanged)
                uFlags |= SWP_NOMOVE;
            if (!sizeChanged)
                uFlags |= SWP_NOSIZE;

            SetWindowPos(
                _currentWindow,
                IntPtr.Zero,
                posChanged ? x : 0,
                posChanged ? y : 0,
                sizeChanged ? width : 0,
                sizeChanged ? height : 0,
                uFlags
            );
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, string lParam);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hwnd, ref WindowRect rectangle);

        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, long dwNewLong);

        [DllImport("user32.dll")]
        static extern long GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hWnd, int dwAttribute, ref uint pvAttribute, int cbAttribute);

        [DllImport("user32.dll")]
        private static extern bool UpdateWindow(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr FindWindowEx(
            IntPtr parentHandle,
            IntPtr childAfter,
            string className,
            string? windowTitle
        );

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);
    }
}