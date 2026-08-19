// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows;
using System.Windows.Controls;

namespace RaceDock.App.Settings;

public sealed partial class MoreChampionshipsPage : Page
{
    public MoreChampionshipsPage() => InitializeComponent();

    private void ResetButton_Click(object sender, RoutedEventArgs e) => (DataContext as SettingsDraft)?.ResetChampionshipVisibility();
}
