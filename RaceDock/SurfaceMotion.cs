using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace RaceDock.App;

/// <summary>
/// Fluent-style motion and hover feedback for the widget and the flyout.
/// Owns every duration, easing curve and hover brush so the two surfaces stay in step,
/// and so a single switch can return the app to instant, unanimated behaviour.
/// </summary>
internal static class SurfaceMotion
{
    /// <summary>Vertical travel of the flyout as it opens.</summary>
    internal const double SlideDistance = 20;

    private static readonly Duration Reveal = new(TimeSpan.FromMilliseconds(300));
    private static readonly Duration Hover = new(TimeSpan.FromMilliseconds(200));

    /// <summary>Height of the highlight along the top edge of a hovered surface.</summary>
    private const double HighlightThickness = 1.25;

    /// <summary>Fallbacks for when the Fluent palette has not been merged, as in a test harness.</summary>
    private static readonly Color FallbackFill = Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF);
    private static readonly Color FallbackHighlight = Color.FromArgb(0x5D, 0xFF, 0xFF, 0xFF);

    /// <summary>When false every transition below collapses to an immediate state change.</summary>
    public static bool Enabled { get; set; } = true;

    /// <summary>Names the palette entries that resolved, so a harness can prove the theme was consulted.</summary>
    public static string ResolvedPalette =>
        $"fill={DescribeResource("SubtleFillColorSecondary")} highlight={DescribeResource("ControlStrokeColorDefault")}";

    /// <summary>
    /// Gives a surface an animated hover fill and a highlight along its top edge, the way the
    /// Windows 11 taskbar lights a control from above.
    /// </summary>
    /// <param name="highlightTopEdge">Cards are lit from above; rows in a list are not, so a row
    /// takes the fill alone.</param>
    public static void AttachHover(Border surface, bool highlightTopEdge = true)
    {
        // Animating a brush needs a local, unfrozen instance; a resource reference cannot be
        // animated in place. The colours themselves still come from the Fluent palette.
        var fill = new SolidColorBrush(Colors.Transparent);
        surface.Background = fill;
        if (highlightTopEdge)
        {
            surface.BorderThickness = new Thickness(0, HighlightThickness, 0, 0);
            surface.BorderBrush = new SolidColorBrush(Colors.Transparent);
        }

        surface.MouseEnter += (_, _) => SetHover(surface, fill, true, highlightTopEdge);
        surface.MouseLeave += (_, _) => SetHover(surface, fill, false, highlightTopEdge);
    }

    private static Color HighlightFor(bool entering) =>
        entering ? Resolve("ControlStrokeColorDefault", FallbackHighlight) : Colors.Transparent;

    private static void SetHover(Border surface, SolidColorBrush fill, bool entering, bool highlightTopEdge)
    {
        var fillTarget = entering ? Resolve("SubtleFillColorSecondary", FallbackFill) : Colors.Transparent;

        if (!Enabled)
        {
            fill.Color = fillTarget;
            if (highlightTopEdge) surface.BorderBrush = new SolidColorBrush(HighlightFor(entering));
            return;
        }

        // The highlight leads on the way in and is dropped immediately on the way out, so the
        // edge never outlives the fill it belongs to.
        if (highlightTopEdge) surface.BorderBrush = new SolidColorBrush(HighlightFor(entering));
        fill.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation
        {
            To = fillTarget,
            Duration = Hover,
            EasingFunction = new CubicEase { EasingMode = entering ? EasingMode.EaseOut : EasingMode.EaseInOut }
        });
    }

    /// <summary>Lifts a flyout into place, optionally fading an opaque surface.</summary>
    /// <param name="slide">Carries the travel. It drives no visual of its own; the caller watches
    /// it and moves the window, so the blurred surface travels with the content.</param>
    /// <param name="fade">Whether to animate window opacity. Accent blur remains native and must not outlive faded WPF content.</param>
    public static void PlayOpen(Window window, TranslateTransform slide, bool fade = true)
    {
        slide.BeginAnimation(TranslateTransform.YProperty, null);
        window.BeginAnimation(UIElement.OpacityProperty, null);
        window.Opacity = 1;

        if (!Enabled)
        {
            slide.Y = 0;
            return;
        }

        slide.Y = SlideDistance;
        var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
        if (fade)
        {
            window.Opacity = 0;
            window.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation
            {
                To = 1,
                Duration = Reveal,
                EasingFunction = ease
            });
        }
        slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation
        {
            To = 0,
            Duration = Reveal,
            EasingFunction = ease
        });
    }

    /// <summary>Drops a flyout away, optionally fading it, and invokes <paramref name="hidden"/> once it has gone.</summary>
    public static void PlayClose(Window window, TranslateTransform slide, Action hidden, bool fade = true)
    {
        if (!Enabled)
        {
            hidden();
            return;
        }

        var ease = new QuadraticEase { EasingMode = EasingMode.EaseIn };
        var slideAnimation = new DoubleAnimation
        {
            To = SlideDistance,
            Duration = Reveal,
            EasingFunction = ease
        };
        if (fade)
        {
            var fadeAnimation = new DoubleAnimation { To = 0, Duration = Reveal, EasingFunction = ease };
            fadeAnimation.Completed += (_, _) => hidden();
            window.BeginAnimation(UIElement.OpacityProperty, fadeAnimation);
        }
        else
        {
            window.BeginAnimation(UIElement.OpacityProperty, null);
            window.Opacity = 1;
            slideAnimation.Completed += (_, _) => hidden();
        }
        slide.BeginAnimation(TranslateTransform.YProperty, slideAnimation);
    }

    /// <summary>Stops an in-flight close so a re-opened flyout is not left half faded.</summary>
    public static void CancelClose(Window window, TranslateTransform slide)
    {
        window.BeginAnimation(UIElement.OpacityProperty, null);
        slide.BeginAnimation(TranslateTransform.YProperty, null);
        window.Opacity = 1;
        slide.Y = 0;
    }

    private static Color Resolve(string key, Color fallback) =>
        System.Windows.Application.Current?.TryFindResource(key) switch
        {
            Color color => color,
            SolidColorBrush brush => brush.Color,
            _ => fallback
        };

    private static string DescribeResource(string key) =>
        System.Windows.Application.Current?.TryFindResource(key) switch
        {
            Color color => $"{key}:{color}",
            SolidColorBrush brush => $"{key}:{brush.Color}",
            _ => $"{key}:fallback"
        };
}
