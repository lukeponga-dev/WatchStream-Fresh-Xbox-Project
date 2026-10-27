using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace WatchStream.Xbox
{
    public sealed partial class NavigationSidebar : UserControl
    {
        public NavigationSidebar() => InitializeComponent();

        public string CurrentSection { get; set; } = string.Empty;

        private void NavigationSidebar_Loaded(object sender, RoutedEventArgs e)
        {
            foreach (var button in new[] { HomeButton, DiscoverButton, LibraryButton, SeriesButton, SearchButton, AddonsButton, ConnectButton, SettingsButton })
            {
                var selected = string.Equals(button.Tag as string, CurrentSection, System.StringComparison.OrdinalIgnoreCase);
                button.Background = selected ? new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 82, 104, 255)) : new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 16, 31, 58));
            }
        }

        private Frame? RootFrame => Window.Current.Content as Frame;
        private void HomeButton_Click(object sender, RoutedEventArgs e) => RootFrame?.Navigate(typeof(MainPage));
        private void DiscoverButton_Click(object sender, RoutedEventArgs e) => RootFrame?.Navigate(typeof(CatalogPage), "Discover");
        private void LibraryButton_Click(object sender, RoutedEventArgs e) => RootFrame?.Navigate(typeof(CatalogPage), "Library");
        private void SeriesButton_Click(object sender, RoutedEventArgs e) => RootFrame?.Navigate(typeof(CatalogPage), "Series");
        private void SearchButton_Click(object sender, RoutedEventArgs e) => RootFrame?.Navigate(typeof(CatalogPage), "Search");
        private void AddonsButton_Click(object sender, RoutedEventArgs e) => RootFrame?.Navigate(typeof(AddonsPage));
        private void ConnectButton_Click(object sender, RoutedEventArgs e) => RootFrame?.Navigate(typeof(AccountPage));
        private void SettingsButton_Click(object sender, RoutedEventArgs e) => RootFrame?.Navigate(typeof(SettingsPage));
    }
}