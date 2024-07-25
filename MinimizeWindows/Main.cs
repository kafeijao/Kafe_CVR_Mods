using System.Runtime.InteropServices;
using System.Text;
using ABI_RC.Core.Savior;
using ABI_RC.Core.Savior.SceneManagers;
using ABI_RC.Systems.GameEventSystem;
using HarmonyLib;
using MelonLoader;

namespace Kafe.MinimizeWindows;

public class MinimizeWindows : MelonMod {

    private const string GameWindowName = "ChilloutVR";
    private const string ConsoleWindowPrefix = "MelonLoader v";

    public override void OnInitializeMelon() {
        ModConfig.InitializeMelonPrefs();

        CVRGameEventSystem.Initialization.OnPlayerSetupStart.AddListener(() => {
            try {
                EnumWindows(MinimizeConsoleWindow, IntPtr.Zero);
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during {nameof(CVRGameEventSystem.Initialization.OnPlayerSetupStart)}");
                MelonLogger.Error(e);
            }
        });

    }

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hwnd, int nCmdShow);


    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder strText, int maxCount);


    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr hWnd);


    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    private static bool MinimizeConsoleWindow(IntPtr hWnd, IntPtr lParam) {
        var length = GetWindowTextLength(hWnd);
        var stringBuilder = new StringBuilder(length + 1);
        GetWindowText(hWnd, stringBuilder, stringBuilder.Capacity);
        var windowName = stringBuilder.ToString();

        if (MetaPort.Instance.isUsingVr) {
            if (ModConfig.MeMinimizeGameWindowInVR.Value && windowName.Equals(GameWindowName)) ShowWindow(hWnd, 2);
            if (ModConfig.MeMinimizeMelonConsoleWindowInVR.Value && windowName.StartsWith(ConsoleWindowPrefix) && windowName.Contains(GameWindowName)) ShowWindow(hWnd, 2);
        }
        else {
            if (ModConfig.MeMinimizeGameWindowInDesktop.Value && windowName.Equals(GameWindowName)) ShowWindow(hWnd, 2);
            if (ModConfig.MeMinimizeMelonConsoleWindowInDesktop.Value && windowName.StartsWith(ConsoleWindowPrefix) && windowName.Contains(GameWindowName)) ShowWindow(hWnd, 2);
        }

        // Continue iterating the windows
        return true;
    }
}
