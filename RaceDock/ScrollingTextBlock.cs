// SPDX-License-Identifier: GPL-3.0-or-later
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

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

/// <summary>Loops overflowing single-line text continuously while preserving its offset across shell visibility changes.</summary>
internal sealed class ScrollingTextBlock : System.Windows.Controls.UserControl
{
    private const string LoopSpacer = "\u00A0\u00A0\u00A0\u00A0\u00A0";
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
    private readonly TextBlock _text = new() { TextWrapping = TextWrapping.NoWrap };
    private readonly TranslateTransform _translation = new();
    private ScrollViewer? _scrollViewer;
    private ScrollingTextClock _clock = SharedClock;
    private long _lastRenderTimestamp;
    private double _scrollOffset;
    private double _scrollPeriod;
    private bool _rendering;
    private double _textWidth;
    private double _textHeight;
    private bool _overflowing;
    private bool _clockSubscribed;

    public ScrollingTextBlock()
    {
        Focusable = false;
        ClipToBounds = true;
        SnapsToDevicePixels = true;
        _text.RenderTransform = _translation;
        _viewport.Children.Add(_text);
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
            _scrollOffset = 0;
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

    private void SyncText() => _text.Text = Text;

    private void SyncTypography()
    {
        _text.FontFamily = FontFamily;
        _text.FontSize = FontSize;
        _text.FontStyle = FontStyle;
        _text.FontWeight = FontWeight;
        _text.FontStretch = FontStretch;
        _text.Foreground = Foreground;
        _text.FlowDirection = FlowDirection;
        _text.Language = Language;
        TextOptions.SetTextFormattingMode(_text, TextOptions.GetTextFormattingMode(this));
        TextOptions.SetTextRenderingMode(_text, TextOptions.GetTextRenderingMode(this));
    }

    private void MeasureText()
    {
        var size = MeasureString(Text);
        _textWidth = Math.Ceiling(size.Width);
        _textHeight = Math.Ceiling(size.Height);
    }

    private double MeasureLoopSpacer() => Math.Ceiling(MeasureString(LoopSpacer).Width);

    private System.Windows.Size MeasureString(string value)
    {
        var formatted = new FormattedText(
            value,
            CultureInfo.CurrentCulture,
            FlowDirection,
            new Typeface(FontFamily, FontStyle, FontWeight, FontStretch),
            FontSize,
            Brushes.Transparent,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        return new System.Windows.Size(formatted.WidthIncludingTrailingWhitespace, formatted.Height);
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
        Canvas.SetTop(_text, y);
        _text.TextTrimming = _overflowing ? TextTrimming.None : TextTrimming;

        if (!_overflowing || _textWidth <= 0)
        {
            StopAnimation();
            _scrollOffset = 0;
            _translation.X = 0;
            _text.Text = Text;
            _text.Width = double.NaN;
            _text.TextAlignment = TextAlignment.Left;
            var x = TextAlignment switch
            {
                TextAlignment.Center => Math.Max(0, (width - _textWidth) / 2),
                TextAlignment.Right => Math.Max(0, width - _textWidth),
                _ => 0
            };
            Canvas.SetLeft(_text, x);
            return;
        }

        var spacerWidth = MeasureLoopSpacer();
        var period = _textWidth + spacerWidth;
        _text.Text = Text + LoopSpacer + Text;
        _text.Width = double.NaN;
        _text.TextAlignment = TextAlignment.Left;
        Canvas.SetLeft(_text, 0);
        if (IsLoaded && IsVisible && IsInsideViewport()) StartAnimation(period);
        else StopAnimation();
    }

    private void StartAnimation(double period)
    {
        if (period <= 0) return;
        _scrollPeriod = period;
        _scrollOffset %= period;
        _translation.X = -_scrollOffset;
        if (_rendering) return;
        _lastRenderTimestamp = Stopwatch.GetTimestamp();
        CompositionTarget.Rendering += OnRendering;
        _rendering = true;
    }

    private void StopAnimation()
    {
        if (_rendering) CompositionTarget.Rendering -= OnRendering;
        _rendering = false;
        _lastRenderTimestamp = 0;
        _scrollPeriod = 0;
    }

    private void OnRendering(object? sender, EventArgs eventArgs)
    {
        if (!_rendering || _scrollPeriod <= 0) return;
        var now = Stopwatch.GetTimestamp();
        var elapsed = Stopwatch.GetElapsedTime(_lastRenderTimestamp, now).TotalSeconds;
        _lastRenderTimestamp = now;
        _scrollOffset = (_scrollOffset + elapsed * PixelsPerSecond) % _scrollPeriod;
        _translation.X = -_scrollOffset;
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
        _scrollOffset = 0;
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
