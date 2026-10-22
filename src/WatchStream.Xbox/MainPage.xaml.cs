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
            BrowseButton.Focus(FocusState.Programmatic);
            StatusText.Text = "Ready. Use the D-pad or left stick to move, then press A to select.";
        }

        private void NavigateToCatalog(string section)
        {
            if (Frame != null)
                Frame.Navigate(typeof(CatalogPage), section);
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateToCatalog("Library");
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame != null)
                Frame.Navigate(typeof(AccountPage));
        }

        private void LibraryButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateToCatalog("Library");
        }

        private void DiscoverButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateToCatalog("Discover");
        }

        private void SeriesButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateToCatalog("Series");
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "Settings is planned for a later milestone.";
        }
    }
}
