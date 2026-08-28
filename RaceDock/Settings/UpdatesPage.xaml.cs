// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows;
using System.Windows.Controls;
using RaceDock.Core;

namespace RaceDock.App.Settings;

/// <summary>
/// Shows the release notes that ship with the build. Builds installed from the project's own downloads also
/// check whether a newer release was published; Store installations update through the Store and never ask.
/// </summary>
public sealed partial class UpdatesPage : Page
{
    private bool _checked;

    public UpdatesPage()
    {
        InitializeComponent();
        var notes = ReleaseNotes.Parse(ChangelogText.Read());
        ReleaseList.ItemsSource = notes;
        SubtitleText.Text = notes.Count == 0
            ? $"RaceDock {AppVersion.Display}."
            : $"Release notes for RaceDock {AppVersion.Display} and earlier versions.";
        if (!UpdateProbe.IsSupported) return;
        UpdateCard.Visibility = Visibility.Visible;
        UpdateStatusText.Text = $"RaceDock {AppVersion.Display}";
        UpdateDetailText.Text = "Checking for a newer release\u2026";
        Loaded += async (_, _) =>
        {
            if (_checked) return;
            _checked = true;
            await CheckAsync(refresh: false);
        };
    }

    private async void Check_Click(object sender, RoutedEventArgs e) => await CheckAsync(refresh: true);

    private async Task CheckAsync(bool refresh)
    {
        CheckButton.IsEnabled = false;
        UpdateLink.Visibility = Visibility.Collapsed;
        UpdateDetailText.Text = "Checking for a newer release\u2026";
        try
        {
            ShowResult(await UpdateProbe.CheckAsync(refresh));
        }
        catch (Exception failure)
        {
            AppLog.Warning("Update", "The published releases could not be read.", failure);
            UpdateDetailText.Text = "The published releases could not be reached. Try again later.";
        }
        finally
        {
            CheckButton.IsEnabled = true;
        }
    }

    private void ShowResult(ReleaseUpdate? update)
    {
        if (update is null)
        {
            UpdateStatusText.Text = $"RaceDock {AppVersion.Display}";
            UpdateDetailText.Text = "This is the latest release.";
            return;
        }
        UpdateStatusText.Text = $"RaceDock {update.Version} is available";
        UpdateDetailText.Text = $"You are running {AppVersion.Display}. Download the new release to update.";
        UpdateLink.NavigateUri = update.Url;
        UpdateLink.Visibility = Visibility.Visible;
    }
}
