using System.Runtime.InteropServices;
using System.Text;
using ABI_RC.Core.Savior;
using HarmonyLib;
using MelonLoader;

namespace Kafe.MinimiseVR;

public class MinimiseVR : MelonMod {

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

        if (stringBuilder.ToString().StartsWith("MelonLoader") || stringBuilder.ToString().StartsWith("ChilloutVR")) {
            // Minimize the window
            ShowWindow(hWnd, 2);
        }

        return true;
    }

    [HarmonyPatch]
    internal class HarmonyPatches {

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HQTools), nameof(HQTools.Awake))]
        public static void After_MetaPort_Awake() {
            if (MetaPort.Instance.isUsingVr) {
                EnumWindows(MinimizeConsoleWindow, IntPtr.Zero);
            }
        }
    }
}
