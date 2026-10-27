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
    public sealed partial class DetailsPage : Page
    {
        private readonly IStremioAddonService _addonService;
        private MediaItem _currentMedia;

        public DetailsPage()
        {
            this.InitializeComponent();

            var httpClient = new System.Net.Http.HttpClient();
            _addonService = new StremioAddonClient(httpClient);
        }

        protected override async void OnNavigatedTo(Windows.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            if (e.Parameter is MediaItem item)
            {
                _currentMedia = item;
                LoadMediaDetails();
            }
        }

        private async void LoadMediaDetails()
        {
            TitleText.Text = _currentMedia.Name;
            PosterImage.Source = new Windows.UI.Xaml.Media.Imaging.BitmapImage(new Uri(_currentMedia.PosterUrl ?? ""));
            DescriptionText.Text = _currentMedia.Description ?? "No description available.";

            try
            {
                // Resolve streams for this specific media ID
                var streams = await _addonService.GetStreamsAsync(_currentMedia.Id);
                StreamsList.ItemsSource = streams;
            }
            catch (Exception)
            {
                // Handle error
            }
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is StreamSource source)
            {
                Frame?.Navigate(typeof(PlayerPage), source.Url);
            }
        }
    }
}
