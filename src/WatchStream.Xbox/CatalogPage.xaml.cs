using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
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
                if (!string.Equals(_currentCategory, "Library", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.Equals(_currentCategory, "Search", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(searchQuery))
                    {
                        ShowEmptyState("Search for a title", "Enter a movie or series name above, then select Search.");
                        CategoryDesc.Text = "Search movies and series from your available catalogs.";
                        return;
                    }

                    var sections = await _catalogService.GetCatalogSectionsAsync(CancellationToken.None, searchQuery);
                    if (string.Equals(_currentCategory, "Series", StringComparison.OrdinalIgnoreCase))
                        sections = sections.Where(section => string.Equals(section.Type, "series", StringComparison.OrdinalIgnoreCase)).ToList();
                    CatalogSectionsPanel.Children.Clear();
                    foreach (var section in sections)
                        CatalogSectionsPanel.Children.Add(BuildCatalogSection(section));
                    MediaGrid.ItemsSource = null;
                    if (sections.Count == 0)
                    {
                        CatalogSectionsScrollViewer.Visibility = Visibility.Collapsed;
                        ResultsScrollViewer.Visibility = Visibility.Collapsed;
                        ShowEmptyState("No catalogs found", "Install an add-on that provides movie or series catalogs, then try again.");
                        CategoryDesc.Text = "No titles returned from available add-on catalogs.";
                    }
                    else
                    {
                        StatePanel.Visibility = Visibility.Collapsed;
                        ResultsScrollViewer.Visibility = Visibility.Collapsed;
                        CatalogSectionsScrollViewer.Visibility = Visibility.Visible;
                        StateProgressRing.IsActive = false;
                        CategoryDesc.Text = sections.Count + " catalogs loaded from your installed add-ons.";
                    }
                    return;
                }

                CatalogSectionsScrollViewer.Visibility = Visibility.Collapsed;
                IReadOnlyList<WatchStream.Core.Models.MediaItem> results = await _libraryService.GetLibraryAsync(CancellationToken.None);
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
            catch (Exception ex)
            {
                var detail = ex is System.Net.Http.HttpRequestException
                    ? "The catalog server could not be reached. Check the Xbox internet connection and try again."
                    : ex is TaskCanceledException
                        ? "The catalog request timed out. Check your connection and try again."
                        : "The catalog response could not be loaded. Check your connection and installed add-ons, then try again.";
                CategoryDesc.Text = detail;
                ShowErrorState("Couldn't load content", detail);
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

        private StackPanel BuildCatalogSection(StremioCatalogService.CatalogSection section)
        {
            var heading = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(4, 0, 4, 4) };
            heading.Children.Add(new TextBlock { Text = section.CatalogName, FontSize = 25, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 244, 246, 251)), VerticalAlignment = VerticalAlignment.Center });
            heading.Children.Add(new TextBlock { Text = section.AddonName + " · " + (section.Type == "series" ? "Series" : "Movies") + " · " + section.Items.Count, FontSize = 13, Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 155, 165, 181)), VerticalAlignment = VerticalAlignment.Center });

            var cards = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            foreach (var coreItem in section.Items)
            {
                var item = ToDomainItem(coreItem);
                var cardContent = new Grid { Width = 170, Height = 260, Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 16, 27, 48)) };
                if (Uri.TryCreate(item.PosterUrl, UriKind.Absolute, out var posterUri) && (posterUri.Scheme == Uri.UriSchemeHttps || posterUri.Scheme == Uri.UriSchemeHttp))
                {
                    try { cardContent.Children.Add(new Image { Source = new BitmapImage(posterUri), Stretch = Windows.UI.Xaml.Media.Stretch.UniformToFill, Width = 170, Height = 260 }); }
                    catch { }
                }
                cardContent.Children.Add(new Border { Background = new SolidColorBrush(Windows.UI.Color.FromArgb(220, 0, 0, 0)), VerticalAlignment = VerticalAlignment.Bottom, Padding = new Thickness(9), Child = new TextBlock { Text = item.Name, Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255)), FontSize = 14, TextWrapping = TextWrapping.Wrap, MaxLines = 2 } });
                var button = new Button { Content = cardContent, Width = 182, Height = 272, Padding = new Thickness(0), Margin = new Thickness(2), Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 16, 27, 48)), BorderThickness = new Thickness(0), Tag = item, HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
                button.Click += MediaCard_Click;
                cards.Children.Add(button);
            }

            var horizontal = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = cards, ZoomMode = ZoomMode.Disabled };
            var container = new StackPanel { Spacing = 10 };
            container.Children.Add(heading);
            container.Children.Add(horizontal);
            return container;
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
