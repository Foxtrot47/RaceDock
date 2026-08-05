// SPDX-License-Identifier: GPL-3.0-or-later
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace RaceDock.App;

internal sealed class ScrollingTextClock
{
    private long _started = Stopwatch.GetTimestamp();

    public double ElapsedSeconds => Stopwatch.GetElapsedTime(_started).TotalSeconds;
    public event Action? ResetOccurred;

    public void Reset()
    {
        _started = Stopwatch.GetTimestamp();
        ResetOccurred?.Invoke();
    }
}

/// <summary>Uses WPF's animation clock to loop overflowing single-line text on the render thread.</summary>
internal sealed class ScrollingTextBlock : System.Windows.Controls.UserControl
{
    private const double LoopGap = 18;
    private const double PixelsPerSecond = 28;
    private static readonly ScrollingTextClock SharedClock = new();

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(ScrollingTextBlock),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsMeasure, OnTextChanged));

    public static readonly DependencyProperty TextAlignmentProperty = DependencyProperty.Register(
        nameof(TextAlignment), typeof(TextAlignment), typeof(ScrollingTextBlock),
        new FrameworkPropertyMetadata(TextAlignment.Left, FrameworkPropertyMetadataOptions.AffectsArrange, OnLayoutPropertyChanged));

    public static readonly DependencyProperty TextTrimmingProperty = TextBlock.TextTrimmingProperty.AddOwner(
        typeof(ScrollingTextBlock), new FrameworkPropertyMetadata(TextTrimming.None, OnLayoutPropertyChanged));

    private readonly Canvas _viewport = new() { ClipToBounds = true };
    private readonly Canvas _track = new();
    private readonly TextBlock _first = new() { TextWrapping = TextWrapping.NoWrap };
    private readonly TextBlock _second = new() { TextWrapping = TextWrapping.NoWrap };
    private readonly TranslateTransform _translation = new();
    private ScrollViewer? _scrollViewer;
    private AnimationClock? _animationClock;
    private ScrollingTextClock _clock = SharedClock;
    private double _textWidth;
    private double _textHeight;
    private bool _overflowing;
    private bool _clockSubscribed;

    public ScrollingTextBlock()
    {
        Focusable = false;
        ClipToBounds = true;
        SnapsToDevicePixels = true;
        _track.RenderTransform = _translation;
        _track.Children.Add(_first);
        _track.Children.Add(_second);
        _viewport.Children.Add(_track);
        Content = _viewport;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += (_, _) => UpdateAnimationState();
        SizeChanged += (_, _) => UpdateAnimationState();
        SyncText();
        SyncTypography();
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value ?? "");
    }

    public TextAlignment TextAlignment
    {
        get => (TextAlignment)GetValue(TextAlignmentProperty);
        set => SetValue(TextAlignmentProperty, value);
    }

    public TextTrimming TextTrimming
    {
        get => (TextTrimming)GetValue(TextTrimmingProperty);
        set => SetValue(TextTrimmingProperty, value);
    }

    public ScrollingTextClock Clock
    {
        get => _clock;
        set
        {
            var next = value ?? SharedClock;
            if (ReferenceEquals(_clock, next)) return;
            StopAnimation();
            UnsubscribeClock();
            _clock = next;
            SubscribeClock();
            UpdateAnimationState();
        }
    }

    protected override System.Windows.Size MeasureOverride(System.Windows.Size constraint)
    {
        base.MeasureOverride(constraint);
        MeasureText();
        var width = double.IsPositiveInfinity(constraint.Width) ? _textWidth : Math.Min(_textWidth, constraint.Width);
        return new System.Windows.Size(Math.Max(0, width), Math.Min(_textHeight, constraint.Height));
    }

    protected override System.Windows.Size ArrangeOverride(System.Windows.Size arrangeBounds)
    {
        var arranged = base.ArrangeOverride(arrangeBounds);
        UpdateMetrics(arrangeBounds.Width, arrangeBounds.Height);
        return arranged;
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property is null || !IsTypographyProperty(e.Property)) return;
        SyncTypography();
        InvalidateMeasure();
        UpdateAnimationState();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SubscribeClock();
        AttachScrollViewer();
        UpdateAnimationState();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        StopAnimation();
        DetachScrollViewer();
        UnsubscribeClock();
    }

    private static void OnTextChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        var control = (ScrollingTextBlock)dependencyObject;
        AutomationProperties.SetName(control, e.NewValue as string ?? "");
        control.SyncText();
        control.InvalidateMeasure();
        control.UpdateAnimationState();
    }

    private static void OnLayoutPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        var control = (ScrollingTextBlock)dependencyObject;
        control.InvalidateArrange();
        control.UpdateAnimationState();
    }

    private void SyncText()
    {
        _first.Text = Text;
        _second.Text = Text;
    }

    private void SyncTypography()
    {
        foreach (var text in new[] { _first, _second })
        {
            text.FontFamily = FontFamily;
            text.FontSize = FontSize;
            text.FontStyle = FontStyle;
            text.FontWeight = FontWeight;
            text.FontStretch = FontStretch;
            text.Foreground = Foreground;
            text.FlowDirection = FlowDirection;
            text.Language = Language;
            TextOptions.SetTextFormattingMode(text, TextOptions.GetTextFormattingMode(this));
            TextOptions.SetTextRenderingMode(text, TextOptions.GetTextRenderingMode(this));
        }
    }

    private void MeasureText()
    {
        _first.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
        _textWidth = Math.Ceiling(_first.DesiredSize.Width);
        _textHeight = Math.Ceiling(_first.DesiredSize.Height);
    }

    private void UpdateAnimationState()
    {
        if (!IsLoaded || !IsVisible || ActualWidth <= 0 || ActualHeight <= 0)
        {
            StopAnimation();
            return;
        }
        MeasureText();
        UpdateMetrics(ActualWidth, ActualHeight);
    }

    private void UpdateMetrics(double width, double height)
    {
        MeasureText();
        _overflowing = _textWidth > width + 0.5;
        var y = Math.Max(0, (height - _textHeight) / 2);
        Canvas.SetTop(_first, y);
        Canvas.SetTop(_second, y);
        _first.TextTrimming = _overflowing ? TextTrimming.None : TextTrimming;
        _second.TextTrimming = TextTrimming.None;

        if (!_overflowing || _textWidth <= 0)
        {
            StopAnimation();
            _first.Width = double.NaN;
            _first.TextAlignment = TextAlignment.Left;
            var x = TextAlignment switch
            {
                TextAlignment.Center => Math.Max(0, (width - _textWidth) / 2),
                TextAlignment.Right => Math.Max(0, width - _textWidth),
                _ => 0
            };
            Canvas.SetLeft(_first, x);
            _second.Visibility = Visibility.Collapsed;
            _track.Width = Math.Max(0, width);
            _track.Height = height;
            return;
        }

        _first.Width = double.NaN;
        _first.TextAlignment = TextAlignment.Left;
        _second.Visibility = Visibility.Visible;
        var period = _textWidth + LoopGap;
        Canvas.SetLeft(_first, 0);
        Canvas.SetLeft(_second, period);
        _track.Width = period + _textWidth;
        _track.Height = height;
        if (IsLoaded && IsVisible && IsInsideViewport()) StartAnimation(period);
        else StopAnimation();
    }

    private void StartAnimation(double period)
    {
        if (period <= 0) return;
        var duration = TimeSpan.FromSeconds(period / PixelsPerSecond);
        if (_animationClock?.Timeline.Duration.TimeSpan == duration) return;

        StopAnimation();
        var animation = new DoubleAnimation(0, -period, new Duration(duration))
        {
            RepeatBehavior = RepeatBehavior.Forever,
            FillBehavior = FillBehavior.HoldEnd
        };
        animation.Freeze();
        _animationClock = (AnimationClock)animation.CreateClock(true);
        _translation.ApplyAnimationClock(TranslateTransform.XProperty, _animationClock);
        _animationClock.Controller?.Begin();
        _animationClock.Controller?.SeekAlignedToLastTick(
            TimeSpan.FromSeconds(Clock.ElapsedSeconds % duration.TotalSeconds), TimeSeekOrigin.BeginTime);
    }

    private void StopAnimation()
    {
        if (_animationClock is not null)
        {
            _animationClock.Controller?.Stop();
            _translation.ApplyAnimationClock(TranslateTransform.XProperty, null);
            _animationClock = null;
        }
        _translation.X = 0;
    }

    private void SubscribeClock()
    {
        if (_clockSubscribed || !IsLoaded) return;
        Clock.ResetOccurred += OnClockReset;
        _clockSubscribed = true;
    }

    private void UnsubscribeClock()
    {
        if (!_clockSubscribed) return;
        Clock.ResetOccurred -= OnClockReset;
        _clockSubscribed = false;
    }

    private void OnClockReset()
    {
        StopAnimation();
        UpdateAnimationState();
    }

    private void AttachScrollViewer()
    {
        DependencyObject? current = this;
        while ((current = VisualTreeHelper.GetParent(current)) is not null)
        {
            if (current is not ScrollViewer viewer) continue;
            _scrollViewer = viewer;
            _scrollViewer.ScrollChanged += OnScrollChanged;
            break;
        }
    }

    private void DetachScrollViewer()
    {
        if (_scrollViewer is not null) _scrollViewer.ScrollChanged -= OnScrollChanged;
        _scrollViewer = null;
    }

    private void OnScrollChanged(object sender, ScrollChangedEventArgs eventArgs) => UpdateAnimationState();

    private bool IsInsideViewport()
    {
        if (_scrollViewer is null) return true;
        try
        {
            var bounds = TransformToAncestor(_scrollViewer).TransformBounds(new Rect(0, 0, ActualWidth, ActualHeight));
            return bounds.IntersectsWith(new Rect(0, 0, _scrollViewer.ViewportWidth, _scrollViewer.ViewportHeight));
        }
        catch (InvalidOperationException) { return false; }
    }

    private static bool IsTypographyProperty(DependencyProperty property) =>
        property == FontFamilyProperty || property == FontSizeProperty || property == FontStyleProperty ||
        property == FontWeightProperty || property == FontStretchProperty || property == ForegroundProperty ||
        property == FlowDirectionProperty || property == LanguageProperty ||
        property == TextOptions.TextFormattingModeProperty || property == TextOptions.TextRenderingModeProperty;
}
