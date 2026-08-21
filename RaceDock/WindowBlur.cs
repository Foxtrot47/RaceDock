using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace RaceDock.App;

/// <summary>
/// Gives the flyout an acrylic surface that blurs whatever sits behind it.
/// </summary>
/// <remarks>
/// This uses the accent-policy route rather than the modern DWM system backdrop. The flyout is
/// deliberately never activated, and a DWM backdrop renders as a flat fill on a window carrying
/// WS_EX_NOACTIVATE, so it cannot show anything behind. Accent blur has no such restriction and
/// works on the layered window the flyout already needs for its slide.
/// </remarks>
internal static class WindowBlur
{
    /// <summary>Fallback tint for when the Fluent palette has not been merged, as in a test harness.</summary>
    private static readonly Color FallbackDark = Color.FromRgb(0x20, 0x20, 0x20);

    /// <summary>
    /// Rounds the window corners, reporting whether Windows honoured the request. Older builds have
    /// no such preference, and a caller that gets <see langword="false"/> must not leave rounded
    /// content behind a square window, or the corners will show through.
    /// </summary>
    public static bool TryRoundCorners(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return false;
        var corner = DwmwcpRound;
        return DwmSetWindowAttribute(hwnd, DwmwaWindowCornerPreference, ref corner, sizeof(int)) == 0;
    }

    /// <summary>Removes the blur, returning the window to an ordinary opaque surface.</summary>
    public static void Disable(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd != IntPtr.Zero) Send(hwnd, new AccentPolicy { AccentState = AccentDisabled });
    }

    /// <summary>
    /// Blurs whatever sits behind the window, reporting whether the surface became translucent.
    /// A caller that gets <see langword="false"/> must keep painting an opaque background of its own.
    /// </summary>
    /// <param name="tintPercent">How strongly the blur is tinted. Lower shows more of the desktop.</param>
    public static bool TryApply(Window window, int tintPercent)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return false;

        // Transparency effects are a system-wide accessibility and battery preference. Blurring
        // anyway would override a deliberate choice, so fall back to the opaque surface.
        if (!TransparencyEnabled()) return false;

        var tint = Resolve("SolidBackgroundFillColorBase", FallbackDark);
        var alpha = (uint)Math.Clamp(tintPercent * 255 / 100, 0, 255);
        var accent = new AccentPolicy
        {
            AccentState = AccentEnableAcrylicBlurBehind,
            // The gradient is packed as alpha followed by a BGR triple, not RGB.
            GradientColor = (alpha << 24) | ((uint)tint.B << 16) | ((uint)tint.G << 8) | tint.R
        };
        return Send(hwnd, accent);
    }

    private static bool Send(IntPtr hwnd, AccentPolicy accent)
    {

        var size = Marshal.SizeOf(accent);
        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(accent, buffer, false);
            var data = new WindowCompositionAttributeData
            {
                Attribute = WcaAccentPolicy,
                Data = buffer,
                SizeOfData = size
            };
            return SetWindowCompositionAttribute(hwnd, ref data) != 0;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static bool TransparencyEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("EnableTransparency") is not int enabled || enabled != 0;
        }
        catch (Exception)
        {
            // An unreadable preference is not a reason to refuse the effect.
            return true;
        }
    }

    private static Color Resolve(string key, Color fallback) =>
        System.Windows.Application.Current?.TryFindResource(key) switch
        {
            Color color => color,
            SolidColorBrush brush => brush.Color,
            _ => fallback
        };

    private const int WcaAccentPolicy = 19;
    private const int AccentDisabled = 0;
    private const int AccentEnableAcrylicBlurBehind = 4;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwcpRound = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public int AccentState;
        public uint AccentFlags;
        public uint GradientColor;
        public uint AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public int Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
