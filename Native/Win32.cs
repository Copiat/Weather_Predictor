using System.Runtime.InteropServices;
using System.Reflection;

namespace WeatherWidget.Native;

/// <summary>
/// Minimal Win32 P/Invoke surface used to drive the borderless Photino window:
///   • dragging the widget by its HTML canvas (ReleaseCapture + SendMessage),
///   • keeping it pinned on top of every other window (SetWindowPos / HWND_TOPMOST).
/// </summary>
public static class Win32
{
    // ----- Window messages & hit-test constants -----
    private const int WM_NCLBUTTONDOWN = 0xA1;
    private const int HT_CAPTION = 0x2;

    // ----- SetWindowPos flags -----
    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private static readonly IntPtr HWND_NOTOPMOST = new(-2);
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private const uint SWP_NOACTIVATE = 0x0010;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    /// <summary>
    /// Starts a native caption-drag: releases the mouse capture on the webview
    /// and posts <c>WM_NCLBUTTONDOWN(HTCAPTION)</c> so the OS moves the window.
    /// Called from the Blazor component when the user drags the glass canvas.
    /// </summary>
    public static void BeginDrag(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;
        ReleaseCapture();
        SendMessage(hwnd, WM_NCLBUTTONDOWN, new IntPtr(HT_CAPTION), IntPtr.Zero);
    }

    /// <summary>
    /// Pins (or un-pins) the widget above every other window using
    /// <c>HWND_TOPMOST</c>.
    /// </summary>
    public static void SetTopMost(IntPtr hwnd, bool topMost)
    {
        if (hwnd == IntPtr.Zero) return;
        var after = topMost ? HWND_TOPMOST : HWND_NOTOPMOST;
        SetWindowPos(hwnd, after, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
    }

    /// <summary>
    /// Best-effort retrieval of the native HWND from a Photino window instance.
    /// Photino exposes the handle through different property names depending on
    /// the package version, so we resolve it reflectively to stay robust.
    /// Photino's property getter throws if the window is not initialized yet,
    /// so each reflective read is guarded.
    /// </summary>
    public static IntPtr GetWindowHandle(object window)
    {
        if (window == null) return IntPtr.Zero;
        var type = window.GetType();
        var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        // Common: IntPtr WindowHandle
        try
        {
            var prop = type.GetProperty("WindowHandle", flags);
            if (prop?.GetValue(window) is IntPtr p && p != IntPtr.Zero) return p;
        }
        catch (TargetInvocationException)
        {
            // Window not initialized yet — fall through to other strategies.
        }

        // Fallback: string WindowHandleString (older builds)
        try
        {
            var strProp = type.GetProperty("WindowHandleString", flags);
            if (strProp?.GetValue(window) is string s &&
                IntPtr.TryParse(s, out var parsed) && parsed != IntPtr.Zero)
            {
                return parsed;
            }
        }
        catch (TargetInvocationException)
        {
            // Ignore and continue.
        }

        // Last resort: common private backing fields across Photino builds.
        foreach (var name in new[] { "m_windowHandle", "_windowHandle", "windowHandle" })
        {
            try
            {
                var field = type.GetField(name, flags);
                if (field?.GetValue(window) is IntPtr fp && fp != IntPtr.Zero) return fp;
            }
            catch (TargetInvocationException)
            {
                // Ignore and continue.
            }
        }

        return IntPtr.Zero;
    }
}