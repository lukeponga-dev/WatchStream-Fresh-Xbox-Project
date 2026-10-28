using System;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace WatchStream.Xbox
{
    public sealed partial class NavigationSidebar : UserControl
    {
        private static readonly SolidColorBrush SelectedBrush = new SolidColorBrush(Color.FromArgb(255, 35, 48, 76));
        private static readonly SolidColorBrush DefaultBrush = new SolidColorBrush(Color.FromArgb(255, 8, 8, 8));
        private static readonly SolidColorBrush FocusBrush = new SolidColorBrush(Color.FromArgb(255, 57, 214, 197));
        private string _currentSection = "Home";

        public NavigationSidebar() => InitializeComponent();

        public string CurrentSection
        {
            get => _currentSection;
            set
            {
                _currentSection = value ?? string.Empty;
                UpdateNavigationState();
            }
        }

        private void NavigationSidebar_Loaded(object sender, RoutedEventArgs e)
        {
            HomeButton.Content = "⌂   " + Services.LocalizedText.Get("Nav_Home", "Home");
            SearchButton.Content = "⌕   " + Services.LocalizedText.Get("Nav_Search", "Search");
            LibraryButton.Content = "▣   " + Services.LocalizedText.Get("Nav_Library", "My Library");
            DiscoverButton.Content = "◉   " + Services.LocalizedText.Get("Nav_Discover", "Discover");
            SeriesButton.Content = "▤   " + Services.LocalizedText.Get("Nav_Series", "Series");
            AddonsButton.Content = "◇   " + Services.LocalizedText.Get("Nav_Addons", "Add-ons");
            ConnectButton.Content = "↗   " + Services.LocalizedText.Get("Nav_ConnectStremio", "Connect Stremio");
            SettingsButton.Content = "⚙   " + Services.LocalizedText.Get("Nav_Settings", "Settings");
            UpdateNavigationState();
        }

        private Button[] NavigationButtons => new[]
        {
            HomeButton, SearchButton, LibraryButton, DiscoverButton,
            SeriesButton, AddonsButton, ConnectButton, SettingsButton
        };

        private void UpdateNavigationState()
        {
            if (HomeButton == null) return;
            foreach (var button in NavigationButtons)
            {
                var tag = button.Tag as string ?? string.Empty;
                var selected = string.Equals(tag, _currentSection, StringComparison.OrdinalIgnoreCase) ||
                    (tag == "Library" && string.Equals(_currentSection, "My Library", StringComparison.OrdinalIgnoreCase)) ||
                    (tag == "Connect Stremio" && string.Equals(_currentSection, "Sign in", StringComparison.OrdinalIgnoreCase));
                button.Background = selected ? SelectedBrush : DefaultBrush;
                button.BorderBrush = selected ? FocusBrush : DefaultBrush;
                button.BorderThickness = new Thickness(selected ? 2 : 1);
                button.Foreground = selected ? new SolidColorBrush(Colors.White) : new SolidColorBrush(Color.FromArgb(255, 229, 229, 229));
            }
        }

        private void NavButton_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                button.BorderBrush = FocusBrush;
                button.BorderThickness = new Thickness(3);
                button.Foreground = new SolidColorBrush(Colors.White);
            }
        }

        private void NavButton_LostFocus(object sender, RoutedEventArgs e) => UpdateNavigationState();

        private Frame? RootFrame => Window.Current.Content as Frame;

        private void Navigate(Type pageType, object? parameter = null)
        {
            var frame = RootFrame;
            if (frame == null) return;
            if (frame.Content?.GetType() == pageType &&
                (parameter == null || string.Equals(parameter as string, _currentSection, StringComparison.OrdinalIgnoreCase)))
                return;
            frame.Navigate(pageType, parameter);
        }

        private void HomeButton_Click(object sender, RoutedEventArgs e) => Navigate(typeof(MainPage));
        private void DiscoverButton_Click(object sender, RoutedEventArgs e) => Navigate(typeof(CatalogPage), "Discover");
        private void LibraryButton_Click(object sender, RoutedEventArgs e) => Navigate(typeof(CatalogPage), "Library");
        private void SeriesButton_Click(object sender, RoutedEventArgs e) => Navigate(typeof(CatalogPage), "Series");
        private void SearchButton_Click(object sender, RoutedEventArgs e) => Navigate(typeof(CatalogPage), "Search");
        private void AddonsButton_Click(object sender, RoutedEventArgs e) => Navigate(typeof(AddonsPage));
        private void ConnectButton_Click(object sender, RoutedEventArgs e) => Navigate(typeof(AccountPage));
        private void SettingsButton_Click(object sender, RoutedEventArgs e) => Navigate(typeof(SettingsPage));
    }
}
