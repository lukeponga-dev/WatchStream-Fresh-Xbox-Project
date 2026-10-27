using System;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using WatchStream.Platform.Xbox;

namespace WatchStream.Xbox
{
    public sealed partial class PlayerPage : Page
    {
        private readonly XboxMediaPlayer _player;

        public PlayerPage()
        {
            this.InitializeComponent();
            _player = new XboxMediaPlayer();
            PlayerElement.SetMediaPlayer(_player.GetMediaPlayer());
        }

        protected override async void OnNavigatedTo(Windows.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            if (e.Parameter is string streamUrl)
            {
                LoadingOverlay.Visibility = Visibility.Visible;
                try
                {
                    await _player.PlayStreamAsync(streamUrl);
                }
                catch (Exception)
                {
                    // Handle playback failure (unsupported codec, etc)
                }
                finally
                {
                    LoadingOverlay.Visibility = Visibility.Collapsed;
                }
            }
        }

        protected override void OnNavigatedFrom(Windows.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            _player.Stop();
            base.OnNavigatedFrom(e);
        }
    }
}
