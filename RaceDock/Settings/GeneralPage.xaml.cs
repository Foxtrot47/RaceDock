// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace RaceDock.App.Settings;

public sealed partial class GeneralPage : Page
{
    public event Action<string>? NavigateRequested;

    public GeneralPage() => InitializeComponent();

    private void SettingsCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CardAction { Tag: string page }) NavigateRequested?.Invoke(page);
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e) => (DataContext as SettingsDraft)?.ResetGeneral();
}
