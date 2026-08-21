// SPDX-License-Identifier: GPL-3.0-or-later
using System.ComponentModel;
using System.Runtime.CompilerServices;
using RaceDock.Core;

namespace RaceDock.App.Settings;

/// <summary>
/// One movable classification column on the championship page. Visibility is delegated back to the draft so the
/// existing profile save, reset, and auto-save paths stay the single source of truth for what a column shows.
/// </summary>
internal sealed class FlyoutColumnItem : INotifyPropertyChanged
{
    private readonly SettingsDraft _draft;
    private bool _canMoveUp;
    private bool _canMoveDown;
    private int _position;

    internal FlyoutColumnItem(SettingsDraft draft, FlyoutColumn column, string title, string description, bool isSupported)
    {
        _draft = draft;
        Column = column;
        Title = title;
        Description = description;
        IsSupported = isSupported;
    }

    public FlyoutColumn Column { get; }
    public string Title { get; }
    public string Description { get; }

    /// <summary>Whether this championship's feed carries the column at all. Unsupported columns are hidden outright.</summary>
    public bool IsSupported { get; }

    public bool IsVisible
    {
        get => _draft.IsColumnVisible(Column);
        set
        {
            if (_draft.IsColumnVisible(Column) == value) return;
            _draft.SetColumnVisible(Column, value);
            OnPropertyChanged();
        }
    }

    public bool CanMoveUp { get => _canMoveUp; internal set => Set(ref _canMoveUp, value); }
    public bool CanMoveDown { get => _canMoveDown; internal set => Set(ref _canMoveDown, value); }

    /// <summary>Where the column lands among the ones on show, so the list never numbers around a hidden row.</summary>
    public int Position
    {
        get => _position;
        internal set
        {
            if (_position == value) return;
            _position = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Names the move buttons for screen readers, which otherwise announce a bare arrow.</summary>
    public string MoveUpName => $"Move {Title} up";
    public string MoveDownName => $"Move {Title} down";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set(ref bool field, bool value, [CallerMemberName] string? name = null)
    {
        if (field == value) return;
        field = value;
        OnPropertyChanged(name);
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
