// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using RaceDock.App.Settings;
using RaceDock.Core;
using FluentWindow = Wpf.Ui.Controls.FluentWindow;
using InfoBadge = Wpf.Ui.Controls.InfoBadge;
using NavigationViewItem = Wpf.Ui.Controls.NavigationViewItem;

namespace RaceDock.App;

public sealed partial class SettingsWindow : FluentWindow
{
    /// <summary>Properties that describe what is on screen rather than what is stored.</summary>
    private static readonly HashSet<string> TransientProperties =
    [
        nameof(SettingsDraft.SelectedChampionship),
        nameof(SettingsDraft.SelectedChampionshipPresentation),
        nameof(SettingsDraft.SelectedChampionshipPageLogo),
        nameof(SettingsDraft.SelectedChampionshipStatus),
        nameof(SettingsDraft.SelectedChampionshipResetLabel),
        nameof(SettingsDraft.SelectedChampionshipEnabled),
        nameof(SettingsDraft.HasEnabledStream)
    ];

    private const string NavigationItemBackgroundKey = "NavigationViewItemBackground";

    private readonly Func<UserSettings, Task> _apply;
    private readonly SettingsDraft _draft = new();
    private readonly TestingPage _testingPage;
    private readonly Dictionary<string, UIElement> _pages = [];
    private readonly DispatcherTimer _autoSave = new() { Interval = TimeSpan.FromMilliseconds(600) };
    private bool _loading;
    private bool _allowClose;

    public SettingsWindow(AppController controller, UserSettings current, Func<UserSettings, Task> apply)
    {
        _apply = apply;
        InitializeComponent();
        ErrorBar.IsOpen = false;

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
            if (!_loading && args.PropertyName is not null && !TransientProperties.Contains(args.PropertyName)) ScheduleSave();
        };
        foreach (var status in _draft.ChampionshipStatuses)
            status.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName is nameof(ChampionshipStatusItem.StatusKind)) UpdateChampionshipNavigation();
            };
        RootNavigation.PaneOpened += (_, _) => UpdateChampionshipNavigation();
        RootNavigation.PaneClosed += (_, _) => UpdateChampionshipNavigation();
        _autoSave.Tick += async (_, _) => await SaveAsync();
        Loaded += (_, _) => ShowPage(DashboardNavigationItem);
        Closing += (_, e) =>
        {
            if (_allowClose) return;
            e.Cancel = true;
            Hide();
        };
        // Closing the window only hides it, so pending edits are applied on the way out.
        IsVisibleChanged += async (_, args) => { if (!(bool)args.NewValue) await FlushAsync(); };
        LoadSettings(current);
    }

    public void ClosePermanently()
    {
        _allowClose = true;
        Close();
    }

    public void LoadSettings(UserSettings settings)
    {
        _loading = true;
        try
        {
            _draft.Load(settings);
            _testingPage.SyncMode();
            UpdateChampionshipNavigation();
        }
        finally { _loading = false; }
        _autoSave.Stop();
        ErrorBar.IsOpen = false;
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
            ApplyCollapsedTint(item, status);
        }
    }

    /// <summary>Carries session state on a collapsed pane, where the label badge would cover the championship artwork.</summary>
    /// <remarks>
    /// The navigation item template fills its row from the NavigationViewItemBackground resource rather than from the
    /// item's Background, so the tint has to enter the lookup through the item's own resource scope. Hover and selection
    /// set that background on the template border directly and still win over the tint.
    /// </remarks>
    private void ApplyCollapsedTint(NavigationViewItem item, ChampionshipStatusItem status)
    {
        var tint = RootNavigation.IsPaneOpen ? null : TintResourceKey(status.StatusKind);
        if (tint is null) item.Resources.Remove(NavigationItemBackgroundKey);
        else item.Resources[NavigationItemBackgroundKey] = FindResource(tint);
    }

    private static string? TintResourceKey(ChampionshipStatusKind kind) => kind switch
    {
        ChampionshipStatusKind.Live => "SystemFillColorSuccessBackgroundBrush",
        ChampionshipStatusKind.PreSession => "SystemFillColorCautionBackgroundBrush",
        ChampionshipStatusKind.RedFlag => "SystemFillColorCriticalBackgroundBrush",
        ChampionshipStatusKind.Ended => "SystemFillColorNeutralBackgroundBrush",
        _ => null
    };

    private static bool TryChampionship(string? key, out Championship championship)
    {
        championship = Championship.Unknown;
        return key is not null && key.StartsWith("Championship:", StringComparison.Ordinal) &&
            Enum.TryParse(key["Championship:".Length..], out championship);
    }

    /// <summary>Coalesces edits so a slider drag applies once instead of on every step.</summary>
    private void ScheduleSave()
    {
        _autoSave.Stop();
        _autoSave.Start();
    }

    private async Task FlushAsync()
    {
        if (_autoSave.IsEnabled) await SaveAsync();
    }

    private async Task SaveAsync()
    {
        _autoSave.Stop();
        try
        {
            await _apply(_draft.BuildSettings());
            ErrorBar.IsOpen = false;
        }
        catch (Exception ex)
        {
            ErrorBar.Message = ex.Message;
            ErrorBar.IsOpen = true;
        }
    }
}
