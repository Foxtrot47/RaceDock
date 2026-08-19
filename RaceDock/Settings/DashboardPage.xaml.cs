// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace RaceDock.App.Settings;

public sealed partial class DashboardPage : Page
{
    public event Action<string>? NavigateRequested;

    public DashboardPage()
    {
        InitializeComponent();
        VersionText.Text = $"v{AppVersion.Display}";
    }

    private void SettingsCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CardAction { Tag: string page }) NavigateRequested?.Invoke(page);
    }

    private void ViewUpdates_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Connect this action to the release/update service.
    }
}
