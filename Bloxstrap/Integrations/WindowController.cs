using System.Runtime.InteropServices;
using System.Windows.Forms;
using Message = Bloxstrap.Models.BloxstrapRPC.Message;
public struct WindowRect {
   public int Left { get; set; }
   public int Top { get; set; }
   public int Right { get; set; }
   public int Bottom { get; set; }
}

namespace Bloxstrap.Integrations
{
    public class WindowController : IDisposable
    {
        private readonly ActivityWatcher _activityWatcher; // activity watcher
        private UI.Elements.ContextMenu.MenuContainer? _menuContainer;
        private IntPtr _currentWindow; // roblox's hwnd
        private long _windowLong = 0x00000000; // roblox's default windowlong
        private bool _foundWindow = false; // basically hwnd != 0
        private bool enabled = true; // its true if legacy mode is enabled or if startwindow is called

        public const uint WM_SETTEXT = 0x000C; // set window title message
        public const int GWL_EXSTYLE = -20; // set new extended window style
        public const long WS_EX_LAYERED = 0x00080000L; // window is a layered window
        public const long WS_EX_TRANSPARENT = 0x00000020L; // window is considered fully transparent
        public const uint LWA_COLORKEY = 0x00000001; // window uses chroma key for transparency
        public const uint LWA_ALPHA = 0x00000002; // window uses alpha for transparency

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
        private uint _lastWindowColor = 0x000000;
        private uint _lastTransparencyMode = 0x00000001;

        private int _startingX = 0;
        private int _startingY = 0;
        private int _startingWidth = 0;
        private int _startingHeight = 0;

        private bool curUniverseAllowed = false;
        private long prevUniverse = 0;

        public WindowController(ActivityWatcher activityWatcher)
        {
            _activityWatcher = activityWatcher;
            _activityWatcher.OnRPCMessage += (_, message) => OnMessage(message);
            _activityWatcher.OnGameLeave += (_,_) => { prevUniverse = 0; stopWindow(); };
            _activityWatcher.OnGameJoin += (_,_) => updateExposedPerms();

            _lastSCWidth = defaultScreenWidth;
            _lastSCHeight = defaultScreenHeight;

            // try to find window
            _currentWindow = FindWindow("Roblox");
            _foundWindow = !(_currentWindow == (IntPtr)0);

            if (_foundWindow) { onWindowFound(); }

            updateExposedPerms();
        }

        public void requestPermission(long universeId = -1) {
            if (universeId == -1) { universeId = _activityWatcher.Data.UniverseId; }
            if (App.Settings.Prop.WindowAllowedUniverses.Contains(universeId)) { return; } // already has perms
            if (App.Settings.Prop.WindowBlacklistedUniverses.Contains(universeId)) { return; } // already has been denied perms
            if (prevUniverse == universeId) { return; }
            prevUniverse = universeId;
            
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

        public void updateExposedPerms() { // so other universes dont see other places allowed perms (basically remove all pngs and then add the universe one)
            if (Watcher.robloxPath == null) { return; }
            
            var idsPath = Path.Combine(Watcher.robloxPath, "content\\bloxstrap");
            if (Directory.Exists(idsPath)) {
                var directory = new DirectoryInfo(idsPath);
                // clear
                foreach(FileInfo file in directory.GetFiles()) if (file.Name!="enabled.png") file.Delete();
            } else { Directory.CreateDirectory(idsPath); }
            
            var currentUniverse = _activityWatcher.Data.UniverseId;

            curUniverseAllowed = isGameAllowed(currentUniverse);
            if (!curUniverseAllowed) { return; }

            // current
            System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(3, 1);
            bitmap.SetPixel(0, 0, App.Settings.Prop.MoveWindowAllowed ? System.Drawing.Color.White : System.Drawing.Color.Transparent);
            bitmap.SetPixel(1, 0, App.Settings.Prop.TitleControlAllowed ? System.Drawing.Color.White : System.Drawing.Color.Transparent);
            bitmap.SetPixel(2, 0, App.Settings.Prop.WindowTransparencyAllowed ? System.Drawing.Color.White : System.Drawing.Color.Transparent);
            bitmap.Save(Path.Combine(idsPath, $"{currentUniverse}.png"), System.Drawing.Imaging.ImageFormat.Png);
        }

        public bool isGameAllowed(long universeId = -1) {
            if (universeId==-1) { universeId = _activityWatcher.Data.UniverseId; }

            return App.Settings.Prop.WindowAllowedUniverses.Contains(universeId);
        }

        public void updateState(bool state) {
            enabled = state;
            if (!enabled) { // stop stuff
                stopWindow();
            }
        }

        public void updateWinMonitor() {
            if (App.Settings.Prop.WindowMonitorStyle == WindowMonitorStyle.All) {
                screenWidth = SystemInformation.VirtualScreen.Width;
                screenHeight = SystemInformation.VirtualScreen.Height;

                monitorX = SystemInformation.VirtualScreen.X;
                monitorY = SystemInformation.VirtualScreen.Y;

                Screen primaryScreen = Screen.PrimaryScreen;

                widthMult = primaryScreen.Bounds.Width/((float)screenWidth);
                heightMult = primaryScreen.Bounds.Height/((float)screenHeight);
                return;
            }

            var curScreen = Screen.FromHandle(_currentWindow);

            screenWidth = curScreen.Bounds.Width;
            screenHeight = curScreen.Bounds.Height;

            monitorX = curScreen.Bounds.X;
            monitorY = curScreen.Bounds.Y;
        }

        public void onWindowFound() {
            const string LOG_IDENT = "WindowController::onWindowFound";

            saveWindow();

            _windowLong = GetWindowLong(_currentWindow, GWL_EXSTYLE);

            App.Logger.WriteLine(LOG_IDENT, $"Monitor X:{monitorX} Y:{monitorY} W:{screenWidth} H:{screenHeight}");
            App.Logger.WriteLine(LOG_IDENT, $"Window X:{_lastX} Y:{_lastY} W:{_lastWidth} H:{_lastHeight}");
        }

        public void stopWindow() {
            _activityWatcher.delay = 250; // reset delay
            resetWindow();
        }

        // not recommended to be used as a save point for in-game movement, just as a save point between manipulation start and end
        public void saveWindow() {
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

        public void resetWindow() {
            if (changedWindow) {
                _lastX = _startingX;
                _lastY = _startingY;
                _lastWidth = _startingWidth;
                _lastHeight = _startingHeight;

                _lastTransparency = 1;
                _lastWindowColor = 0x000000;
                _lastTransparencyMode = LWA_COLORKEY;

                // reset sets to defaults on the monitor it was found at the start
                MoveWindow(_currentWindow,_startingX,_startingY,_startingWidth,_startingHeight,false);
                SetWindowLong(_currentWindow, GWL_EXSTYLE, _windowLong);

                changedWindow = false;
            }
            
            SendMessage(_currentWindow, WM_SETTEXT, IntPtr.Zero, "Roblox");
        }

        public void OnMessage(Message message) {
            const string LOG_IDENT = "WindowController::OnMessage";

            // try to find window now
            if (!_foundWindow) {
                _currentWindow = FindWindow("Roblox");
                _foundWindow = !(_currentWindow == (IntPtr)0);

                if (_foundWindow) { onWindowFound(); }
            }

            if (_currentWindow == (IntPtr)0) {return;}

            if (!curUniverseAllowed && (message.Command != "RequestWindowPermission" || prevUniverse == _activityWatcher.Data.UniverseId) && message.Command != "SetWindowTitle") { return; }
            
            // to avoid people saving the windows position or size to another place when startwindow is called later
            if (!enabled && message.Command != "RequestWindowPermission" && message.Command != "SetWindowTitle" && message.Command != "StartWindow") { return; }
            
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
                case "RestoreWindow": // really?? "restorewindow"?? what was i thinking????
                    _lastX = _startingX;
                    _lastY = _startingY;
                    _lastWidth = _startingWidth;
                    _lastHeight = _startingHeight;

                    MoveWindow(_currentWindow, _startingX, _startingY, _startingWidth, _startingHeight, false);
                    break;
                /*case "SaveWindow":
                case "SetWindowDefault": // just like RestoreWindow, this one is getting removed soon
                    saveWindow();
                    break;*/
                case "SetWindow":
                    {
                        if (!App.Settings.Prop.MoveWindowAllowed) { break; }

                        WindowMessage? windowData;

                        try
                        {
                            windowData = message.Data.Deserialize<WindowMessage>();
                        }
                        catch (Exception)
                        {
                            App.Logger.WriteLine(LOG_IDENT, "Failed to parse message! (JSON deserialization threw an exception)");
                            return;
                        }

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
                        {
                            _lastSCWidth = (int)windowData.ScaleWidth;
                        }

                        if (windowData.ScaleHeight != null)
                        {
                            _lastSCHeight = (int)windowData.ScaleHeight;
                        }

                        // scaling (float casting to fix integer division, might change screenWidth to float or something idk)
                        float scaleX = ((float)screenWidth) / _lastSCWidth;
                        float scaleY = ((float)screenHeight) / _lastSCHeight;

                        if (windowData.Width != null)
                        {
                            _lastWidth = (int)(windowData.Width * scaleX);
                        }

                        if (windowData.Height != null)
                        {
                            _lastHeight = (int)(windowData.Height * scaleY);
                        }

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
                        MoveWindow(_currentWindow, _lastX + monitorX, _lastY + monitorY, (int)(_lastWidth * widthMult), (int)(_lastHeight * heightMult), false);
                        //App.Logger.WriteLine(LOG_IDENT, $"Updated Window Properties");
                        break;
                    }
                case "SetWindowTitle":
                    {
                        if (!App.Settings.Prop.TitleControlAllowed) { return; }

                        string? title;
                        
                        try
                        {
                            title = message.Data.Deserialize<string>();
                        }
                        catch (Exception)
                        {
                            App.Logger.WriteLine(LOG_IDENT, "Failed to parse message! (Not a valid string)");
                            return;
                        }

                        if (title == null)
                            title = "Roblox";

                        SendMessage(_currentWindow, WM_SETTEXT, IntPtr.Zero, title);
                        break;
                    }
                case "SetWindowTransparency":
                    {
                        if (!App.Settings.Prop.WindowTransparencyAllowed) { return; }
                        WindowTransparency? windowData;

                        try
                        {
                            windowData = message.Data.Deserialize<WindowTransparency>();
                        }
                        catch (Exception)
                        {
                            App.Logger.WriteLine(LOG_IDENT, "Failed to parse message! (JSON deserialization threw an exception)");
                            return;
                        }

                        if (windowData is null)
                        {
                            App.Logger.WriteLine(LOG_IDENT, "Failed to parse message! (JSON deserialization returned null)");
                            return;
                        }

                        if (windowData.Transparency != null)
                        {
                            _lastTransparency = (byte)(windowData.Transparency * 255);
                        }

                        if (windowData.Color != null)
                        {
                            _lastWindowColor = Convert.ToUInt32(windowData.Color, 16);
                        }

                        if (windowData.UseAlpha != null)
                        {
                            _lastTransparencyMode = (windowData.UseAlpha == true) ? LWA_COLORKEY : LWA_COLORKEY;
                        }

                        changedWindow = true;

                        if (_lastTransparency == 255)
                        {
                            SetWindowLong(_currentWindow, GWL_EXSTYLE, _windowLong);
                        }
                        else
                        {
                            SetWindowLong(_currentWindow, GWL_EXSTYLE, (_windowLong | WS_EX_LAYERED) & ~WS_EX_TRANSPARENT);
                            SetLayeredWindowAttributes(_currentWindow, _lastWindowColor, _lastTransparency, _lastTransparencyMode);
                        }

                        break;
                    }
                default:
                    {
                        return;
                    }
            }
        }
        public void Dispose()
        {
            stopWindow();

            _menuContainer?.Dispatcher.Invoke(_menuContainer.Close);

            GC.SuppressFinalize(this);
        }

        private IntPtr FindWindow(string title)
        {
            Process[] tempProcesses;
            tempProcesses = Process.GetProcesses();
            foreach (Process proc in tempProcesses)
            {
                if (proc.MainWindowTitle == title)
                {
                    return proc.MainWindowHandle;
                }
            }
            return (IntPtr)0;
        }

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

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
    }
}
