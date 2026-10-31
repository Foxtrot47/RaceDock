// SPDX-License-Identifier: GPL-3.0-or-later
namespace RaceDock.Core;

/// <summary>
/// Picks the letter colour for a solid background by WCAG contrast, so the letters are readable on any strip colour
/// regardless of the taskbar's theme.
/// </summary>
public static class TaskbarContrast
{
    /// <summary>True when black text gives the higher contrast ratio against the background than white does.</summary>
    public static bool PrefersBlackText(byte red, byte green, byte blue)
    {
        var luminance = RelativeLuminance(red, green, blue);
        var againstBlack = (luminance + 0.05) / 0.05;
        var againstWhite = 1.05 / (luminance + 0.05);
        return againstBlack >= againstWhite;
    }

    /// <summary>WCAG relative luminance of an 8-bit sRGB colour.</summary>
    public static double RelativeLuminance(byte red, byte green, byte blue) =>
        0.2126 * Linearise(red) + 0.7152 * Linearise(green) + 0.0722 * Linearise(blue);

    private static double Linearise(byte channel)
    {
        var value = channel / 255.0;
        return value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}
