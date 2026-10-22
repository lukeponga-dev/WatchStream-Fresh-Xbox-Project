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
                button.BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 57, 214, 197));
                button.Opacity = 1.0;
                if (button.Name != "BrowseButton")
                    button.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 45, 65, 98));
            }
        }

        private void Control_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                button.BorderThickness = new Thickness(1);
                button.BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 42, 56, 88));
                button.Opacity = 1.0;
                button.Background = button.Name == "BrowseButton"
                    ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 57, 214, 197))
                    : new SolidColorBrush(Windows.UI.Color.FromArgb(255, 23, 33, 58));
                if (button.Name == "ConnectButton")
                    button.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 38, 52, 82));
            }
        }

        private void NavigateToCatalog(string section) { if (Frame != null) Frame.Navigate(typeof(CatalogPage), section); }
        private void BrowseButton_Click(object sender, RoutedEventArgs e) => NavigateToCatalog("Library");
        private void ConnectButton_Click(object sender, RoutedEventArgs e) { Frame?.Navigate(typeof(AccountPage)); }
        private void LibraryButton_Click(object sender, RoutedEventArgs e) => NavigateToCatalog("Library");
        private void DiscoverButton_Click(object sender, RoutedEventArgs e) => NavigateToCatalog("Discover");
        private void SeriesButton_Click(object sender, RoutedEventArgs e) => NavigateToCatalog("Series");
        private void SettingsButton_Click(object sender, RoutedEventArgs e) => StatusText.Text = "Settings is planned for a later milestone.";
    }
}