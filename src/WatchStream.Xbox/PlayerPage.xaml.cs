using System;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using WatchStream.Xbox.Services;

namespace WatchStream.Xbox
{
    public sealed partial class PlayerPage : Page
    {
        private StreamSource? _source;

        public PlayerPage()
        {
            InitializeComponent();
            VideoPlayer.MediaPlayer.MediaOpened += MediaPlayer_MediaOpened;
            VideoPlayer.MediaPlayer.MediaFailed += MediaPlayer_MediaFailed;
            VideoPlayer.MediaPlayer.MediaEnded += MediaPlayer_MediaEnded;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _source = e.Parameter as StreamSource;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (_source == null || !Uri.TryCreate(_source.Url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            {
                PlayerStatus.Text = "No valid HTTP(S) stream URL was supplied.";
                return;
            }

            PlayerTitle.Text = _source.Name;
            PlayerSubtitle.Text = string.IsNullOrWhiteSpace(_source.Description)
                ? _source.AddonName
                : _source.AddonName + "  •  " + _source.Description;
            PlayerStatus.Text = "Connecting to stream… Xbox will report whether the media source opens or fails.";
            try
            {
                VideoPlayer.Source = MediaSource.CreateFromUri(uri);
            }
            catch (Exception)
            {
                PlayerStatus.Text = "Xbox couldn't create a media source for this URL. Try another stream.";
            }
        }

        private void MediaPlayer_MediaOpened(Windows.Media.Playback.MediaPlayer sender, object args)
        {
            PlayerStatus.Text = "Media opened successfully. Confirm video and audio playback; codec and stream compatibility can vary by Xbox model.";
        }

        private void MediaPlayer_MediaFailed(Windows.Media.Playback.MediaPlayer sender, MediaPlayerFailedEventArgs args)
        {
            PlayerStatus.Text = "Xbox playback failed (" + args.Error.ToString() + "). " +
                (string.IsNullOrWhiteSpace(args.ErrorMessage) ? "Try a different stream or format." : args.ErrorMessage);
        }

        private void MediaPlayer_MediaEnded(Windows.Media.Playback.MediaPlayer sender, object args)
        {
            PlayerStatus.Text = "Playback ended.";
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            VideoPlayer.Source = null;
            if (Frame != null && Frame.CanGoBack) Frame.GoBack();
            else Frame?.Navigate(typeof(MainPage));
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            VideoPlayer.Source = null;
            VideoPlayer.MediaPlayer.MediaOpened -= MediaPlayer_MediaOpened;
            VideoPlayer.MediaPlayer.MediaFailed -= MediaPlayer_MediaFailed;
            VideoPlayer.MediaPlayer.MediaEnded -= MediaPlayer_MediaEnded;
            base.OnNavigatedFrom(e);
        }
    }
}