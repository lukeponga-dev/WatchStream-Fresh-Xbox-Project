using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using WatchStream.Xbox.Services;

namespace WatchStream.Xbox
{
    public sealed partial class AddonsPage : Page
    {
        private readonly StremioAddonService _addonService = new StremioAddonService();

        public AddonsPage() { InitializeComponent(); }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            ReloadInstalled();
            ManifestUrlBox.Focus(FocusState.Programmatic);
        }

        private void ReloadInstalled()
        {
            var installed = _addonService.GetInstalled();
            AddonsList.ItemsSource = installed;
            EmptyAddonsText.Visibility = installed.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            AddonsList.Visibility = installed.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            StatusText.Text = installed.Count + (installed.Count == 1 ? " add-on installed on this device." : " add-ons installed on this device.");
        }

        private async void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            var url = ManifestUrlBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(url))
            {
                StatusText.Text = "Enter the HTTPS URL of an add-on manifest.";
                ManifestUrlBox.Focus(FocusState.Programmatic);
                return;
            }

            InstallButton.IsEnabled = false;
            InstallButton.Content = "Checking manifest…";
            StatusText.Text = "Downloading and validating the add-on manifest…";
            try
            {
                var addon = await _addonService.InstallAsync(url, CancellationToken.None);
                ManifestUrlBox.Text = string.Empty;
                ReloadInstalled();
                StatusText.Text = "Installed " + addon.Name + ". " +
                    (addon.SupportsStreams ? "Stream support advertised." : "This add-on does not advertise stream support.") +
                    (addon.SupportsCatalogs ? " Catalog support advertised." : string.Empty);
            }
            catch (Exception ex)
            {
                StatusText.Text = ex.Message;
            }
            finally
            {
                InstallButton.IsEnabled = true;
                InstallButton.Content = "＋  Install add-on";
            }
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string url)
            {
                _addonService.Remove(url);
                ReloadInstalled();
                StatusText.Text = "Removed add-on from this device.";
            }
        }

        private void Control_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                button.BorderThickness = new Thickness(3);
                button.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 57, 214, 197));
                button.Opacity = 1;
            }
        }

        private void Control_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                button.BorderThickness = new Thickness(1);
                button.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 24, 59, 112));
                button.Opacity = 1;
            }
        }

        private void HomeButton_Click(object sender, RoutedEventArgs e) => Frame?.Navigate(typeof(MainPage));
        private void SearchButton_Click(object sender, RoutedEventArgs e) => Frame?.Navigate(typeof(CatalogPage), "Search");
        private void LibraryButton_Click(object sender, RoutedEventArgs e) => Frame?.Navigate(typeof(CatalogPage), "Library");
        private void DiscoverButton_Click(object sender, RoutedEventArgs e) => Frame?.Navigate(typeof(CatalogPage), "Discover");
        private void ConnectButton_Click(object sender, RoutedEventArgs e) => Frame?.Navigate(typeof(AccountPage));
        private void SettingsButton_Click(object sender, RoutedEventArgs e) => StatusText.Text = "Settings is planned for a later milestone.";
    }
}