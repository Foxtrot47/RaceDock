// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows;
using System.Windows.Controls;
using RaceDock.App.Settings;
using RaceDock.Core;
using FluentWindow = Wpf.Ui.Controls.FluentWindow;
using InfoBadge = Wpf.Ui.Controls.InfoBadge;
using NavigationViewItem = Wpf.Ui.Controls.NavigationViewItem;

namespace RaceDock.App;

public sealed partial class SettingsWindow : FluentWindow
{
    private readonly Func<UserSettings, Task> _apply;
    private readonly SettingsDraft _draft = new();
    private readonly TestingPage _testingPage;
    private readonly Dictionary<string, UIElement> _pages = [];
    private bool _allowClose;

    public SettingsWindow(AppController controller, UserSettings current, Func<UserSettings, Task> apply)
    {
        _apply = apply;
        InitializeComponent();
        MessageText.SetResourceReference(TextBlock.ForegroundProperty, "SystemFillColorCriticalBrush");

        var dashboard = new DashboardPage();
        dashboard.NavigateRequested += NavigateTo;
        _testingPage = new TestingPage(controller);
        AddPage("Dashboard", dashboard);
        AddPage("General", new GeneralPage());
        AddPage("Championship", new ChampionshipPage());
        AddPage("Testing", _testingPage);
        AddPage("MoreChampionships", new MoreChampionshipsPage());
        AddPage("About", new AboutPage());

        _draft.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(SettingsDraft.SelectedChampionshipEnabled) or nameof(SettingsDraft.HasEnabledStream)
                or nameof(SettingsDraft.ChampionshipStatuses))
                UpdateChampionshipNavigation();
        };
        Loaded += (_, _) => ShowPage(DashboardNavigationItem);
        Closing += (_, e) =>
        {
            if (_allowClose) return;
            e.Cancel = true;
            Hide();
        };
        LoadSettings(current);
    }

    public void ClosePermanently()
    {
        _allowClose = true;
        Close();
    }

    public void LoadSettings(UserSettings settings)
    {
        _draft.Load(settings);
        _testingPage.SyncMode();
        UpdateChampionshipNavigation();
        MessageText.SetResourceReference(TextBlock.ForegroundProperty, "SystemFillColorCriticalBrush");
        MessageText.Text = "";
    }

    public void UpdateSnapshot(TimingSnapshot snapshot)
    {
        _testingPage.UpdateSnapshot(snapshot);
        _draft.UpdateTimingStatus(snapshot);
    }

    public void ClearTimingStatuses() => _draft.ClearTimingStatuses();

    private void AddPage(string key, UIElement page)
    {
        // Each page scrolls itself; otherwise the navigation presenter adds a second scroll viewer and the nested pair swallows the mouse wheel.
        ScrollViewer.SetCanContentScroll(page, false);
        if (page is FrameworkElement element) element.DataContext = _draft;
        _pages.Add(key, page);
    }

    private void NavigationItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is NavigationViewItem item) ShowPage(item);
    }

    private void NavigateTo(string pageKey)
    {
        var item = RootNavigation.MenuItems.OfType<NavigationViewItem>()
            .Concat(RootNavigation.FooterMenuItems.OfType<NavigationViewItem>())
            .FirstOrDefault(candidate => Equals(candidate.Tag, pageKey));
        if (item is not null) ShowPage(item);
    }

    private void ShowPage(NavigationViewItem item)
    {
        if (item.Tag is not string key) return;
        if (TryChampionship(key, out var championship))
        {
            _draft.SelectedChampionship = championship;
            key = "Championship";
        }
        if (!_pages.TryGetValue(key, out var page)) return;
        foreach (var navigationItem in RootNavigation.MenuItems.OfType<NavigationViewItem>()
                     .Concat(RootNavigation.FooterMenuItems.OfType<NavigationViewItem>()))
        {
            var active = ReferenceEquals(navigationItem, item);
            navigationItem.IsActive = active;
            if (active) navigationItem.SetResourceReference(BackgroundProperty, "ControlFillColorSecondaryBrush");
            else navigationItem.ClearValue(BackgroundProperty);
        }
        RootNavigation.ReplaceContent(page, _draft);
    }

    private void UpdateChampionshipNavigation()
    {
        var badgeStyle = (Style)FindResource("SettingsStatusBadgeStyle");
        foreach (var item in RootNavigation.MenuItems.OfType<NavigationViewItem>())
        {
            if (!TryChampionship(item.Tag as string, out var championship)) continue;
            var status = _draft.ChampionshipStatuses.FirstOrDefault(candidate => candidate.Championship == championship);
            if (status is null) continue;
            item.DataContext = status;
            item.InfoBadge ??= new InfoBadge { Style = badgeStyle, Margin = new Thickness(0, 0, 12, 0) };
            item.Visibility = status.IsVisible ? Visibility.Visible : Visibility.Collapsed;
            item.Opacity = status.IsChampionshipEnabled ? 1 : 0.62;
            item.ToolTip = status.IsChampionshipEnabled ? null : "Disabled — open to configure";
        }
    }

    private static bool TryChampionship(string? key, out Championship championship)
    {
        championship = Championship.Unknown;
        return key is not null && key.StartsWith("Championship:", StringComparison.Ordinal) &&
            Enum.TryParse(key["Championship:".Length..], out championship);
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e) => LoadSettings(new UserSettings());

    private async void SaveButton_Click(object sender, RoutedEventArgs e) => await SaveAsync();

    private async Task SaveAsync()
    {
        if (!_draft.HasEnabledStream)
        {
            MessageText.SetResourceReference(TextBlock.ForegroundProperty, "SystemFillColorCriticalBrush");
            MessageText.Text = "Select at least one live stream.";
            return;
        }

        var next = _draft.BuildSettings();
        SaveButton.IsEnabled = false;
        MessageText.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        MessageText.Text = "Applying…";
        try
        {
            await _apply(next);
            MessageText.SetResourceReference(TextBlock.ForegroundProperty, "SystemFillColorSuccessBrush");
            MessageText.Text = "Saved";
            Hide();
        }
        catch (Exception ex)
        {
            MessageText.SetResourceReference(TextBlock.ForegroundProperty, "SystemFillColorCriticalBrush");
            MessageText.Text = $"Could not apply settings: {ex.Message}";
        }
        finally
        {
            SaveButton.IsEnabled = true;
        }
    }
}
