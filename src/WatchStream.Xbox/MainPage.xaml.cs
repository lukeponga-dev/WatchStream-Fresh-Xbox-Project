using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace WatchStream.Xbox
{
    public sealed partial class MainPage : Page
    {
        public MainPage() { InitializeComponent(); }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            BrowseButton.Focus(FocusState.Programmatic);
            StatusText.Text = "Ready. Use the D-pad or left stick to move, then press A to select.";
        }

        private void Control_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                button.BorderThickness = new Thickness(3);
                button.BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 57, 214, 255));
                button.Opacity = 1.0;
                if (button.Name != "ConnectButton" && button.Name != "HomeNavButton")
                    button.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 35, 54, 100));
            }
        }

        private void Control_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                button.BorderThickness = new Thickness(1);
                button.BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 42, 56, 88));
                button.Opacity = 1.0;
                if (button.Name == "HomeNavButton")
                {
                    button.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 52, 32, 167));
                    button.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255));
                }
                else if (button.Name == "ConnectButton")
                {
                    button.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 23, 43, 77));
                    button.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255));
                }
                else
                {
                    button.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 16, 31, 58));
                    button.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 245, 247, 255));
                }
            }
        }

        private void HomeNavButton_Click(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "You're already on Home.";
            HomeNavButton.Focus(FocusState.Programmatic);
        }

        private void NavigateToCatalog(string section) { if (Frame != null) Frame.Navigate(typeof(CatalogPage), section); }
        private void BrowseButton_Click(object sender, RoutedEventArgs e) => NavigateToCatalog("Library");
        private void ConnectButton_Click(object sender, RoutedEventArgs e) { Frame?.Navigate(typeof(AccountPage)); }
        private void LibraryButton_Click(object sender, RoutedEventArgs e) => NavigateToCatalog("Library");
        private void DiscoverButton_Click(object sender, RoutedEventArgs e) => NavigateToCatalog("Discover");
        private void SeriesButton_Click(object sender, RoutedEventArgs e) => NavigateToCatalog("Series");
        private void SearchButton_Click(object sender, RoutedEventArgs e) => NavigateToCatalog("Search");
        private void AddonsButton_Click(object sender, RoutedEventArgs e) => Frame?.Navigate(typeof(AddonsPage));
        private void SettingsButton_Click(object sender, RoutedEventArgs e) => StatusText.Text = "Settings is planned for a later milestone.";
    }
}