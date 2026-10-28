using System;
using System.Threading;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using WatchStream.Xbox.Services;

namespace WatchStream.Xbox
{
    public sealed partial class SettingsPage : Page
    {
        public SettingsPage()
        {
            InitializeComponent();
        }

        private readonly StremioServerService _serverService = new StremioServerService();

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "Settings page ready. Use the sidebar to move between sections.";
            ServerUrlBox.Text = _serverService.GetBaseUrl();
            ServerStatusText.Text = string.IsNullOrWhiteSpace(ServerUrlBox.Text)
                ? "Enter the IP address of the PC running Stremio Server, for example http://192.168.1.20:11470."
                : "Saved server: " + ServerUrlBox.Text + " (not checked yet).";
        }

        private async void TestServerButton_Click(object sender, RoutedEventArgs e)
        {
            ServerStatusText.Text = "Checking Stremio Server heartbeatâ€¦";
            try
            {
                var message = await _serverService.CheckConnectionAsync(ServerUrlBox.Text, CancellationToken.None);
                _serverService.SaveBaseUrl(ServerUrlBox.Text);
                ServerUrlBox.Text = _serverService.GetBaseUrl();
                ServerStatusText.Text = message + ". Address saved on this device.";
            }
            catch (Exception ex)
            {
                ServerStatusText.Text = ex.Message;
            }
        }

        private void HomeButton_Click(object sender, RoutedEventArgs e) => Frame?.Navigate(typeof(MainPage));
        private void DiscoverButton_Click(object sender, RoutedEventArgs e) => Frame?.Navigate(typeof(CatalogPage), "Discover");
        private void LibraryButton_Click(object sender, RoutedEventArgs e) => Frame?.Navigate(typeof(CatalogPage), "Library");
        private void SeriesButton_Click(object sender, RoutedEventArgs e) => Frame?.Navigate(typeof(CatalogPage), "Series");
        private void AddonsButton_Click(object sender, RoutedEventArgs e) => Frame?.Navigate(typeof(AddonsPage));
        private void SearchButton_Click(object sender, RoutedEventArgs e) => Frame?.Navigate(typeof(CatalogPage), "Search");
        private void ConnectButton_Click(object sender, RoutedEventArgs e) => Frame?.Navigate(typeof(AccountPage));
        private void SettingsButton_Click(object sender, RoutedEventArgs e) => StatusText.Text = "You are already on Settings.";
    }
}
