using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using WatchStream.Core.Models;
using WatchStream.Xbox.Services;

namespace WatchStream.Xbox
{
    public sealed partial class MainPage : Page
    {
        public MainPage() => InitializeComponent();
        private bool _homeCatalogLoading;

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            WatchStream.Xbox.Services.PageTransition.PlayEntrance(this);
            _ = LoadHomeCatalogAsync();
            _ = RefreshAccountPromptAsync();
            HeroPlayButton.Focus(FocusState.Programmatic);
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
                    .Select(group => group.First()).Take(18).ToList();
                ContinueRow.ItemsSource = items.Take(6).ToList();
                TrendingRow.ItemsSource = items.Skip(6).Take(12).ToList();
                var featured = items.FirstOrDefault();
                if (featured != null)
                {
                    HeroTitle.Text = featured.Name;
                    HeroDesc.Text = string.IsNullOrWhiteSpace(featured.Description)
                        ? "Explore movies and series from your available Stremio catalogs."
                        : featured.Description;
                    if (Uri.TryCreate(featured.Poster, UriKind.Absolute, out var posterUri) &&
                        (posterUri.Scheme == Uri.UriSchemeHttps || posterUri.Scheme == Uri.UriSchemeHttp))
                        HeroBackdrop.Source = new BitmapImage(posterUri);
                }
            }
            catch (Exception)
            {
                HeroTitle.Text = "Your next favourite is waiting";
                HeroDesc.Text = "Connect your Stremio account and compatible add-ons to load your catalogs here.";
            }
            finally { _homeCatalogLoading = false; }
        }

        private async Task RefreshAccountPromptAsync()
        {
            try
            {
                var profile = await new StremioAccountService(new SecureSessionStore()).GetProfileAsync(CancellationToken.None);
                if (profile == null)
                {
                    AccountPromptTitle.Text = "Your entertainment, all in one place";
                    AccountPromptDescription.Text = "Connect Stremio to bring your add-ons and library together.";
                    HomeAccountButton.Content = "SIGN IN";
                    return;
                }

                AccountPromptTitle.Text = "Welcome back, " + profile.DisplayName;
                AccountPromptDescription.Text = "Your Stremio account is connected. Your library and add-ons are ready to explore.";
                HomeAccountButton.Content = "VIEW PROFILE";
            }
            catch (Exception)
            {
                // Keep the home screen usable if account verification is temporarily offline.
            }
        }

        private void HomeAccountButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.Equals(HomeAccountButton.Content?.ToString(), "VIEW PROFILE", StringComparison.OrdinalIgnoreCase))
                Frame?.Navigate(typeof(ProfilePage));
            else
                Frame?.Navigate(typeof(AccountPage));
        }

        public bool TryCloseNavigation() => false;

        private void HeroPlayButton_Click(object sender, RoutedEventArgs e) => NavigateToCatalog("Discover");
        private void HeroInfoButton_Click(object sender, RoutedEventArgs e) => NavigateToCatalog("Library");

        private void HomeMediaCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is MediaItem item)
            {
                DateTime? releaseDate = DateTime.TryParse(item.Year, out var parsedYear) ? parsedYear : null;
                var detailsItem = new WatchStream.Domain.Models.MediaItem(item.Id, item.Name, item.Type,
                    item.Description, string.IsNullOrWhiteSpace(item.Poster) ? null : item.Poster, releaseDate);
                Frame?.Navigate(typeof(DetailsPage), detailsItem);
            }
        }

        private void Control_GotFocus(object sender, RoutedEventArgs e) { }
        private void Control_LostFocus(object sender, RoutedEventArgs e) { }
        private void NavigateToCatalog(string section) => Frame?.Navigate(typeof(CatalogPage), section);
        private void BrowseButton_Click(object sender, RoutedEventArgs e) => NavigateToCatalog("Library");
        private void ConnectButton_Click(object sender, RoutedEventArgs e) => Frame?.Navigate(typeof(AccountPage));
        private void LibraryButton_Click(object sender, RoutedEventArgs e) => NavigateToCatalog("Library");
        private void DiscoverButton_Click(object sender, RoutedEventArgs e) => NavigateToCatalog("Discover");
        private void SeriesButton_Click(object sender, RoutedEventArgs e) => NavigateToCatalog("Series");
        private void SearchButton_Click(object sender, RoutedEventArgs e) => NavigateToCatalog("Search");
        private void AddonsButton_Click(object sender, RoutedEventArgs e) => Frame?.Navigate(typeof(AddonsPage));
        private void SettingsButton_Click(object sender, RoutedEventArgs e) => Frame?.Navigate(typeof(SettingsPage));
    }
}