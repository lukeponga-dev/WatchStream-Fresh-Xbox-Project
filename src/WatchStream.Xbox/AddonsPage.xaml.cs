using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace WatchStream.Xbox
{
    public sealed partial class AddonsPage : Page
    {
        public AddonsPage() { InitializeComponent(); }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "Add-on management is not connected yet.";
        }

        private void Control_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                button.BorderThickness = new Thickness(3);
                button.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 57, 214, 197));
                if (button.Content?.ToString() != "▦   Add-ons")
                    button.Background = new SolidColorBrush(Color.FromArgb(255, 45, 65, 98));
            }
        }

        private void Control_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                button.BorderThickness = new Thickness(1);
                button.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 42, 56, 88));
                if (button.Content?.ToString() == "▦   Add-ons")
                {
                    button.Background = new SolidColorBrush(Color.FromArgb(255, 57, 214, 197));
                    button.Foreground = new SolidColorBrush(Color.FromArgb(255, 7, 17, 29));
                }
                else
                {
                    button.Background = new SolidColorBrush(Color.FromArgb(255, 38, 52, 82));
                    button.Foreground = new SolidColorBrush(Color.FromArgb(255, 245, 247, 255));
                }
            }
        }

        private void HomeButton_Click(object sender, RoutedEventArgs e) => Frame?.Navigate(typeof(MainPage));
        private void SearchButton_Click(object sender, RoutedEventArgs e) => Frame?.Navigate(typeof(CatalogPage), "Search");
        private void LibraryButton_Click(object sender, RoutedEventArgs e) => Frame?.Navigate(typeof(CatalogPage), "Library");
        private void DiscoverButton_Click(object sender, RoutedEventArgs e) => Frame?.Navigate(typeof(CatalogPage), "Discover");
        private void ConnectButton_Click(object sender, RoutedEventArgs e) => Frame?.Navigate(typeof(AccountPage));
        private void SettingsButton_Click(object sender, RoutedEventArgs e) => StatusText.Text = "Settings is planned for a later milestone.";
    }
}