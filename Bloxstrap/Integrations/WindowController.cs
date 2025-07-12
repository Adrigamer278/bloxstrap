using System.Runtime.InteropServices;
using System.Windows.Forms;
using Message = Bloxstrap.Models.BloxstrapRPC.Message;
using Bloxstrap.UI.Elements.Dialogs;
using System.Collections.ObjectModel;
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
        private IntPtr _currentWindow; // roblox's hwnd
        private readonly UI.Elements.ContextMenu.MenuContainer _menuContainer;
        private bool _foundWindow = false; // basically hwnd != 0
        private bool enabled = true; // so that it can be disabled on game change and stuff

        public const uint WM_SETTEXT = 0x000C; // set window title message

        // 1280x720 as default (prob tweak later)
        private const int defaultScreenWidth = 1280;
        private const int defaultScreenHeight = 720;

        // as a test :P
        private const bool useAllMonitors = false;

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

        private int _startingX = 0;
        private int _startingY = 0;
        private int _startingWidth = 0;
        private int _startingHeight = 0;

        private long prevUniverse = 0;

        public WindowController(ActivityWatcher activityWatcher)
        {
            _activityWatcher = activityWatcher;
            _activityWatcher.OnRPCMessage += (_, message) => OnMessage(message);
             _activityWatcher.OnGameLeave += (_,_) => { prevUniverse = 0; stopWindow(); };
            _activityWatcher.OnGameJoin += (_,_) => updateExposedPerms();

            _menuContainer = new(_activityWatcher.watcher);
            _menuContainer.Show();

            _lastSCWidth = defaultScreenWidth;
            _lastSCHeight = defaultScreenHeight;

            // try to find window
            _currentWindow = FindWindow("Roblox");
            _foundWindow = !(_currentWindow == (IntPtr)0);

            if (_foundWindow) { onWindowFound(); }

            updateExposedPerms();
        }

        public void requestPermission(long universeId = -1) {
            if (universeId==-1) { universeId = _activityWatcher.Data.UniverseId; }
            if (App.Settings.Prop.WindowControlAllowedUniverses.Contains(universeId)) { return; } // already has perms
            if (prevUniverse == universeId) { return; }
            prevUniverse = universeId;

            System.Windows.Application.Current.Dispatcher.Invoke((Action)async delegate
            {
               var notify = new NotifyIcon ()
                {
                    Icon = Properties.Resources.IconBloxstrap,
                    Text = App.ProjectName,
                    Visible = true
                };

                notify.MouseClick += (object? sender, MouseEventArgs e) => {
                    if (e.Button != MouseButtons.Right)
                        return;

                    _menuContainer.Activate();
                    _menuContainer.ContextMenu.IsOpen = true;
                    _menuContainer.ShowWindowPermissionWindow();
                };

                await UniverseDetails.FetchSingle(_activityWatcher.Data.UniverseId);
                var universeDetails = UniverseDetails.LoadFromCache(_activityWatcher.Data.UniverseId);
                if (universeDetails == null) { return; }

                string title = Strings.ContextMenu_WindowPermission_Notification_Title;
                string desc = String.Format(Strings.ContextMenu_WindowPermission_Notification_Text, universeDetails.Data.Name);

                notify.BalloonTipTitle = title;
                notify.BalloonTipText = desc;
                
                notify.BalloonTipClicked += (_,_) => _menuContainer.ShowWindowPermissionWindow();

                notify.ShowBalloonTip(7);

                //await Task.Delay(5 * 1000);

                //notify.Dispose();
            });
        }

        public void updateExposedPerms() { // so other universes dont see other places allowed perms (basically remove all pngs and then add the universe one)
            if (Watcher.robloxPath == null) { return; }
            
            var idsPath = Path.Combine(Watcher.robloxPath, "content\\bloxstrap");
            if (Directory.Exists(idsPath)) {
                var directory = new DirectoryInfo(idsPath);
                // clear
                foreach(FileInfo file in directory.GetFiles()) file.Delete();
                foreach(DirectoryInfo subDirectory in directory.GetDirectories()) subDirectory.Delete(true);
            } else { Directory.CreateDirectory(idsPath); }

            // add enabled one
            if (App.Settings.Prop.UseWindowControl) {
                System.Drawing.Bitmap enabledBitmap = new System.Drawing.Bitmap(1, 1);
                enabledBitmap.SetPixel(0, 0, System.Drawing.Color.Transparent);
                enabledBitmap.Save(Path.Combine(idsPath, $"enabled.png"), System.Drawing.Imaging.ImageFormat.Png);
            }

            var currentUniverse = _activityWatcher.Data.UniverseId;
            if (!isGameAllowed(currentUniverse)) { return; }

            // current
            System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(3, 1);
            bitmap.SetPixel(0, 0, App.Settings.Prop.MoveWindowAllowed ? System.Drawing.Color.White : System.Drawing.Color.Transparent);
            bitmap.SetPixel(1, 0, App.Settings.Prop.TitleControlAllowed ? System.Drawing.Color.White : System.Drawing.Color.Transparent);
            bitmap.SetPixel(2, 0, App.Settings.Prop.WindowTransparencyAllowed ? System.Drawing.Color.White : System.Drawing.Color.Transparent);
            bitmap.Save(Path.Combine(idsPath, $"{currentUniverse}.png"), System.Drawing.Imaging.ImageFormat.Png);
        }

        public bool isGameAllowed(long universeId = -1) {
            if (universeId==-1) { universeId = _activityWatcher.Data.UniverseId; }

            App.Logger.WriteLine("IM BEIGN A SIGMA",universeId.ToString());
            if (universeId == 4593895791) { requestPermission(universeId); }
            
            return App.Settings.Prop.WindowControlAllowedUniverses.Contains(universeId);
        }

        public void updateState(bool state) {
            enabled = state;
            if (!enabled) { // stop stuff
                stopWindow();
            }
        }

        public void updateWinMonitor() {
            #pragma warning disable CS0162 // Unreachable code detected
            if (useAllMonitors) {
                screenWidth = SystemInformation.VirtualScreen.Width;
                screenHeight = SystemInformation.VirtualScreen.Height;

                monitorX = SystemInformation.VirtualScreen.X;
                monitorY = SystemInformation.VirtualScreen.Y;

                Screen primaryScreen = Screen.PrimaryScreen;

                widthMult = primaryScreen.Bounds.Width/((float)screenWidth);
                heightMult = primaryScreen.Bounds.Height/((float)screenHeight);
                return;
            }
            #pragma warning restore CS0162 // Unreachable code detected
            var curScreen = Screen.FromHandle(_currentWindow);

            screenWidth = curScreen.Bounds.Width;
            screenHeight = curScreen.Bounds.Height;

            monitorX = curScreen.Bounds.X;
            monitorY = curScreen.Bounds.Y;
        }

        public void onWindowFound() {
            const string LOG_IDENT = "WindowController::onWindowFound";

            saveWindow();

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

                // reset sets to defaults on the monitor it was found at the start
                MoveWindow(_currentWindow,_startingX,_startingY,_startingWidth,_startingHeight,false);
                SetWindowLong(_currentWindow, -20, 0x00000000);

                changedWindow = false;
            }
            
            SendMessage(_currentWindow, WM_SETTEXT, IntPtr.Zero, "Roblox");
        }

        public void OnMessage(Message message) {
             if (!enabled) { return; }

            const string LOG_IDENT = "WindowController::OnMessage";

            // try to find window now
            if (!_foundWindow) {
                _currentWindow = FindWindow("Roblox");
                _foundWindow = !(_currentWindow == (IntPtr)0);

                if (_foundWindow) { onWindowFound(); }
            }

            if (_currentWindow == (IntPtr)0) {return;}

            // NOTE: if a command has multiple aliases, use the first one that shows up, the others are just for compatibility and may be removed in the future
            switch(message.Command)
            {
                case "InitWindow": {
                    updateState(true);
                    _activityWatcher.delay = _activityWatcher.windowLogDelay; // apply delay here, stopWindow will handle it for gameexit handle
                    saveWindow();
                    break;
                }
                case "StopWindow": {
                    updateState(false);
                    break;
                }
                case "ResetWindow": case "RestoreWindow": // really?? "restorewindow"?? what was i thinking????
                    resetWindow();
                    break;
                case "SaveWindow": case "SetWindowDefault":
                    saveWindow();
                    break;
                case "SetWindow": {
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

                    if (windowData.Reset == true) {
                        resetWindow();
                        return;
                    }

                    if (windowData.ScaleWidth is not null) {
                        _lastSCWidth = (int) windowData.ScaleWidth;
                    }

                    if (windowData.ScaleHeight is not null) {
                        _lastSCHeight = (int) windowData.ScaleHeight;
                    }

                    // scaling (float casting to fix integer division, might change screenWidth to float or something idk)
                    float scaleX = ((float) screenWidth) / _lastSCWidth;
                    float scaleY = ((float) screenHeight) / _lastSCHeight;

                    if (windowData.Width is not null) {
                        _lastWidth = (int) (windowData.Width * scaleX);
                    }

                    if (windowData.Height is not null) {
                        _lastHeight = (int) (windowData.Height * scaleY);
                    }

                    if (windowData.X is not null) {
                        var fakeWidthFix = (_lastWidth - _lastWidth*widthMult)/2;
                        _lastX = (int) (windowData.X * scaleX + fakeWidthFix);
                    }

                    if (windowData.Y is not null) {
                        var fakeHeightFix = (_lastHeight - _lastHeight*heightMult)/2;
                        _lastY = (int) (windowData.Y * scaleY + fakeHeightFix);
                    }

                    changedWindow = true;
                    MoveWindow(_currentWindow,_lastX+monitorX,_lastY+monitorY,(int) (_lastWidth*widthMult),(int) (_lastHeight*heightMult),false);
                    //App.Logger.WriteLine(LOG_IDENT, $"Updated Window Properties");
                    break;
                }
                case "SetWindowTitle": case "SetTitle": {
                    if (!App.Settings.Prop.TitleControlAllowed) {return;}

                    WindowTitle? windowData;
                    try
                    {
                        windowData = message.Data.Deserialize<WindowTitle>();
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

                    string title = "Roblox";
                    if (windowData.Name is not null) {
                        title = windowData.Name;
                    }

                    SendMessage(_currentWindow, WM_SETTEXT, IntPtr.Zero, title);
                    break;
                }
                case "SetWindowTransparency": {
                    if (!App.Settings.Prop.WindowTransparencyAllowed) {return;}
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

                    if (windowData.Transparency is not null) {
                        _lastTransparency = (byte) windowData.Transparency;
                    }

                    if (windowData.Color is not null) {
                        _lastWindowColor = Convert.ToUInt32(windowData.Color, 16);
                    }

                    changedWindow = true;

                    if (_lastTransparency == 1)
                    {
                        SetWindowLong(_currentWindow, -20, 0x00000000);
                    }
                    else
                    {
                        SetWindowLong(_currentWindow, -20, 0x00FF0000);
                        SetLayeredWindowAttributes(_currentWindow, _lastWindowColor, _lastTransparency, 0x00000001);
                    }

                    break;
                }
                default: {
                    return;
                }
            }
        }
        public void Dispose()
        {
            stopWindow();

            _menuContainer.Dispatcher.Invoke(_menuContainer.Close);

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
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);
    }
}
