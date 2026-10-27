using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;

namespace WatchStream.Xbox.Services
{
    /// <summary>
    /// Applies a lightweight fade-and-slide entrance without relying on unsupported XAML properties.
    /// </summary>
    internal static class PageTransition
    {
        public static void PlayEntrance(FrameworkElement element)
        {
            if (element == null) return;

            var translate = new TranslateTransform { X = 22 };
            element.RenderTransform = translate;
            element.Opacity = 0;

            var fade = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = new Duration(TimeSpan.FromMilliseconds(220))
            };
            Storyboard.SetTarget(fade, element);
            Storyboard.SetTargetProperty(fade, "Opacity");

            var slide = new DoubleAnimation
            {
                From = 22,
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(260)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(slide, translate);
            Storyboard.SetTargetProperty(slide, "X");

            var storyboard = new Storyboard();
            storyboard.Children.Add(fade);
            storyboard.Children.Add(slide);
            storyboard.Begin();
        }
    }
}
