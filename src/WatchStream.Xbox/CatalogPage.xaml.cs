using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using WatchStream.Domain.Models;
using WatchStream.Xbox.Services;

namespace WatchStream.Xbox
{
    public sealed partial class CatalogPage : Page
    {
        private readonly StremioCatalogService _catalogService;
        private string _currentCategory = "Discover";
        private bool _isLoading;

        public CatalogPage()
        {
            InitializeComponent();
            _catalogService = new StremioCatalogService(new SecureSessionStore());
        }

        protected override void OnNavigatedTo(Windows.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is string category)
                _currentCategory = category;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            WatchStream.Xbox.Services.PageTransition.PlayEntrance(this);
        }

        private async void CatalogPage_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadCatalogAsync();
        }

        private async Task LoadCatalogAsync(string? searchQuery = null)
        {
            if (_isLoading) return;
            _isLoading = true;
            CategoryTitle.Text = _currentCategory;
            CategoryDesc.Text = "Loading content from Stremio add-ons…";
            MediaGrid.ItemsSource = null;

            try
            {
                IReadOnlyList<WatchStream.Core.Models.MediaItem> results;
                if (string.Equals(_currentCategory, "Library", StringComparison.OrdinalIgnoreCase))
                {
                    results = await _catalogService.GetLibraryAsync(CancellationToken.None);
                }
                else if (string.Equals(_currentCategory, "Series", StringComparison.OrdinalIgnoreCase))
                {
                    results = await _catalogService.GetDiscoverAsync("series", CancellationToken.None, searchQuery);
                }
                else if (string.Equals(_currentCategory, "Search", StringComparison.OrdinalIgnoreCase))
                {
                    results = await _catalogService.GetDiscoverAsync("movie", CancellationToken.None, searchQuery);
                }
                else
                {
                    var movies = await _catalogService.GetDiscoverAsync("movie", CancellationToken.None, searchQuery);
                    var series = await _catalogService.GetDiscoverAsync("series", CancellationToken.None, searchQuery);
                    results = movies.Concat(series)
                        .GroupBy(item => item.Type + "|" + item.Id, StringComparer.OrdinalIgnoreCase)
                        .Select(group => group.First()).ToList();
                }

                var cards = results.Select(ToDomainItem).ToList();
                MediaGrid.ItemsSource = cards;
                CategoryDesc.Text = cards.Count == 0
                    ? (string.Equals(_currentCategory, "Library", StringComparison.OrdinalIgnoreCase)
                        ? "Your Stremio library is empty, or no saved items were returned."
                        : "No content was returned by the available catalogs. Try another search or install an add-on.")
                    : cards.Count + " titles loaded from Stremio.";
            }
            catch (Exception ex)
            {
                CategoryDesc.Text = ex.Message;
            }
            finally { _isLoading = false; }
        }

        private static MediaItem ToDomainItem(WatchStream.Core.Models.MediaItem item)
        {
            DateTime? releaseDate = null;
            if (DateTime.TryParse(item.Year, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                releaseDate = parsed;

            return new MediaItem(
                item.Id,
                item.Name,
                item.Type,
                item.Description,
                string.IsNullOrWhiteSpace(item.Poster) ? null : item.Poster,
                releaseDate);
        }

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            _currentCategory = "Search";
            await LoadCatalogAsync(SearchBox.Text?.Trim());
        }

        private async void MediaCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is MediaItem item)
            {
                Frame?.Navigate(typeof(DetailsPage), item,
                    new Windows.UI.Xaml.Media.Animation.SlideNavigationTransitionInfo
                    {
                        Effect = Windows.UI.Xaml.Media.Animation.SlideNavigationTransitionEffect.FromRight
                    });
            }
        }
    }
}