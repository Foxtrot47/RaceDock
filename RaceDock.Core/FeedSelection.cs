// Copyright (c) 2026 RaceDock contributors
// SPDX-License-Identifier: GPL-3.0-or-later
namespace RaceDock.Core;

/// <summary>
/// Which feed a reading surface follows: the source's rotation, a manually chosen championship, or, while
/// pinned to the schedule, the first classification that arrives. Kept free of UI types so the
/// "never switch a pinned surface on its own" rule is testable.
/// </summary>
public sealed class FeedSelection
{
    /// <summary>Whether the surface takes whatever the rotation publishes.</summary>
    public bool IsFollowingRotation { get; private set; } = true;

    /// <summary>The championship the surface is frozen to, or null while it waits for one.</summary>
    public Championship? PinnedChampionship { get; private set; }

    public void FollowRotation()
    {
        IsFollowingRotation = true;
        PinnedChampionship = null;
    }

    /// <summary>
    /// Holds the surface on <paramref name="championship"/>. Null means "freeze on the next classification",
    /// which is how pinning the schedule behaves while no session is on track.
    /// </summary>
    public void Freeze(Championship? championship)
    {
        IsFollowingRotation = false;
        PinnedChampionship = championship is null or Championship.Unknown ? null : championship;
    }

    /// <summary>
    /// Whether a snapshot belongs on the surface. A frozen surface that does not have a championship yet
    /// adopts the first real classification offered to it.
    /// </summary>
    public bool TryAccept(TimingSnapshot snapshot)
    {
        if (IsFollowingRotation) return true;
        if (PinnedChampionship is { } championship) return snapshot.Championship == championship;
        if (snapshot.Championship == Championship.Unknown) return false;
        PinnedChampionship = snapshot.Championship;
        return true;
    }
}
