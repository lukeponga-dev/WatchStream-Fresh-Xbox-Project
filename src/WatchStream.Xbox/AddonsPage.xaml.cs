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
            AddonsNavButton.Focus(FocusState.Programmatic);
        }

        private void ReloadInstalled()
        {
            var installed = _addonService.GetInstalled();
            try
            {
                // Use a WinRT-friendly observable vector rather than an array/IReadOnlyList.
                AddonsList.ItemsSource = new System.Collections.ObjectModel.ObservableCollection<InstalledAddon>(installed);
            }
            catch (Exception ex)
            {
                StatusText.Text = "Add-ons list binding failed: " + ex.GetType().FullName + " | " + ex.Message + " | source=" + installed.GetType().FullName + " | count=" + installed.Count;
                AddonsList.Visibility = Visibility.Collapsed;
                EmptyAddonsText.Visibility = Visibility.Visible;
                return;
            }
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

        private async void SyncAccountButton_Click(object sender, RoutedEventArgs e)
        {
            SyncAccountButton.IsEnabled = false;
            StatusText.Text = "Syncing add-ons from your Stremio account…";
            try
            {
                var imported = await _addonService.SyncFromAccountAsync(new SecureSessionStore(), CancellationToken.None);
                ReloadInstalled();
                StatusText.Text = "Stremio sync complete. Imported or refreshed " + imported +
                    " compatible add-on(s). Legacy add-on transports are not imported by this version.";
            }
            catch (Exception ex)
            {
                StatusText.Text = ex.Message;
            }
            finally { SyncAccountButton.IsEnabled = true; }
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
        private void SeriesButton_Click(object sender, RoutedEventArgs e) => Frame?.Navigate(typeof(CatalogPage), "Series");
        private void AddonsButton_Click(object sender, RoutedEventArgs e) => StatusText.Text = "You are already on Add-ons.";
        private void ConnectButton_Click(object sender, RoutedEventArgs e) => Frame?.Navigate(typeof(AccountPage));
        private void SettingsButton_Click(object sender, RoutedEventArgs e) => Frame?.Navigate(typeof(SettingsPage));
    }
}