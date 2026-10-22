using System;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace WatchStream.Xbox
{
    public sealed partial class CatalogPage : Page
    {
        private string _section = "Library";
        private bool _isLoading;
        public CatalogPage() { InitializeComponent(); }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            var requestedSection = e.Parameter as string;
            _section = string.Equals(requestedSection, "Discover", StringComparison.OrdinalIgnoreCase) ? "Discover"
                : string.Equals(requestedSection, "Series", StringComparison.OrdinalIgnoreCase) ? "Series" : "Library";
            ConfigureSection();
        }

        private void ConfigureSection()
        {
            if (PageTitle == null) return;
            switch (_section)
            {
                case "Discover":
                    PageTitle.Text = "Discover"; PageSubtitle.Text = "Explore catalogs and find your next watch"; EmptyIcon.Text = "✦";
                    EmptyTitle.Text = "Nothing to discover yet"; EmptyDescription.Text = "Connect a compatible Stremio catalog to browse available movies and series. No catalog data is loaded until a source is configured."; LoadingText.Text = "Loading discover catalogs…"; break;
                case "Series":
                    PageTitle.Text = "Series"; PageSubtitle.Text = "Browse TV and episodic content"; EmptyIcon.Text = "▣";
                    EmptyTitle.Text = "No series to show yet"; EmptyDescription.Text = "Connect a compatible catalog to browse series and episodes."; LoadingText.Text = "Loading series…"; break;
                default:
                    PageTitle.Text = "My Library"; PageSubtitle.Text = "Your saved movies and series"; EmptyIcon.Text = "▤";
                    EmptyTitle.Text = "Your library is waiting"; EmptyDescription.Text = "Connect a supported account or catalog source to see your saved titles here."; LoadingText.Text = "Loading your library…"; break;
            }
            SetNavState(LibraryNavButton, _section == "Library");
            SetNavState(DiscoverNavButton, _section == "Discover");
            SetNavState(SeriesNavButton, _section == "Series");
        }

        private void SetNavState(Button button, bool selected)
        {
            button.Background = new SolidColorBrush(selected ? Windows.UI.Color.FromArgb(255, 57, 214, 197) : Windows.UI.Color.FromArgb(255, 38, 52, 82));
            button.Foreground = new SolidColorBrush(selected ? Windows.UI.Color.FromArgb(255, 7, 17, 29) : Windows.UI.Color.FromArgb(255, 245, 247, 255));
            button.BorderThickness = selected ? new Thickness(2) : new Thickness(1);
            button.BorderBrush = new SolidColorBrush(selected ? Windows.UI.Color.FromArgb(255, 57, 214, 197) : Windows.UI.Color.FromArgb(255, 42, 56, 88));
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            Button initial = _section == "Discover" ? DiscoverNavButton : _section == "Series" ? SeriesNavButton : LibraryNavButton;
            initial.Focus(FocusState.Programmatic);
            _ = LoadSectionAsync();
        }

        private async Task LoadSectionAsync()
        {
            if (_isLoading) return;
            _isLoading = true; ShowState(LoadingPanel); StatusText.Text = "Loading " + _section.ToLowerInvariant() + "…";
            try
            {
                await Task.Delay(350);
                ShowState(EmptyPanel);
                StatusText.Text = "No catalog source connected. Your data has not been changed.";
                // Keep focus in the left navigation rather than on a button that appears after loading.
                Button nav = _section == "Discover" ? DiscoverNavButton : _section == "Series" ? SeriesNavButton : LibraryNavButton;
                nav.Focus(FocusState.Programmatic);
            }
            catch (Exception)
            {
                ErrorDescription.Text = "The section couldn't be loaded. Check your connection or catalog setup, then try again.";
                ShowState(ErrorPanel); StatusText.Text = "Unable to load " + _section.ToLowerInvariant() + ".";
                RetryButton.Focus(FocusState.Programmatic);
            }
            finally { _isLoading = false; }
        }

        private void ShowState(StackPanel stateToShow)
        {
            LoadingPanel.Visibility = Visibility.Collapsed; EmptyPanel.Visibility = Visibility.Collapsed; ErrorPanel.Visibility = Visibility.Collapsed;
            stateToShow.Visibility = Visibility.Visible;
        }

        private void Control_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                button.BorderThickness = new Thickness(3);
                button.BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 57, 214, 197));
                if (button != LibraryNavButton && button != DiscoverNavButton && button != SeriesNavButton && button != ConnectAccountButton)
                    button.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 45, 65, 98));
            }
        }

        private void Control_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (button == LibraryNavButton || button == DiscoverNavButton || button == SeriesNavButton)
                {
                    bool selected = (button == LibraryNavButton && _section == "Library") || (button == DiscoverNavButton && _section == "Discover") || (button == SeriesNavButton && _section == "Series");
                    button.BorderThickness = selected ? new Thickness(2) : new Thickness(1);
                    button.BorderBrush = new SolidColorBrush(selected ? Windows.UI.Color.FromArgb(255, 57, 214, 197) : Windows.UI.Color.FromArgb(255, 42, 56, 88));
                    button.Background = new SolidColorBrush(selected ? Windows.UI.Color.FromArgb(255, 57, 214, 197) : Windows.UI.Color.FromArgb(255, 38, 52, 82));
                    button.Foreground = new SolidColorBrush(selected ? Windows.UI.Color.FromArgb(255, 7, 17, 29) : Windows.UI.Color.FromArgb(255, 245, 247, 255));
                }
                else
                {
                    button.BorderThickness = new Thickness(1);
                    button.BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 42, 56, 88));
                    button.Background = button == ConnectAccountButton || button == RetryButton ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 57, 214, 197)) : new SolidColorBrush(Windows.UI.Color.FromArgb(255, 38, 52, 82));
                }
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e) => _ = LoadSectionAsync();
        private void HomeButton_Click(object sender, RoutedEventArgs e) { if (Frame != null && Frame.CanGoBack) Frame.GoBack(); else Frame?.Navigate(typeof(MainPage)); }
        private void LibraryTabButton_Click(object sender, RoutedEventArgs e) { if (_section != "Library") Frame?.Navigate(typeof(CatalogPage), "Library"); }
        private void DiscoverTabButton_Click(object sender, RoutedEventArgs e) { if (_section != "Discover") Frame?.Navigate(typeof(CatalogPage), "Discover"); }
        private void SeriesButton_Click(object sender, RoutedEventArgs e) { if (_section != "Series") Frame?.Navigate(typeof(CatalogPage), "Series"); }
        private void ConnectButton_Click(object sender, RoutedEventArgs e) => Frame?.Navigate(typeof(AccountPage));
        private void SettingsButton_Click(object sender, RoutedEventArgs e) { StatusText.Text = "Settings is planned for a later milestone."; }
    }
}