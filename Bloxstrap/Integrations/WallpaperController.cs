using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace Bloxstrap.Integrations;

public static class WallpaperController
{
    private const int SPI_SETDESKWALLPAPER = 20;
    private const int SPI_GETDESKWALLPAPER = 0x0073;
    private const int SPIF_UPDATEINIFILE = 0x01;
    private const int SPIF_SENDCHANGE = 0x02;
    private static string? _originalWallpaper;
    private static bool _wallpaperApps = false;
    private static readonly List<string> _closedWallpaperApps = new();

    // String array of known apps (currently just Wallpaper Engine and Lively Wallpaper, as these are the main 2 everyone uses I believe)
    private static readonly string[] WallpaperProcesses =
    {
        "wallpaper32",
        "wallpaper64",
        "Lively",
        "LivelyUI",
        "Livelywpf",
    };

    private static readonly List<string> VALID_STYLES = new()
    {
        "fill",
        "fit",
        "stretch",
        "tile",
        "center",
        "span",
    };

    public static void SetWallpaper(string wallpaperPath, string? style)
    {
        const string LOG_IDENT = "WallpaperController::SetWallpaper";
        try
        {
            CloseWallpaperApps();
            ApplyWallpaper(wallpaperPath, style ?? "Fill");
        }
        catch (Exception ex)
        {
            App.Logger.WriteLine(
                LOG_IDENT,
                $"Failed to set wallpaper: {ex}"
            );
            RestoreWallpaperApps();
        }
    }

    public static void ResetWallpaper()
    {
        const string LOG_IDENT = "WallpaperController::ResetWallpaper";
        try
        {
            if (!string.IsNullOrEmpty(_originalWallpaper))
                ApplyWallpaper(_originalWallpaper, "Fill");

            RestoreWallpaperApps();

            _originalWallpaper = null;
        } catch (Exception ex)
        {
            App.Logger.WriteLine(
                LOG_IDENT,
                $"Failed to reset wallpaper: {ex}"
            );
        }
    }

    private static void ApplyWallpaper(string path, string style = "fill")
    {
        const string LOG_IDENT = "WallpaperController::ApplyWallpaper";

        if (string.IsNullOrEmpty(_originalWallpaper))
        {
            _originalWallpaper = GetCurrentWallpaper();
            if (string.IsNullOrEmpty(_originalWallpaper))
            {
                App.Logger.WriteLine(
                    LOG_IDENT,
                    "Failed to get current wallpaper, aborting change"
                );

                return;
            }
        }

        if (!VALID_STYLES.Contains(style.ToLower()))
            style = "Fill";

        App.Logger.WriteLine(
            LOG_IDENT,
            $"Applying wallpaper: {path} | style-{style}"
        );

        SetWallpaperStyle(style);

        bool result = SystemParametersInfo(
            SPI_SETDESKWALLPAPER,
            0,
            path,
            SPIF_UPDATEINIFILE | SPIF_SENDCHANGE
        );

        if (!result)
        {
            App.Logger.WriteLine(
                LOG_IDENT,
                $"SystemParametersInfo failed: {Marshal.GetLastWin32Error()} | path={path}"
            );
        }
    }

    private static string GetCurrentWallpaper()
    {
        const int MAX_PATH = 260;

        var buffer = new StringBuilder(MAX_PATH);

        SystemParametersInfo(
            SPI_GETDESKWALLPAPER,
            MAX_PATH,
            buffer,
            0
        );

        return buffer.ToString();
    }

    private static void SetWallpaperStyle(string style)
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(
            @"Control Panel\Desktop",
            true
        );

        switch (style.ToLower())
        {
            case "fill":
                key?.SetValue("WallpaperStyle", "10");
                key?.SetValue("TileWallpaper", "0");
                break;

            case "fit":
                key?.SetValue("WallpaperStyle", "6");
                key?.SetValue("TileWallpaper", "0");
                break;

            case "stretch":
                key?.SetValue("WallpaperStyle", "2");
                key?.SetValue("TileWallpaper", "0");
                break;

            case "tile":
                key?.SetValue("WallpaperStyle", "0");
                key?.SetValue("TileWallpaper", "1");
                break;

            case "center":
                key?.SetValue("WallpaperStyle", "0");
                key?.SetValue("TileWallpaper", "0");
                break;

            case "span":
                key?.SetValue("WallpaperStyle", "22");
                key?.SetValue("TileWallpaper", "0");
                break;
        }
    }

    private static void CloseWallpaperApps()
    {
        const string LOG_IDENT = "WallpaperController::CloseWallpaperApps";

        if (_wallpaperApps)
            return;

        _wallpaperApps = true;

        foreach (string procName in WallpaperProcesses)
        {
            foreach (Process proc in Process.GetProcessesByName(procName))
            {
                try
                {
                    string? exe = null;

                    try
                    {
                        exe = proc.MainModule?.FileName;
                    }
                    catch { }

                    if (!string.IsNullOrWhiteSpace(exe))
                        _closedWallpaperApps.Add(exe);

                    App.Logger.WriteLine(
                        LOG_IDENT,
                        $"Closing wallpaper app: {proc.ProcessName}"
                    );

                    proc.CloseMainWindow();

                    if (!proc.WaitForExit(3000))
                        proc.Kill();
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(
                        LOG_IDENT,
                        $"Failed to close wallpaper app: {ex}"
                    );
                }
            }
        }
    }

    private static void RestoreWallpaperApps()
    {
        const string LOG_IDENT = "WallpaperController::RestoreWallpaperApps";
        foreach (string exe in _closedWallpaperApps)
        {
            try
            {
                if (File.Exists(exe))
                {
                    Process.Start(exe);

                    App.Logger.WriteLine(
                        LOG_IDENT,
                        $"Restarted wallpaper app: {exe}"
                    );
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(
                    LOG_IDENT,
                    $"Failed to restart wallpaper app: {ex}"
                );
            }
        }

        _closedWallpaperApps.Clear();
        _wallpaperApps = false;
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool SystemParametersInfo(
        int uAction,
        int uParam,
        string lpvParam,
        int fuWinIni
    );

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool SystemParametersInfo(
        int uAction,
        int uParam,
        System.Text.StringBuilder lpvParam,
        int fuWinIni
    );
}
