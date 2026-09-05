// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RaceDock.Core;
using Control = System.Windows.Controls.Control;

namespace RaceDock.App;

/// <summary>
/// The published schedule, shown while no session is running. The first session is treated as the answer to
/// "what is next?"; everything after it becomes a quieter scan list rather than five equally weighted rows.
/// </summary>
internal sealed class ScheduleView
{
    public const double Width = 460;

    private readonly StackPanel _list = new();
    private readonly TextBlock _empty = new()
    {
        Text = "No published sessions for the championships you follow.",
        FontSize = 12,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(2, 8, 2, 4),
        Visibility = Visibility.Collapsed
    };

    public ScheduleView()
    {
        _empty.SetResourceReference(Control.ForegroundProperty, "TextFillColorSecondaryBrush");
        var layout = new StackPanel();
        layout.Children.Add(_empty);
        layout.Children.Add(_list);
        Element = layout;
    }

    public FrameworkElement Element { get; }

    public void Update(IReadOnlyList<UpcomingSession> sessions, DateTimeOffset now, TimeZoneInfo timeZone)
    {
        _list.Children.Clear();
        _empty.Visibility = sessions.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (sessions.Count == 0) return;

        _list.Children.Add(Heading("NEXT ON TRACK", "LOCAL TIME"));
        _list.Children.Add(Featured(sessions[0], now, timeZone));
        if (sessions.Count == 1) return;

        _list.Children.Add(Heading("LATER", $"{sessions.Count - 1} MORE", new Thickness(2, 15, 2, 7)));
        for (var index = 1; index < sessions.Count; index++)
            _list.Children.Add(Later(sessions[index], timeZone, index == sessions.Count - 1));
    }

    private static Grid Heading(string title, string context, Thickness? margin = null)
    {
        var heading = new Grid { Margin = margin ?? new Thickness(2, 1, 2, 7) };
        heading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var label = Label(title);
        var note = Label(context);
        note.FontSize = 8.5;
        Grid.SetColumn(note, 1);
        heading.Children.Add(label);
        heading.Children.Add(note);
        return heading;
    }

    private static Border Featured(UpcomingSession session, DateTimeOffset now, TimeZoneInfo timeZone)
    {
        var card = new Border
        {
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(12, 11, 12, 11),
            BorderBrush = ChampionshipAccent.For(session.Championship),
            BorderThickness = new Thickness(3, 0, 0, 0)
        };
        card.SetResourceReference(Border.BackgroundProperty, "ControlFillColorSecondaryBrush");

        var layout = new Grid();
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        card.Child = layout;

        var logo = ChampionshipLogo.Create(session.Championship, 72, 34);
        logo.Margin = new Thickness(0, 0, 12, 0);
        logo.VerticalAlignment = VerticalAlignment.Center;
        layout.Children.Add(logo);

        var copy = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(copy, 1);
        copy.Children.Add(EventName(ChampionshipDisplay.ShortEventName(session.Championship, session.EventName), 16));
        if (!string.IsNullOrWhiteSpace(session.SessionName))
            copy.Children.Add(Secondary(session.SessionName!, 12.5, new Thickness(0, 2, 0, 0)));
        var venue = ChampionshipDisplay.ShortVenue(session.Venue);
        if (!string.IsNullOrWhiteSpace(venue))
            copy.Children.Add(Secondary(venue, 11, new Thickness(0, 3, 0, 0), tertiary: true));
        layout.Children.Add(copy);

        var when = new StackPanel
        {
            Margin = new Thickness(14, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 72
        };
        Grid.SetColumn(when, 2);
        var start = NextSessionDisplay.RowTime(session, timeZone);
        var time = Primary(start.Length == 0 ? "TIME TBA" : start, start.Length == 0 ? 11 : 20);
        time.TextAlignment = TextAlignment.Right;
        when.Children.Add(time);
        var date = Secondary(LocalDate(session, timeZone), 10.5, new Thickness(0, 1, 0, 0));
        date.TextAlignment = TextAlignment.Right;
        when.Children.Add(date);
        var countdown = Secondary(ChampionshipCalendars.DescribeTimeUntil(session.StartUtc, now).ToUpperInvariant(),
            9, new Thickness(0, 5, 0, 0), tertiary: true);
        countdown.FontWeight = FontWeights.SemiBold;
        countdown.TextAlignment = TextAlignment.Right;
        when.Children.Add(countdown);
        layout.Children.Add(when);
        card.ToolTip = NextSessionDisplay.Tooltip(session, timeZone);
        return card;
    }

    private static Grid Later(UpcomingSession session, TimeZoneInfo timeZone, bool last)
    {
        var row = new Grid { Margin = new Thickness(2, 0, 2, last ? 2 : 10) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var logo = ChampionshipLogo.Create(session.Championship, 54, 27);
        logo.Margin = new Thickness(0, 2, 11, 0);
        logo.VerticalAlignment = VerticalAlignment.Top;
        row.Children.Add(logo);

        var copy = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(copy, 1);
        copy.Children.Add(EventName(ChampionshipDisplay.ShortEventName(session.Championship, session.EventName), 13.5));
        var subtitle = NextSessionDisplay.RowSubtitle(session);
        if (subtitle.Length > 0)
            copy.Children.Add(Secondary(subtitle, 10.5, new Thickness(0, 1, 0, 0)));
        row.Children.Add(copy);

        var when = new StackPanel { Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(when, 2);
        var date = Secondary(LocalDate(session, timeZone), 10, new Thickness(0));
        date.TextAlignment = TextAlignment.Right;
        when.Children.Add(date);
        var start = NextSessionDisplay.RowTime(session, timeZone);
        var time = Primary(start.Length == 0 ? "TBA" : start, start.Length == 0 ? 10.5 : 14);
        time.Margin = new Thickness(0, 1, 0, 0);
        time.TextAlignment = TextAlignment.Right;
        when.Children.Add(time);
        row.Children.Add(when);
        row.ToolTip = NextSessionDisplay.Tooltip(session, timeZone);
        return row;
    }

    private static string LocalDate(UpcomingSession session, TimeZoneInfo timeZone) =>
        TimeZoneInfo.ConvertTime(session.StartUtc, timeZone).ToString("ddd d MMM");

    private static TextBlock Label(string text)
    {
        var label = new TextBlock { Text = text, FontSize = 9, FontWeight = FontWeights.SemiBold };
        label.SetResourceReference(Control.ForegroundProperty, "TextFillColorTertiaryBrush");
        return label;
    }

    private static TextBlock EventName(string text, double size)
    {
        var value = new TextBlock
        {
            Text = text,
            FontSize = size,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        value.SetResourceReference(Control.ForegroundProperty, "TextFillColorPrimaryBrush");
        return value;
    }

    private static TextBlock Primary(string text, double size)
    {
        var value = new TextBlock { Text = text, FontSize = size, FontWeight = FontWeights.SemiBold };
        value.SetResourceReference(Control.ForegroundProperty, "TextFillColorPrimaryBrush");
        return value;
    }

    private static TextBlock Secondary(string text, double size, Thickness margin, bool tertiary = false)
    {
        var value = new TextBlock
        {
            Text = text,
            FontSize = size,
            Margin = margin,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        value.SetResourceReference(Control.ForegroundProperty,
            tertiary ? "TextFillColorTertiaryBrush" : "TextFillColorSecondaryBrush");
        return value;
    }

}
