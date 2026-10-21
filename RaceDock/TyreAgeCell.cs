// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using RaceDock.Core;

namespace RaceDock.App;

/// <summary>Four provider-reported tyre ages, with the front axle above the rear axle.</summary>
internal sealed class TyreAgeCell
{
    private readonly TextBlock[] _values = new TextBlock[4];
    private static readonly string[] WheelNames = ["Front left", "Front right", "Rear left", "Rear right"];
    public Grid Element { get; } = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3, 0, 3, 0) };

    public TyreAgeCell()
    {
        Element.ColumnDefinitions.Add(new ColumnDefinition());
        Element.ColumnDefinitions.Add(new ColumnDefinition());
        Element.RowDefinitions.Add(new RowDefinition());
        Element.RowDefinitions.Add(new RowDefinition());
        for (var index = 0; index < _values.Length; index++)
        {
            var value = new TextBlock { Text = "—", FontSize = 10, TextAlignment = TextAlignment.Center };
            value.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
            Grid.SetRow(value, index / 2);
            Grid.SetColumn(value, index % 2);
            _values[index] = value;
            Element.Children.Add(value);
        }
    }

    public void Update(CarTyres? tyres)
    {
        WheelTyre?[] wheels = [tyres?.FrontLeft, tyres?.FrontRight, tyres?.RearLeft, tyres?.RearRight];
        for (var index = 0; index < _values.Length; index++)
        {
            UpdateWheel(_values[index], wheels[index], WheelNames[index]);
        }
    }

    private static void UpdateWheel(TextBlock target, WheelTyre? wheel, string name)
    {
        var age = wheel?.AgeInLaps?.ToString(CultureInfo.InvariantCulture) ?? "—";
        target.Text = age;
        if (ClassificationRow.TyreBrush(wheel?.Compound) is { } brush) target.Foreground = brush;
        else target.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        var compound = string.IsNullOrWhiteSpace(wheel?.Compound) ? "Compound unavailable" : wheel.Compound;
        target.ToolTip = $"{name} · {compound} · Tyre age: {age} laps";
    }
}
