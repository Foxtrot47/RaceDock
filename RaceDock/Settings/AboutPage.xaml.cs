// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows.Controls;

namespace RaceDock.App.Settings;

public sealed partial class AboutPage : Page
{
    public AboutPage()
    {
        InitializeComponent();
        RaceDockVersionText.Text = AppVersion.Display;
    }
}
