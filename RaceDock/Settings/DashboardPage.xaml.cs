// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows;
using System.Windows.Controls;
using RaceDock.Core;
using Wpf.Ui.Controls;

namespace RaceDock.App.Settings;

public sealed partial class DashboardPage : Page
{
    public event Action<string>? NavigateRequested;

    public DashboardPage()
    {
        InitializeComponent();
        VersionText.Text = $"v{AppVersion.Display}";
        if (UpdateProbe.IsSupported) Loaded += async (_, _) => await ShowUpdateStateAsync();
    }

    /// <summary>Builds installed from the project releases report their own update state here.</summary>
    private async Task ShowUpdateStateAsync()
    {
        UpdateStateIcon.Symbol = SymbolRegular.ArrowSync24;
        UpdateStateTitle.Text = "Checking for updates";
        UpdateStateDetail.Text = "Reading the published releases\u2026";
        try
        {
            var update = await UpdateProbe.CheckAsync();
            UpdateStateIcon.Symbol = update is null ? SymbolRegular.CheckmarkCircle24 : SymbolRegular.ArrowDownload24;
            UpdateStateTitle.Text = update is null ? "RaceDock is up to date" : $"RaceDock {update.Version} is available";
            UpdateStateDetail.Text = update is null
                ? "Use View updates for release notes"
                : "Use View updates to download it";
        }
        catch (Exception failure)
        {
            AppLog.Warning("Update", "The published releases could not be read.", failure);
            UpdateStateIcon.Symbol = SymbolRegular.CloudOff24;
            UpdateStateTitle.Text = "Update check unavailable";
            UpdateStateDetail.Text = "The published releases could not be reached";
        }
    }

    private void SettingsCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CardAction { Tag: string page }) NavigateRequested?.Invoke(page);
    }

    private void ViewUpdates_Click(object sender, RoutedEventArgs e) => NavigateRequested?.Invoke("Updates");
}
