// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows.Media;
using RaceDock.Core;
using Brush = System.Windows.Media.Brush;

namespace RaceDock.App;

/// <summary>Stable series accents shared by compact RaceDock surfaces.</summary>
internal static class ChampionshipAccent
{
    private static readonly Dictionary<Championship, SolidColorBrush> Colours = new()
    {
        [Championship.Formula1] = Frozen(0xE1, 0x0A, 0x0A),
        [Championship.Formula2] = Frozen(0x12, 0x5F, 0xA9),
        [Championship.Formula3] = Frozen(0xA6, 0x16, 0x52),
        [Championship.FormulaE] = Frozen(0x00, 0x67, 0x7A),
        [Championship.MotoGp] = Frozen(0xBE, 0x24, 0x32),
        [Championship.Moto2] = Frozen(0x8E, 0x48, 0x1D),
        [Championship.Moto3] = Frozen(0x7F, 0x3E, 0x64),
        [Championship.WorldRallyChampionship] = Frozen(0x9B, 0x4A, 0x09),
        [Championship.GtWorldChallengeEurope] = Frozen(0x1D, 0x72, 0x68),
        [Championship.WorldEnduranceChampionship] = Frozen(0x00, 0x71, 0x89),
        [Championship.EuropeanLeMansSeries] = Frozen(0xF0, 0x4E, 0x00),
        [Championship.MichelinLeMansCup] = Frozen(0x2F, 0x5C, 0xA6),
        [Championship.Imsa] = Frozen(0x22, 0x58, 0x9C),
        [Championship.Dtm] = Frozen(0x75, 0x5B, 0x00),
        [Championship.IndyCar] = Frozen(0xA4, 0x36, 0x21),
        [Championship.NascarCup] = Frozen(0x1F, 0x3B, 0x73),
        [Championship.NascarXfinity] = Frozen(0xC4, 0x2F, 0x2F),
        [Championship.NascarTrucks] = Frozen(0xD0, 0x8A, 0x1A),
    };

    public static Brush For(Championship championship) =>
        Colours.TryGetValue(championship, out var brush) ? brush : Brushes.Gray;

    private static SolidColorBrush Frozen(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }
}
