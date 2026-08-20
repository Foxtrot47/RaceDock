// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows;
using System.Windows.Controls;

namespace RaceDock.App.Settings;

public sealed partial class ChampionshipPage : Page
{
    public ChampionshipPage() => InitializeComponent();

    private void ResetButton_Click(object sender, RoutedEventArgs e) => (DataContext as SettingsDraft)?.ResetSelectedChampionship();

    private void MoveColumnUp_Click(object sender, RoutedEventArgs e) => MoveColumn(sender, -1);

    private void MoveColumnDown_Click(object sender, RoutedEventArgs e) => MoveColumn(sender, 1);

    private void MoveColumn(object sender, int offset)
    {
        if (sender is FrameworkElement { DataContext: FlyoutColumnItem item } && DataContext is SettingsDraft draft)
            draft.MoveColumn(item, offset);
    }
}
