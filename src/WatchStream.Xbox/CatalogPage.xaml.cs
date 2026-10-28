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
        private readonly WatchStream.Core.Contracts.IStremioLibraryService _libraryService;
        private string _currentCategory = "Discover";
        private string? _lastSearchQuery;
        private bool _isLoading;

        public CatalogPage()
        {
            InitializeComponent();
            var sessionStore = new SecureSessionStore();
            _catalogService = new StremioCatalogService(sessionStore);
            _libraryService = new StremioLibraryService(sessionStore);
        }

        protected override void OnNavigatedTo(Windows.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is string category && !string.IsNullOrWhiteSpace(category))
                _currentCategory = NormalizeCategory(category);
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            WatchStream.Xbox.Services.PageTransition.PlayEntrance(this);
            CategoryTitle.Text = DisplayCategoryName(_currentCategory);
            if (string.Equals(_currentCategory, "Search", StringComparison.OrdinalIgnoreCase))
                SearchBox.Focus(FocusState.Programmatic);
            PageSidebar.CurrentSection = _currentCategory;
            _ = LoadCatalogAsync();
        }

        private static string DisplayCategoryName(string category) =>
            string.Equals(category, "Library", StringComparison.OrdinalIgnoreCase) ? "My Library" : category;

        private static string NormalizeCategory(string category)
        {
            if (string.Equals(category, "My Library", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(category, "Library", StringComparison.OrdinalIgnoreCase))
                return "Library";
            if (string.Equals(category, "Add-ons", StringComparison.OrdinalIgnoreCase))
                return "Add-ons";
            if (string.Equals(category, "Connect Stremio", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(category, "Sign in", StringComparison.OrdinalIgnoreCase))
                return "Connect Stremio";
            return category;
        }

        private async Task LoadCatalogAsync(string? searchQuery = null)
        {
            if (_isLoading) return;
            _isLoading = true;
            _lastSearchQuery = searchQuery;
            CategoryTitle.Text = DisplayCategoryName(_currentCategory);
            PageSidebar.CurrentSection = _currentCategory;
            CategoryDesc.Text = "Loading content from Stremio add-ons…";
            MediaGrid.ItemsSource = null;
            ShowLoadingState();

            try
            {
                IReadOnlyList<WatchStream.Core.Models.MediaItem> results;
                if (string.Equals(_currentCategory, "Library", StringComparison.OrdinalIgnoreCase))
                {
                    results = await _libraryService.GetLibraryAsync(CancellationToken.None);
                }
                else if (string.Equals(_currentCategory, "Series", StringComparison.OrdinalIgnoreCase))
                {
                    results = await _catalogService.GetDiscoverAsync("series", CancellationToken.None, searchQuery);
                }
                else if (string.Equals(_currentCategory, "Search", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(searchQuery))
                    {
                        ShowEmptyState("Search for a title", "Enter a movie or series name above, then select Search.");
                        CategoryDesc.Text = "Search movies and series from your available catalogs.";
                        return;
                    }
                    var movies = await _catalogService.GetDiscoverAsync("movie", CancellationToken.None, searchQuery);
                    var series = await _catalogService.GetDiscoverAsync("series", CancellationToken.None, searchQuery);
                    results = movies.Concat(series)
                        .GroupBy(item => item.Type + "|" + item.Id, StringComparer.OrdinalIgnoreCase)
                        .Select(group => group.First()).ToList();
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
                if (cards.Count == 0)
                {
                    var message = string.Equals(_currentCategory, "Library", StringComparison.OrdinalIgnoreCase)
                        ? "Your Stremio library is empty, or no saved items were returned. Connect your account or explore available catalogs."
                        : "No titles were returned. Try another search or check that a compatible add-on is installed.";
                    CategoryDesc.Text = message;
                    ShowEmptyState("Nothing to show yet", message);
                }
                else
                {
                    CategoryDesc.Text = cards.Count + (cards.Count == 1 ? " title loaded from Stremio." : " titles loaded from Stremio.");
                    ShowResultsState();
                    MediaGrid.Focus(FocusState.Programmatic);
                }
            }
            catch (OperationCanceledException)
            {
                ShowErrorState("Loading was cancelled", "Try loading this section again.");
                RetryButton.Focus(FocusState.Programmatic);
            }
            catch (Exception)
            {
                const string message = "WatchStream couldn't load this section. Check your connection and Stremio account, then try again.";
                CategoryDesc.Text = message;
                ShowErrorState("Couldn't load content", message);
                RetryButton.Focus(FocusState.Programmatic);
            }
            finally
            {
                _isLoading = false;
            }
        }

        private void ShowLoadingState()
        {
            StatePanel.Visibility = Visibility.Visible;
            StateTitle.Text = "Loading " + DisplayCategoryName(_currentCategory);
            StateMessage.Text = "Retrieving available titles. This may take a moment.";
            StateProgressRing.Visibility = Visibility.Visible;
            StateProgressRing.IsActive = true;
            RetryButton.Visibility = Visibility.Collapsed;
            ResultsScrollViewer.Visibility = Visibility.Collapsed;
        }

        private void ShowEmptyState(string title, string message)
        {
            StatePanel.Visibility = Visibility.Visible;
            StateTitle.Text = title;
            StateMessage.Text = message;
            StateProgressRing.IsActive = false;
            StateProgressRing.Visibility = Visibility.Collapsed;
            RetryButton.Visibility = Visibility.Collapsed;
            ResultsScrollViewer.Visibility = Visibility.Collapsed;
        }

        private void ShowErrorState(string title, string message)
        {
            StatePanel.Visibility = Visibility.Visible;
            StateTitle.Text = title;
            StateMessage.Text = message;
            StateProgressRing.IsActive = false;
            StateProgressRing.Visibility = Visibility.Collapsed;
            RetryButton.Visibility = Visibility.Visible;
            ResultsScrollViewer.Visibility = Visibility.Collapsed;
        }

        private void ShowResultsState()
        {
            StateProgressRing.IsActive = false;
            StatePanel.Visibility = Visibility.Collapsed;
            ResultsScrollViewer.Visibility = Visibility.Visible;
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
            PageSidebar.CurrentSection = _currentCategory;
            await LoadCatalogAsync(SearchBox.Text?.Trim());
        }

        private async void RetryButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadCatalogAsync(_lastSearchQuery);
        }

        private void MediaCard_Click(object sender, RoutedEventArgs e)
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
