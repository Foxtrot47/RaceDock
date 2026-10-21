// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using System.Text.Json.Nodes;

namespace RaceDock.Core;

/// <summary>Reads Griiip's numbered sectors and retains only sectors from observed best laps.</summary>
internal sealed class GriiipSectorTiming
{
    private sealed record Sample(long Pid, int Lap, int Sector, int Milliseconds, bool Valid,
        SectorTimingState State, DateTimeOffset Timestamp);
    private readonly record struct LapIdentity(int Number, int Milliseconds, int Part);
    private readonly Dictionary<long, SectorTimingSet?> _current = [];
    private readonly Dictionary<long, (LapIdentity Lap, SectorTimingSet? Sectors)> _best = [];

    public SectorTimingSet? Current(long pid) => _current.GetValueOrDefault(pid);
    public SectorTimingSet? Best(long pid) => _best.GetValueOrDefault(pid).Sectors;

    public void Reset()
    {
        _current.Clear();
        _best.Clear();
    }

    public void Update(JsonObject bootstrap, IReadOnlyDictionary<long, JsonObject> bestLaps)
    {
        var samples = ReadSamples(bootstrap).GroupBy(sample => sample.Pid)
            .ToDictionary(group => group.Key, group => group.ToList());
        _current.Clear();
        foreach (var (pid, values) in samples)
            _current[pid] = SectorSet(ForLap(values, values.Max(value => value.Lap)));
        foreach (var pid in _best.Keys.Except(bestLaps.Keys).ToArray()) _best.Remove(pid);
        foreach (var (pid, lap) in bestLaps)
            CaptureBest(pid, lap, samples.GetValueOrDefault(pid) ?? []);
    }

    private void CaptureBest(long pid, JsonObject lap, IReadOnlyList<Sample> samples)
    {
        var identity = new LapIdentity(JsonSupport.Int(lap["lapNumber"]) ?? 0,
            JsonSupport.Int(lap["lapTimeMillis"]) ?? 0, JsonSupport.Int(lap["sessionPart"]) ?? 0);
        if (identity.Number <= 0 || identity.Milliseconds <= 0 || !JsonSupport.Bool(lap["isValid"]))
        {
            _best.Remove(pid);
            return;
        }
        var values = ForLap(samples, identity.Number);
        var previous = _best.GetValueOrDefault(pid);
        var retained = previous.Lap == identity ? RetainedBest(previous.Sectors, values) : null;
        _best[pid] = (identity, BestSectors(values, identity) ?? retained);
    }

    private static SectorTimingSet? RetainedBest(SectorTimingSet? previous, IReadOnlyList<Sample> samples)
    {
        if (previous is null) return null;
        if (samples.Any(sample => !sample.Valid || Timing(sample)?.Value != previous.At(sample.Sector - 1)?.Value)) return null;
        return previous;
    }

    private static SectorTimingSet? BestSectors(IReadOnlyList<Sample> values, LapIdentity lap)
    {
        if (values.Count != 3 || values.Any(value => !value.Valid || value.Milliseconds <= 0)) return null;
        // A set of individually fastest sectors is not the sector set of the best lap.
        if (values.Sum(value => (long)value.Milliseconds) != lap.Milliseconds) return null;
        return SectorSet(values);
    }

    private static List<Sample> ForLap(IEnumerable<Sample> samples, int lap) => samples
        .Where(value => value.Lap == lap)
        .GroupBy(value => value.Sector)
        .Select(group => group.OrderBy(value => value.Timestamp).Last())
        .ToList();

    private static SectorTimingSet? SectorSet(IEnumerable<Sample> samples)
    {
        var values = samples.ToDictionary(value => value.Sector, Timing);
        var set = new SectorTimingSet(values.GetValueOrDefault(1), values.GetValueOrDefault(2), values.GetValueOrDefault(3));
        return set.Sector1 is null && set.Sector2 is null && set.Sector3 is null ? null : set;
    }

    private static SectorTiming? Timing(Sample sample)
    {
        if (sample.Milliseconds <= 0) return null;
        var time = TimeSpan.FromMilliseconds(sample.Milliseconds);
        var value = time.TotalMinutes >= 1
            ? FormattableString.Invariant($"{(int)time.TotalMinutes}:{time.Seconds:00}.{time.Milliseconds:000}")
            : time.TotalSeconds.ToString("0.000", CultureInfo.InvariantCulture);
        return new(value, sample.Valid ? sample.State : SectorTimingState.None);
    }

    private static IEnumerable<Sample> ReadSamples(JsonObject bootstrap)
    {
        var sectors = (bootstrap["sectors"] as JsonArray ?? []).OfType<JsonObject>();
        var current = (bootstrap["currentLapSectors"] as JsonObject ?? [])
            .SelectMany(pair => (pair.Value as JsonArray ?? []).OfType<JsonObject>());
        return sectors.Concat(current).Select(ReadSample).OfType<Sample>();
    }

    private static Sample? ReadSample(JsonObject row)
    {
        if (!long.TryParse(JsonSupport.String(row["pid"]), NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid)) return null;
        var lap = JsonSupport.Int(row["lapNumber"]) ?? 0;
        var sector = JsonSupport.Int(row["sectorNumber"]) ?? 0;
        if (lap <= 0 || sector is < 1 or > 3) return null;
        var timestamp = DateTimeOffset.TryParse(JsonSupport.String(row["ts"]), CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var parsed) ? parsed : DateTimeOffset.MinValue;
        return new(pid, lap, sector, JsonSupport.Int(row["sectorTimeMillis"]) ?? 0,
            JsonSupport.Bool(row["isValid"]), Color(JsonSupport.String(row["color"])), timestamp);
    }

    private static SectorTimingState Color(string? color) => color?.ToUpperInvariant() switch
    {
        "PURPLE" => SectorTimingState.OverallBest,
        "GREEN" => SectorTimingState.PersonalBest,
        "GRAY" => SectorTimingState.Slower,
        _ => SectorTimingState.None
    };
}
