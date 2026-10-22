using System;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace WatchStream.Xbox
{
    public sealed partial class CatalogPage : Page
    {
        private string _section = "Library";
        private bool _isLoading;

        public CatalogPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            var requestedSection = e.Parameter as string;
            _section = string.Equals(requestedSection, "Discover", StringComparison.OrdinalIgnoreCase)
                ? "Discover"
                : string.Equals(requestedSection, "Series", StringComparison.OrdinalIgnoreCase)
                    ? "Series"
                    : "Library";

            ConfigureSection();
        }

        private void ConfigureSection()
        {
            if (PageTitle == null)
                return;

            switch (_section)
            {
                case "Discover":
                    PageTitle.Text = "Discover";
                    PageSubtitle.Text = "Explore catalogs and find your next watch";
                    EmptyIcon.Text = "✦";
                    EmptyTitle.Text = "Nothing to discover yet";
                    EmptyDescription.Text = "Connect a compatible Stremio catalog to browse available movies and series. No catalog data is loaded until a source is configured.";
                    LoadingText.Text = "Loading discover catalogs…";
                    break;

                case "Series":
                    PageTitle.Text = "Series";
                    PageSubtitle.Text = "Browse TV and episodic content";
                    EmptyIcon.Text = "▣";
                    EmptyTitle.Text = "No series to show yet";
                    EmptyDescription.Text = "Connect a compatible catalog to browse series and episodes.";
                    LoadingText.Text = "Loading series…";
                    break;

                default:
                    PageTitle.Text = "My Library";
                    PageSubtitle.Text = "Your saved movies and series";
                    EmptyIcon.Text = "▤";
                    EmptyTitle.Text = "Your library is waiting";
                    EmptyDescription.Text = "Connect a supported account or catalog source to see your saved titles here.";
                    LoadingText.Text = "Loading your library…";
                    break;
            }

            LibraryTabButton.Opacity = _section == "Library" ? 1.0 : 0.72;
            DiscoverTabButton.Opacity = _section == "Discover" ? 1.0 : 0.72;
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadSectionAsync();
        }

        private async Task LoadSectionAsync()
        {
            if (_isLoading)
                return;

            _isLoading = true;
            ShowState(LoadingPanel);
            StatusText.Text = "Loading " + _section.ToLowerInvariant() + "…";

            try
            {
                // The catalog service is not wired to a live Stremio source yet.
                // Keep the loading/empty/error UI in place; replace this block with
                // IAddonCatalogService when the upstream catalog contract is implemented.
                await Task.Delay(350);
                ShowState(EmptyPanel);
                StatusText.Text = "No catalog source connected. Your data has not been changed.";
            }
            catch (Exception)
            {
                ErrorDescription.Text = "The section couldn't be loaded. Check your connection or catalog setup, then try again.";
                ShowState(ErrorPanel);
                StatusText.Text = "Unable to load " + _section.ToLowerInvariant() + ".";
            }
            finally
            {
                _isLoading = false;
            }
        }

        private void ShowState(StackPanel stateToShow)
        {
            LoadingPanel.Visibility = Visibility.Collapsed;
            EmptyPanel.Visibility = Visibility.Collapsed;
            ErrorPanel.Visibility = Visibility.Collapsed;
            stateToShow.Visibility = Visibility.Visible;
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            _ = LoadSectionAsync();
        }

        private void HomeButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame != null && Frame.CanGoBack)
                Frame.GoBack();
            else
                Frame.Navigate(typeof(MainPage));
        }

        private void LibraryTabButton_Click(object sender, RoutedEventArgs e)
        {
            if (_section == "Library")
                return;

            Frame.Navigate(typeof(CatalogPage), "Library");
        }

        private void DiscoverTabButton_Click(object sender, RoutedEventArgs e)
        {
            if (_section == "Discover")
                return;

            Frame.Navigate(typeof(CatalogPage), "Discover");
        }
    }
}
