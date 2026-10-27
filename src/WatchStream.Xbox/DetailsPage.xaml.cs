using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Input;
using WatchStream.Domain.Models;
using WatchStream.Xbox.Services;
using DomainStreamSource = WatchStream.Domain.Models.StreamSource;

namespace WatchStream.Xbox
{
    public sealed partial class DetailsPage : Page
    {
        private readonly StremioAddonService _addonService = new StremioAddonService();
        private MediaItem? _currentMedia;
        private Button? _lastFocusedStreamButton;

        public DetailsPage()
        {
            InitializeComponent();
            NavigationCacheMode = Windows.UI.Xaml.Navigation.NavigationCacheMode.Required;
        }

        protected override void OnNavigatedTo(Windows.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is MediaItem item)
            {
                _currentMedia = item;
                _ = LoadMediaDetailsAsync();
            }

            if (_lastFocusedStreamButton != null)
                _lastFocusedStreamButton.Focus(FocusState.Programmatic);
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            PageTransition.PlayEntrance(this);
            if (_lastFocusedStreamButton != null)
                _lastFocusedStreamButton.Focus(FocusState.Programmatic);
        }

        private void StreamButton_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
                _lastFocusedStreamButton = button;
        }

        public void HandleBack()
        {
            if (Frame?.CanGoBack == true)
                Frame.GoBack();
        }

        private async Task LoadMediaDetailsAsync()
        {
            var media = _currentMedia;
            if (media == null) return;

            TitleText.Text = media.Name;
            DescriptionText.Text = string.IsNullOrWhiteSpace(media.Description)
                ? "No description available."
                : media.Description;
            YearText.Text = media.ReleaseDate?.Year.ToString() ?? media.Type.ToUpperInvariant();

            if (Uri.TryCreate(media.PosterUrl, UriKind.Absolute, out var posterUri) &&
                (posterUri.Scheme == Uri.UriSchemeHttps || posterUri.Scheme == Uri.UriSchemeHttp))
                PosterImage.Source = new BitmapImage(posterUri);
            else
                PosterImage.Source = null;

            StreamsStatusText.Text = "Looking for streams in your installed add-ons…";
            StreamsList.ItemsSource = null;

            try
            {
                var streams = await _addonService.ResolveStreamsAsync(media.Type, media.Id, CancellationToken.None);
                var mapped = streams.Select(stream => new DomainStreamSource(
                    Id: stream.Url,
                    Title: stream.Name,
                    Url: stream.Url,
                    Resolution: string.IsNullOrWhiteSpace(stream.Quality) ? stream.AddonName : stream.Quality,
                    Codec: stream.AddonName)).ToList();

                StreamsList.ItemsSource = mapped;
                StreamsStatusText.Text = mapped.Count == 0
                    ? "No streams were returned. Install a stream-enabled add-on from Add-ons and try again."
                    : mapped.Count + " stream source(s) found.";
            }
            catch (Exception ex)
            {
                StreamsStatusText.Text = ex.Message;
            }
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is DomainStreamSource source &&
                Uri.TryCreate(source.Url, UriKind.Absolute, out var streamUri) &&
                (streamUri.Scheme == Uri.UriSchemeHttps || streamUri.Scheme == Uri.UriSchemeHttp))
            {
                Frame?.Navigate(typeof(PlayerPage), source.Url, new SlideNavigationTransitionInfo
                {
                    Effect = SlideNavigationTransitionEffect.FromRight
                });
            }
        }
    }
}