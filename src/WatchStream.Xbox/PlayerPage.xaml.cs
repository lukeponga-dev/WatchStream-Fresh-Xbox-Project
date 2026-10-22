using System;
using Windows.Media.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using WatchStream.Xbox.Services;

namespace WatchStream.Xbox
{
    public sealed partial class PlayerPage : Page
    {
        private StreamSource? _source;

        public PlayerPage() { InitializeComponent(); }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _source = e.Parameter as StreamSource;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (_source == null || !Uri.TryCreate(_source.Url, UriKind.Absolute, out var uri))
            {
                PlayerStatus.Text = "No valid stream URL was supplied.";
                return;
            }

            PlayerTitle.Text = _source.Name;
            PlayerSubtitle.Text = _source.AddonName;
            PlayerStatus.Text = "Opening the selected HTTP(S) stream. Playback depends on Xbox codec and stream compatibility.";
            try
            {
                VideoPlayer.Source = MediaSource.CreateFromUri(uri);
            }
            catch (Exception ex)
            {
                PlayerStatus.Text = "Unable to open this stream: " + ex.Message;
            }
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
            base.OnNavigatedFrom(e);
        }
    }
}