using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace WatchStream.Xbox
{
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            // Put focus on the primary action so the page is immediately usable with a controller.
            BrowseButton.Focus(FocusState.Programmatic);
            StatusText.Text = "Ready. Use the D-pad or left stick to move, then press A to select.";
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "Library browsing will be connected after the Stremio catalog service is implemented.";
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            ConnectionLabel.Text = "SETUP REQUIRED";
            StatusText.Text = "Stremio connection is not implemented yet. Next we'll add the account and API flow securely.";
        }

        private void LibraryButton_Click(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "My Library selected. Catalog data will appear here once the service is connected.";
        }

        private void DiscoverButton_Click(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "Discover selected. We'll populate this section from compatible Stremio catalogs.";
        }

        private void SeriesButton_Click(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "Series selected. TV catalog browsing is coming in the next milestone.";
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "Settings selected. Account, playback and privacy options will be added here.";
        }
    }
}
