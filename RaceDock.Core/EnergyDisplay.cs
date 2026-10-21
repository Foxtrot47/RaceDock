// SPDX-License-Identifier: GPL-3.0-or-later
namespace RaceDock.Core;

public enum EnergyLevel { Unavailable, Good, Warning, Low }

/// <summary>Display thresholds chosen for RaceDock, not official WEC broadcast cutoffs.</summary>
public static class EnergyDisplay
{
    public static EnergyLevel Level(double? percent)
    {
        if (!percent.HasValue || !double.IsFinite(percent.Value) || percent.Value is < 0 or > 100)
            return EnergyLevel.Unavailable;
        if (percent.Value > 50) return EnergyLevel.Good;
        return percent.Value > 20 ? EnergyLevel.Warning : EnergyLevel.Low;
    }
}
