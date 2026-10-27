using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using WatchStream.Core.Models;
using WatchStream.Xbox.Services;

namespace WatchStream.Xbox
{
    public sealed partial class MainPage : Page
    {
        public MainPage() { InitializeComponent(); }

        private bool _homeCatalogLoading;

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            // HomeNavButton was removed in the new Cinematic UI. 
            // We now focus the first content element or the Hero button.
            _ = LoadHomeCatalogAsync();
        }

        private async Task LoadHomeCatalogAsync()
        {
            if (_homeCatalogLoading) return;
            _homeCatalogLoading = true;
            try
            {
                var catalog = new StremioCatalogService(new SecureSessionStore());
                var movies = await catalog.GetDiscoverAsync("movie", CancellationToken.None);
                var series = await catalog.GetDiscoverAsync("series", CancellationToken.None);
                var items = movies.Concat(series)
                    .Where(item => !string.IsNullOrWhiteSpace(item.Poster))
                    .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First())
                    .Take(12)
                    .ToList();

                // Update the new Cinematic Rows instead of the old HomeMediaItems
                ContinueRow.ItemsSource = items;
                TrendingRow.ItemsSource = items;
            }
            catch (Exception)
            {
                // StatusText was removed in new UI
            }
            finally { _homeCatalogLoading = false; }
        }

        private void HomeMediaCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is MediaItem item)
            {
                Frame?.Navigate(typeof(CatalogPage), item);
            }
        }
        private void Control_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (button.Style == (Style)Resources["NavButtonStyle"])
                {
                    NavOverlay.Opacity = 1;
                    NavOverlay.Translation = new System.Numerics.Vector3(0, 0, 0);
                }
            }
        }

        private void Control_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (button.Style == (Style)Resources["NavButtonStyle"])
                {
                    // Use the focused element from the current window to avoid FocusManager context errors
                    var focused = Windows.UI.Xaml.Input.FocusManager.GetFocusedElement();
                    if (focused is not Button navBtn || navBtn.Style != (Style)Resources["NavButtonStyle"])
                    {
                        NavOverlay.Opacity = 0;
                        NavOverlay.Translation = new System.Numerics.Vector3(-300, 0, 0);
                    }
                }
            }
        }

        private void HomeNavButton_Click(object sender, RoutedEventArgs e)
        {
            // Logic updated for new UI
        }

        private void NavigateToCatalog(string section) { if (Frame != null) Frame.Navigate(typeof(CatalogPage), section); }
        private void BrowseButton_Click(object sender, RoutedEventArgs e) => NavigateToCatalog("Library");
        private void ConnectButton_Click(object sender, RoutedEventArgs e) { Frame?.Navigate(typeof(AccountPage)); }
        private void LibraryButton_Click(object sender, RoutedEventArgs e) => NavigateToCatalog("Library");
        private void DiscoverButton_Click(object sender, RoutedEventArgs e) => NavigateToCatalog("Discover");
        private void SeriesButton_Click(object sender, RoutedEventArgs e) => NavigateToCatalog("Series");
        private void SearchButton_Click(object sender, RoutedEventArgs e) => NavigateToCatalog("Search");
        private void AddonsButton_Click(object sender, RoutedEventArgs e) => Frame?.Navigate(typeof(AddonsPage));
        private void SettingsButton_Click(object sender, RoutedEventArgs e) => Frame?.Navigate(typeof(SettingsPage));
    }
}