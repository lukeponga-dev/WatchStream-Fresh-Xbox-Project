using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.System;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using WatchStream.Xbox.Services;

namespace WatchStream.Xbox
{
    public sealed partial class AddonsPage : Page
    {
        private readonly StremioAddonService _addonService = new StremioAddonService();

        public AddonsPage() { InitializeComponent(); }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            ReloadInstalled();
#if WATCHSTREAM_ADDON_DIRECTORY_PERMISSION_GRANTED
            await LoadDirectoryAsync();
#else
            DirectorySearchButton.IsEnabled = false;
            DirectorySearchBox.IsEnabled = false;
            DirectoryEmptyText.Text = "Community directory browsing is disabled until written permission for this integration is granted. You can still install an add-on using its HTTPS manifest URL above.";
            DirectoryEmptyText.Visibility = Visibility.Visible;
            DirectoryList.Visibility = Visibility.Collapsed;
#endif
        }

        private async Task LoadDirectoryAsync()
        {
            DirectorySearchButton.IsEnabled = false;
            DirectoryEmptyText.Text = "Loading community add-ons…";
            DirectoryEmptyText.Visibility = Visibility.Visible;
            DirectoryList.Visibility = Visibility.Collapsed;
            try
            {
                var results = await _addonService.SearchDirectoryAsync(DirectorySearchBox.Text, CancellationToken.None);
                DirectoryList.ItemsSource = new System.Collections.Generic.List<StremioAddonService.AddonDirectoryEntry>(results);
                DirectoryEmptyText.Text = results.Count == 0
                    ? "No add-ons matched that search. Try another name or category."
                    : string.Empty;
                DirectoryEmptyText.Visibility = results.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                DirectoryList.Visibility = results.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
                StatusText.Text = "Showing " + results.Count + " community add-ons. Select Add to install one on this device.";
            }
            catch (Exception ex)
            {
                DirectoryEmptyText.Text = ex is TaskCanceledException
                    ? "The community directory request timed out. Check your connection and try again."
                    : ex is System.Net.Http.HttpRequestException
                        ? "The community directory could not be reached. Check your connection and try again."
                        : "The community directory could not be loaded. Try again later.";
                DirectoryEmptyText.Visibility = Visibility.Visible;
                DirectoryList.Visibility = Visibility.Collapsed;
            }
            finally
            {
#if WATCHSTREAM_ADDON_DIRECTORY_PERMISSION_GRANTED
                DirectorySearchButton.IsEnabled = true;
#endif
            }
        }

        private async void DirectorySearchButton_Click(object sender, RoutedEventArgs e) => await LoadDirectoryAsync();

        private async void DirectorySearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                e.Handled = true;
                await LoadDirectoryAsync();
            }
        }

        private async void DirectoryInstallButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string manifestUrl)
                await InstallManifestAsync(manifestUrl);
        }

        private async Task InstallManifestAsync(string manifestUrl)
        {
            InstallButton.IsEnabled = false;
            DirectorySearchButton.IsEnabled = false;
            StatusText.Text = "Downloading and validating the selected add-on manifest…";
            try
            {
                var addon = await _addonService.InstallAsync(manifestUrl, CancellationToken.None);
                ManifestUrlBox.Text = string.Empty;
                ReloadInstalled();
                StatusText.Text = "Installed " + addon.Name + " on this device. " +
                    (addon.SupportsStreams ? "Stream support advertised." : "This add-on does not advertise stream support.") +
                    (addon.SupportsCatalogs ? " Catalog support advertised." : string.Empty);
            }
            catch (Exception ex)
            {
                StatusText.Text = "Could not install this add-on: " + ex.Message;
            }
            finally
            {
                InstallButton.IsEnabled = true;
                DirectorySearchButton.IsEnabled = true;
            }
        }

        private async void DirectoryAttribution_Click(object sender, RoutedEventArgs e)
        {
            await Windows.System.Launcher.LaunchUriAsync(new Uri("https://stremio-addons.net/"));
        }

        private void ReloadInstalled()
        {
            var installed = _addonService.GetInstalled();
            if (installed.Count == 0)
            {
                // Do not assign an empty generic collection to the UWP ListView. Some
                // UWP/XAML projections reject this source before the empty state renders.
                AddonsList.ItemsSource = null;
                AddonsList.Visibility = Visibility.Collapsed;
                EmptyAddonsText.Visibility = Visibility.Visible;
                StatusText.Text = "No add-ons installed on this device yet.";
                return;
            }

            // Use a plain CLR List as the UWP ItemsSource. The WinRT XAML projection
            // can reject ObservableCollection<T> here with E_INVALIDARG for this model.
            // Avoid assigning ItemsSource entirely: this UWP XAML projection throws
            // E_INVALIDARG for both null and CLR collection sources in this page.
            // Populate the existing ListView directly with visual containers instead.
            AddonsList.ItemsSource = null;
            AddonsList.Items.Clear();
            foreach (var addon in installed)
            {
                var title = new TextBlock
                {
                    Text = addon.Name ?? "Unnamed add-on",
                    FontSize = 15,
                    FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                    TextWrapping = TextWrapping.Wrap
                };
                var description = new TextBlock
                {
                    Text = addon.Description ?? string.Empty,
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 169, 181, 209)),
                    TextWrapping = TextWrapping.Wrap
                };
                var remove = new Button
                {
                    Content = "Remove",
                    Tag = addon.ManifestUrl,
                    MinWidth = 90,
                    MinHeight = 40,
                    Background = new SolidColorBrush(Color.FromArgb(255, 66, 32, 51)),
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 216, 225)),
                    VerticalAlignment = VerticalAlignment.Center
                };
                remove.Click += RemoveButton_Click;
                var row = new Grid { ColumnSpacing = 10, Background = new SolidColorBrush(Color.FromArgb(255, 16, 31, 58)), Padding = new Thickness(10) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var text = new StackPanel { Spacing = 3 };
                text.Children.Add(title);
                text.Children.Add(description);
                text.Children.Add(new TextBlock { Text = addon.Capabilities + " · " + addon.Catalogs.Count + " catalog(s) · " + (addon.SyncStatus ?? string.Empty), FontSize = 11, Foreground = new SolidColorBrush(Color.FromArgb(255, 117, 229, 211)), TextWrapping = TextWrapping.Wrap });
                Grid.SetColumn(text, 0);
                Grid.SetColumn(remove, 1);
                row.Children.Add(text);
                row.Children.Add(remove);
                AddonsList.Items.Add(new ListViewItem { Content = row, HorizontalContentAlignment = HorizontalAlignment.Stretch });
            }
            EmptyAddonsText.Visibility = Visibility.Collapsed;
            AddonsList.Visibility = Visibility.Visible;
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