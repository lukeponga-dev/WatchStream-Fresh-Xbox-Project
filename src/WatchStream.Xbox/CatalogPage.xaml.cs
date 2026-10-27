using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using WatchStream.Domain.Models;
using WatchStream.Application.Interfaces;
using WatchStream.Infrastructure.Stremio;

namespace WatchStream.Xbox
{
    public sealed partial class CatalogPage : Page
    {
        private readonly IStremioAddonService _addonService;
        private string _currentCategory;

        public CatalogPage()
        {
            this.InitializeComponent();

            // For now, using manual instantiation. Later this moves to DI.
            var httpClient = new System.Net.Http.HttpClient();
            _addonService = new StremioAddonClient(httpClient);

            this.Loaded += CatalogPage_Loaded;
        }

        protected override void OnNavigatedTo(Windows.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            if (e.Parameter is string category)
            {
                _currentCategory = category;
            }
            else if (e.Parameter is MediaItem item)
            {
                // Handle detail view transition
            }
            base.OnNavigatedTo(e);
        }

        private async void CatalogPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentCategory)) _currentCategory = "Discover";

            CategoryTitle.Text = _currentCategory;

            try
            {
                // In a real scenario, we'd iterate through all active addons.
                // For the spike, we use a known test addon.
                var items = await _addonService.GetCatalogAsync("community.stremio.cinema", _currentCategory.ToLower());
                MediaGrid.ItemsSource = items;
            }
            catch (Exception)
            {
                // Error handling
            }
        }

        private void MediaGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is MediaItem item)
            {
                // Navigate to Media Details page
                // Frame.Navigate(typeof(DetailsPage), item);
            }
        }
    }
}
