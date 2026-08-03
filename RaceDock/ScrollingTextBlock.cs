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
    public void Reset() => _started = Stopwatch.GetTimestamp();
}

/// <summary>Draws a single line repeatedly from right to left only when it overflows.</summary>
internal sealed class ScrollingTextBlock : System.Windows.Controls.Control
{
    private const double LoopGap = 18;
    private const double PixelsPerSecond = 28;
    private static readonly HashSet<ScrollingTextBlock> Active = [];
    private static readonly ScrollingTextClock SharedClock = new();
    private static bool _renderingAttached;

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(ScrollingTextBlock),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender,
            OnTextChanged));

    public static readonly DependencyProperty TextAlignmentProperty = DependencyProperty.Register(
        nameof(TextAlignment), typeof(TextAlignment), typeof(ScrollingTextBlock),
        new FrameworkPropertyMetadata(TextAlignment.Left, FrameworkPropertyMetadataOptions.AffectsRender));

    private FormattedText? _formattedText;
    private ScrollViewer? _scrollViewer;
    private double _pixelsPerDip;
    private double _textWidth;
    private bool _overflowing;
    private bool _registered;

    public ScrollingTextBlock()
    {
        Focusable = false;
        ClipToBounds = true;
        SnapsToDevicePixels = true;
        Loaded += (_, _) =>
        {
            AttachScrollViewer();
            UpdateAnimationState();
        };
        Unloaded += (_, _) =>
        {
            DetachScrollViewer();
            Unregister();
        };
        IsVisibleChanged += (_, _) => UpdateAnimationState();
        SizeChanged += (_, _) => UpdateAnimationState();
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

    public ScrollingTextClock Clock { get; set; } = SharedClock;

    protected override System.Windows.Size MeasureOverride(System.Windows.Size constraint)
    {
        var text = Formatted();
        var naturalWidth = Math.Ceiling(text.WidthIncludingTrailingWhitespace);
        var width = double.IsPositiveInfinity(constraint.Width) ? naturalWidth : Math.Min(naturalWidth, constraint.Width);
        return new System.Windows.Size(Math.Max(0, width), Math.Ceiling(text.Height));
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        var text = Formatted();
        _textWidth = Math.Ceiling(text.WidthIncludingTrailingWhitespace);
        _overflowing = _textWidth > ActualWidth + 0.5;
        var y = Math.Max(0, (ActualHeight - text.Height) / 2);
        drawingContext.PushClip(new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight)));
        if (!_overflowing || _textWidth <= 0)
        {
            var x = TextAlignment switch
            {
                TextAlignment.Center => Math.Max(0, (ActualWidth - _textWidth) / 2),
                TextAlignment.Right => Math.Max(0, ActualWidth - _textWidth),
                _ => 0
            };
            drawingContext.DrawText(text, new System.Windows.Point(x, y));
        }
        else
        {
            var period = _textWidth + LoopGap;
            var x = -(Clock.ElapsedSeconds * PixelsPerSecond % period);
            while (x < ActualWidth)
            {
                drawingContext.DrawText(text, new System.Windows.Point(x, y));
                x += period;
            }
        }
        drawingContext.Pop();
        UpdateRegistration();
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property is not null && IsTextMetricProperty(e.Property))
        {
            _formattedText = null;
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    private static void OnTextChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        var control = (ScrollingTextBlock)dependencyObject;
        AutomationProperties.SetName(control, e.NewValue as string ?? "");
        control._formattedText = null;
        control.InvalidateMeasure();
        control.InvalidateVisual();
        if (control.IsLoaded)
            control.Dispatcher.BeginInvoke(control.UpdateAnimationState, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private FormattedText Formatted()
    {
        var pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        if (_formattedText is not null && Math.Abs(_pixelsPerDip - pixelsPerDip) < 0.001) return _formattedText;
        _pixelsPerDip = pixelsPerDip;
        CultureInfo culture;
        try { culture = Language.GetSpecificCulture(); }
        catch (InvalidOperationException) { culture = CultureInfo.CurrentUICulture; }
        _formattedText = new FormattedText(
            Text,
            culture,
            FlowDirection,
            new Typeface(FontFamily, FontStyle, FontWeight, FontStretch),
            FontSize,
            Foreground,
            null,
            TextOptions.GetTextFormattingMode(this),
            pixelsPerDip)
        {
            MaxLineCount = 1,
            Trimming = TextTrimming.None,
            TextAlignment = TextAlignment.Left
        };
        return _formattedText;
    }

    private static bool IsTextMetricProperty(DependencyProperty property) =>
        property == FontFamilyProperty || property == FontSizeProperty || property == FontStyleProperty ||
        property == FontWeightProperty || property == FontStretchProperty || property == ForegroundProperty ||
        property == FlowDirectionProperty || property == LanguageProperty || property == TextOptions.TextFormattingModeProperty;

    private void UpdateAnimationState()
    {
        if (!IsLoaded || !IsVisible || ActualWidth <= 0)
        {
            _overflowing = false;
            Unregister();
            return;
        }
        _textWidth = Math.Ceiling(Formatted().WidthIncludingTrailingWhitespace);
        _overflowing = _textWidth > ActualWidth + 0.5;
        UpdateRegistration();
        InvalidateVisual();
    }

    private void UpdateRegistration()
    {
        if (IsLoaded && IsVisible && _overflowing && IsInsideViewport()) Register();
        else Unregister();
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
            var bounds = TransformToAncestor(_scrollViewer).TransformBounds(
                new Rect(0, 0, ActualWidth, ActualHeight));
            var viewport = new Rect(0, 0, _scrollViewer.ViewportWidth, _scrollViewer.ViewportHeight);
            return bounds.IntersectsWith(viewport);
        }
        catch (InvalidOperationException) { return false; }
    }

    private void Register()
    {
        if (_registered) return;
        _registered = true;
        Active.Add(this);
        if (_renderingAttached) return;
        CompositionTarget.Rendering += OnRendering;
        _renderingAttached = true;
    }

    private void Unregister()
    {
        if (!_registered) return;
        _registered = false;
        Active.Remove(this);
        if (Active.Count != 0 || !_renderingAttached) return;
        CompositionTarget.Rendering -= OnRendering;
        _renderingAttached = false;
    }

    private static void OnRendering(object? sender, EventArgs eventArgs)
    {
        foreach (var control in Active) control.InvalidateVisual();
    }
}
