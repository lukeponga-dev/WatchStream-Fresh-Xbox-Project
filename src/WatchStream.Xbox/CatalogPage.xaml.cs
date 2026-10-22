using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using WatchStream.Core.Models;
using WatchStream.Xbox.Services;

namespace WatchStream.Xbox
{
    public sealed partial class CatalogPage : Page
    {
        private string _section = "Library";
        private string _filter = "All";
        private bool _isLoading;
        private readonly ObservableCollection<MediaItem> _items = new ObservableCollection<MediaItem>();
        private List<MediaItem> _allItems = new List<MediaItem>();
        private StremioCatalogService? _catalogService;
        private CancellationTokenSource? _loadCancellation;
        private CancellationTokenSource? _searchCancellation;
        private MediaItem? _selectedItem;
        private MediaItem? _pendingDetails;

        public CatalogPage()
        {
            InitializeComponent();
            MediaGrid.ItemsSource = _items;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _pendingDetails = e.Parameter as MediaItem;
            var requestedSection = e.Parameter as string;
            if (_pendingDetails != null) requestedSection = "Discover";
            _section = string.Equals(requestedSection, "Discover", StringComparison.OrdinalIgnoreCase) ? "Discover"
                : string.Equals(requestedSection, "Search", StringComparison.OrdinalIgnoreCase) ? "Search"
                : string.Equals(requestedSection, "Series", StringComparison.OrdinalIgnoreCase) ? "Series" : "Library";
            ConfigureSection();
        }

        private void ConfigureSection()
        {
            if (PageTitle == null) return;
            bool discover = _section == "Discover";
            bool search = _section == "Search";
            bool publicCatalog = discover || search;
            PageTitle.Text = discover ? "Discover" : search ? "Search" : _section == "Series" ? "Series" : "My Library";
            PageSubtitle.Text = discover ? "Explore popular movies and series" : search ? "Search movies and series by title" : _section == "Series" ? "Your saved TV series" : "Your saved movies and series";
            EmptyIcon.Text = publicCatalog ? "✦" : "▤";
            EmptyTitle.Text = publicCatalog ? "No titles found" : "Your library is empty";
            EmptyDescription.Text = publicCatalog
                ? "No catalog items were returned. Check your connection and try again."
                : "Your Stremio account has no saved titles yet. Discover movies and series to find something to watch.";
            LoadingText.Text = publicCatalog ? "Loading public catalogs…" : "Loading your library…";
            SetNavState(SearchNavButton, search);
            SetNavState(LibraryNavButton, _section == "Library");
            SetNavState(DiscoverNavButton, discover);
            SetNavState(SeriesNavButton, _section == "Series");
            AllFilterButton.Content = discover ? "Movies" : "All";
            MoviesFilterButton.Content = discover ? "Series" : "Movies";
            SeriesFilterButton.Visibility = discover ? Visibility.Collapsed : Visibility.Visible;
            _filter = discover ? "Movies" : "All";
            UpdateFilterButtons();
        }

        private void ShowState(FrameworkElement activePanel)
        {
            LoadingPanel.Visibility = activePanel == LoadingPanel ? Visibility.Visible : Visibility.Collapsed;
            EmptyPanel.Visibility = activePanel == EmptyPanel ? Visibility.Visible : Visibility.Collapsed;
            ErrorPanel.Visibility = activePanel == ErrorPanel ? Visibility.Visible : Visibility.Collapsed;
            ResultsPanel.Visibility = activePanel == ResultsPanel ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SetNavState(Button button, bool selected)
        {
            button.Background = new SolidColorBrush(Color.FromArgb(255, selected ? (byte)57 : (byte)38, selected ? (byte)214 : (byte)52, selected ? (byte)197 : (byte)82));
            button.Foreground = new SolidColorBrush(Color.FromArgb(255, selected ? (byte)7 : (byte)245, selected ? (byte)17 : (byte)247, selected ? (byte)29 : (byte)255));
            button.BorderThickness = selected ? new Thickness(2) : new Thickness(1);
            button.BorderBrush = new SolidColorBrush(Color.FromArgb(255, selected ? (byte)57 : (byte)42, selected ? (byte)214 : (byte)56, selected ? (byte)197 : (byte)88));
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            _catalogService = new StremioCatalogService(new SecureSessionStore());
            Button initial = _section == "Discover" ? DiscoverNavButton : _section == "Search" ? SearchNavButton : _section == "Series" ? SeriesNavButton : LibraryNavButton;
            initial.Focus(FocusState.Programmatic);
            _ = LoadSectionAsync();
        }

        private async Task LoadSectionAsync()
        {
            if (_isLoading || _catalogService == null) return;
            _isLoading = true;
            _loadCancellation?.Cancel(); _loadCancellation?.Dispose();
            _loadCancellation = new CancellationTokenSource();
            var token = _loadCancellation.Token;
            ShowState(LoadingPanel);
            StatusText.Text = _section == "Discover" ? "Loading public catalogs…" : "Loading your Stremio library…";
            try
            {
                IReadOnlyList<MediaItem> result;
                if (_section == "Discover" || _section == "Search")
                {
                    var movies = await _catalogService.GetDiscoverAsync("movie", token);
                    var series = await _catalogService.GetDiscoverAsync("series", token);
                    result = movies.Concat(series).ToList();
                }
                else
                {
                    result = await _catalogService.GetLibraryAsync(token);
                    if (_section == "Series") result = result.Where(x => string.Equals(x.Type, "series", StringComparison.OrdinalIgnoreCase)).ToList();
                }

                _allItems = result.ToList();
                ApplyFilters();
                if (_pendingDetails != null)
                {
                    var requestedItem = _pendingDetails;
                    _pendingDetails = null;
                    ShowDetails(requestedItem);
                }
                if (_allItems.Count == 0)
                {
                    ShowState(EmptyPanel);
                    StatusText.Text = (_section == "Discover" || _section == "Search") ? "The catalog returned no titles." : "No saved titles in this section.";
                    initialFocus();
                }
                else
                {
                    ShowState(ResultsPanel);
                    StatusText.Text = _allItems.Count + " titles loaded.";
                    // Keep focus on the selected sidebar item after loading. This makes Xbox D-pad navigation predictable;
                    // users can move right into the results when they are ready.
                    initialFocus();
                }
            }
            catch (OperationCanceledException) { }
            catch (InvalidOperationException ex)
            {
                ErrorDescription.Text = ex.Message;
                ShowState(ErrorPanel);
                StatusText.Text = "Unable to load " + _section.ToLowerInvariant() + ".";
                bool needsAccount = ex.Message.IndexOf("Connect your Stremio", StringComparison.OrdinalIgnoreCase) >= 0;
                RetryButton.Content = needsAccount ? "Connect Stremio" : "Retry";
                RetryButton.Focus(FocusState.Programmatic);
            }
            catch (Exception)
            {
                ErrorDescription.Text = (_section == "Discover" || _section == "Search")
                    ? "The public catalog couldn't be reached. Check your connection and retry."
                    : "Couldn't read your Stremio library. Reconnect your account if the problem continues.";
                RetryButton.Content = "Retry";
                ShowState(ErrorPanel); StatusText.Text = "Unable to load " + _section.ToLowerInvariant() + ".";
                RetryButton.Focus(FocusState.Programmatic);
            }
            finally { _isLoading = false; }
        }

        private void initialFocus()
        {
            Button nav = _section == "Discover" ? DiscoverNavButton : _section == "Search" ? SearchNavButton : _section == "Series" ? SeriesNavButton : LibraryNavButton;
            nav.Focus(FocusState.Programmatic);
        }

        private void ApplyFilters()
        {
            IEnumerable<MediaItem> query = _allItems;
            if (_section == "Discover")
            {
                string type = _filter == "Series" ? "series" : "movie";
                query = query.Where(x => string.Equals(x.Type, type, StringComparison.OrdinalIgnoreCase));
            }
            else if (_filter == "Movies") query = query.Where(x => string.Equals(x.Type, "movie", StringComparison.OrdinalIgnoreCase));
            else if (_filter == "Series") query = query.Where(x => string.Equals(x.Type, "series", StringComparison.OrdinalIgnoreCase));

            var search = SearchBox.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(x => x.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
            _items.Clear();
            foreach (var item in query) _items.Add(item);
            FilteredEmptyText.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            MediaScrollViewer.Visibility = _items.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            if (ResultsPanel.Visibility == Visibility.Visible)
                StatusText.Text = _items.Count + " titles shown.";
        }

        private void UpdateFilterButtons()
        {
            SetFilterButton(AllFilterButton, _filter == "All" || (_section == "Discover" && _filter == "Movies"));
            SetFilterButton(MoviesFilterButton, _filter == "Movies" || (_section == "Discover" && _filter == "Series"));
            SetFilterButton(SeriesFilterButton, _filter == "Series");
        }

        private void SetFilterButton(Button button, bool selected)
        {
            button.Background = new SolidColorBrush(selected ? Color.FromArgb(255, 57, 214, 197) : Color.FromArgb(255, 38, 52, 82));
            button.Foreground = new SolidColorBrush(selected ? Color.FromArgb(255, 7, 17, 29) : Color.FromArgb(255, 245, 247, 255));
        }

        private void AllFilterButton_Click(object sender, RoutedEventArgs e)
        {
            _filter = _section == "Discover" ? "Movies" : "All";
            UpdateFilterButtons(); ApplyFilters();
        }
        private void MoviesFilterButton_Click(object sender, RoutedEventArgs e)
        {
            _filter = _section == "Discover" ? "Series" : "Movies";
            UpdateFilterButtons(); ApplyFilters();
        }
        private void SeriesFilterButton_Click(object sender, RoutedEventArgs e) { _filter = "Series"; UpdateFilterButtons(); ApplyFilters(); }
        private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_section != "Search")
            {
                if (_allItems.Count > 0) ApplyFilters();
                return;
            }

            var term = SearchBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(term))
            {
                _searchCancellation?.Cancel();
                if (_section == "Search" && !_isLoading && _catalogService != null)
                {
                    _ = LoadSectionAsync();
                    return;
                }
                if (_allItems.Count > 0) ApplyFilters();
                return;
            }
            if (_catalogService == null) return;

            _searchCancellation?.Cancel();
            _searchCancellation?.Dispose();
            _searchCancellation = new CancellationTokenSource();
            var token = _searchCancellation.Token;
            try
            {
                await Task.Delay(350, token);
                StatusText.Text = "Searching Stremio catalogs…";
                var movies = await _catalogService.GetDiscoverAsync("movie", token, term);
                var series = await _catalogService.GetDiscoverAsync("series", token, term);
                token.ThrowIfCancellationRequested();
                _allItems = movies.Concat(series)
                    .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First())
                    .ToList();
                ApplyFilters();
                ShowState(_items.Count > 0 ? ResultsPanel : EmptyPanel);
                StatusText.Text = _items.Count + " search result(s) for “" + term + "”.";
                if (_items.Count == 0) initialFocus();
            }
            catch (OperationCanceledException) { }
            catch (Exception)
            {
                StatusText.Text = "Search failed. Check your connection and try again.";
            }
        }

        private void MediaCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is MediaItem item) ShowDetails(item);
        }
        private void MediaGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is MediaItem item) ShowDetails(item);
        }
        private void ShowDetails(MediaItem item)
        {
            _selectedItem = item;
            DetailsTitle.Text = item.Name;
            DetailsYear.Text = item.Year;
            DetailsType.Text = string.Equals(item.Type, "series", StringComparison.OrdinalIgnoreCase) ? "Series" : "Movie";
            DetailsDescription.Text = string.IsNullOrWhiteSpace(item.Description) ? "No description was supplied by this catalog." : item.Description;
            DetailsActionStatus.Text = string.Empty;
            DetailsSourceStatus.Text = "Stream links require a compatible installed add-on.";
            DetailsPoster.Source = null;
            Uri posterUri;
            if (!string.IsNullOrWhiteSpace(item.Poster) && Uri.TryCreate(item.Poster, UriKind.Absolute, out posterUri)) DetailsPoster.Source = new BitmapImage(posterUri);
            DetailsOverlay.Visibility = Visibility.Visible;
            DetailsBackButton.Focus(FocusState.Programmatic);
            StatusText.Text = "Viewing " + item.Name + ".";
        }
        private void DetailsBackButton_Click(object sender, RoutedEventArgs e)
        {
            DetailsOverlay.Visibility = Visibility.Collapsed;
            if (!MediaGrid.Focus(FocusState.Programmatic)) initialFocus();
        }
        private async void DetailsPlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedItem == null) return;
            if (string.Equals(_selectedItem.Type, "series", StringComparison.OrdinalIgnoreCase))
            {
                DetailsSourceStatus.Text = "For series, select an episode first. Episode selection is the next playback feature.";
                DetailsActionStatus.Text = "Movie stream lookup is supported first; series playback needs an episode ID.";
                return;
            }

            DetailsPlayButton.IsEnabled = false;
            DetailsPlayButton.Content = "Searching add-ons…";
            DetailsStreamsList.Visibility = Visibility.Collapsed;
            DetailsStreamsList.ItemsSource = null;
            DetailsSourceStatus.Text = "Checking installed add-ons for streams…";
            DetailsActionStatus.Text = string.Empty;
            try
            {
                var addonService = new StremioAddonService();
                var sources = await addonService.ResolveStreamsAsync(_selectedItem.Type, _selectedItem.Id, CancellationToken.None);
                DetailsStreamsList.ItemsSource = sources;
                DetailsStreamsList.Visibility = sources.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                DetailsSourceStatus.Text = sources.Count > 0
                    ? "Choose a source to start playback."
                    : "No direct HTTP(S) streams were returned. Install an add-on that supports this title and try again.";
                DetailsActionStatus.Text = sources.Count + " playable source(s) found.";
            }
            catch (Exception ex)
            {
                DetailsSourceStatus.Text = "Stream lookup failed.";
                DetailsActionStatus.Text = ex.Message;
            }
            finally
            {
                DetailsPlayButton.IsEnabled = true;
                DetailsPlayButton.Content = "▶   Find streams";
            }
        }

        private void DetailsStreamsList_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is StreamSource source && Frame != null)
                Frame.Navigate(typeof(PlayerPage), source);
        }
        private void DetailsLibraryButton_Click(object sender, RoutedEventArgs e)
        {
            DetailsActionStatus.Text = "Saving to your Stremio library requires the account library-write integration.";
        }
        private void Control_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                button.BorderThickness = new Thickness(3);
                button.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 57, 214, 197));
                if (button != LibraryNavButton && button != DiscoverNavButton && button != SeriesNavButton && button != ConnectAccountButton)
                    button.Background = new SolidColorBrush(Color.FromArgb(255, 45, 65, 98));
            }
        }

        private void Control_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (button == SearchNavButton || button == LibraryNavButton || button == DiscoverNavButton || button == SeriesNavButton)
                {
                    bool selected = (button == SearchNavButton && _section == "Search") || (button == LibraryNavButton && _section == "Library") || (button == DiscoverNavButton && _section == "Discover") || (button == SeriesNavButton && _section == "Series");
                    SetNavState(button, selected);
                }
                else
                {
                    button.BorderThickness = new Thickness(1);
                    button.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 42, 56, 88));
                    if (button == ConnectAccountButton || button == RetryButton)
                        button.Background = new SolidColorBrush(Color.FromArgb(255, 57, 214, 197));
                    else if (button == AllFilterButton || button == MoviesFilterButton || button == SeriesFilterButton)
                        UpdateFilterButtons();
                    else button.Background = new SolidColorBrush(Color.FromArgb(255, 38, 52, 82));
                }
            }
        }

        private void ErrorActionButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.Equals(RetryButton.Content?.ToString(), "Connect Stremio", StringComparison.OrdinalIgnoreCase))
                Frame?.Navigate(typeof(AccountPage));
            else
            {
                _isLoading = false;
                _ = LoadSectionAsync();
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            _isLoading = false;
            _ = LoadSectionAsync();
        }

        private void HomeButton_Click(object sender, RoutedEventArgs e) => Frame?.Navigate(typeof(MainPage));
        private void SearchTabButton_Click(object sender, RoutedEventArgs e) { if (_section != "Search") Frame?.Navigate(typeof(CatalogPage), "Search"); else SearchBox.Focus(FocusState.Programmatic); }
        private void AddonsButton_Click(object sender, RoutedEventArgs e) => Frame?.Navigate(typeof(AddonsPage));
        private void LibraryTabButton_Click(object sender, RoutedEventArgs e) { if (_section != "Library") Frame?.Navigate(typeof(CatalogPage), "Library"); }
        private void DiscoverTabButton_Click(object sender, RoutedEventArgs e) { if (_section != "Discover") Frame?.Navigate(typeof(CatalogPage), "Discover"); }
        private void SeriesButton_Click(object sender, RoutedEventArgs e) { if (_section != "Series") Frame?.Navigate(typeof(CatalogPage), "Series"); }
        private void ConnectButton_Click(object sender, RoutedEventArgs e) => Frame?.Navigate(typeof(AccountPage));
        private void SettingsButton_Click(object sender, RoutedEventArgs e) => Frame?.Navigate(typeof(SettingsPage));

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            _loadCancellation?.Cancel();
            _loadCancellation?.Dispose();
            _loadCancellation = null;
            _searchCancellation?.Cancel();
            _searchCancellation?.Dispose();
            _searchCancellation = null;
            base.OnNavigatedFrom(e);
        }
    }
}