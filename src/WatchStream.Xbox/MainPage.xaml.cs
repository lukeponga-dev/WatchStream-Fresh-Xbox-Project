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
            WatchStream.Xbox.Services.PageTransition.PlayEntrance(this);
            // Load content asynchronously and start focus on the always-visible menu entry point.
            _ = LoadHomeCatalogAsync();
            MenuButton.Focus(FocusState.Programmatic);
        }

        private void MenuButton_Click(object sender, RoutedEventArgs e)
        {
            var opening = NavOverlay.Visibility != Visibility.Visible;
            if (opening)
            {
                NavOverlay.Visibility = Visibility.Visible;
                NavOverlay.IsHitTestVisible = true;
                NavOverlay.Opacity = 1;
                MenuButton.Content = "✕  CLOSE MENU";
                HomeNavButton.Focus(FocusState.Programmatic);
            }
            else
            {
                CloseNavigation();
                MenuButton.Focus(FocusState.Programmatic);
            }
        }

        private void CloseNavigation()
        {
            NavOverlay.Opacity = 0;
            NavOverlay.IsHitTestVisible = false;
            NavOverlay.Visibility = Visibility.Collapsed;
            MenuButton.Content = "☰  MENU";
        }

        // The app-level B/Back handler calls this before navigating away from Home.
        public bool TryCloseNavigation()
        {
            if (NavOverlay.Visibility != Visibility.Visible) return false;
            CloseNavigation();
            MenuButton.Focus(FocusState.Programmatic);
            return true;
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
                DateTime? releaseDate = DateTime.TryParse(item.Year, out var parsedYear) ? parsedYear : null;
                var detailsItem = new WatchStream.Domain.Models.MediaItem(
                    item.Id, item.Name, item.Type, item.Description,
                    string.IsNullOrWhiteSpace(item.Poster) ? null : item.Poster, releaseDate);
                Frame?.Navigate(typeof(DetailsPage), detailsItem);
            }
        }
        private void Control_GotFocus(object sender, RoutedEventArgs e)
        {
            // Card focus styling is defined in CinematicCardButtonStyle; no translation is needed here.
        }

        private void Control_LostFocus(object sender, RoutedEventArgs e)
        {
            // Keep focus changes visual-only; navigation visibility is controlled by the menu button.
        }

        private void HomeNavButton_Click(object sender, RoutedEventArgs e)
        {
            CloseNavigation();
            MenuButton.Focus(FocusState.Programmatic);
        }

        private void NavigateToCatalog(string section)
        {
            CloseNavigation();
            Frame?.Navigate(typeof(CatalogPage), section);
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e) => NavigateToCatalog("Library");
        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            CloseNavigation();
            Frame?.Navigate(typeof(AccountPage));
        }
        private void LibraryButton_Click(object sender, RoutedEventArgs e) => NavigateToCatalog("Library");
        private void DiscoverButton_Click(object sender, RoutedEventArgs e) => NavigateToCatalog("Discover");
        private void SeriesButton_Click(object sender, RoutedEventArgs e) => NavigateToCatalog("Series");
        private void SearchButton_Click(object sender, RoutedEventArgs e) => NavigateToCatalog("Search");
        private void AddonsButton_Click(object sender, RoutedEventArgs e)
        {
            CloseNavigation();
            Frame?.Navigate(typeof(AddonsPage));
        }
        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            CloseNavigation();
            Frame?.Navigate(typeof(SettingsPage));
        }
    }
}