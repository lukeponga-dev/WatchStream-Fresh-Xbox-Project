using System;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Input;
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

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            WatchStream.Xbox.Services.PageTransition.PlayEntrance(this);
        }

        private void PlayerBack_Click(object sender, RoutedEventArgs e) => HandleBack();

        protected override async void OnNavigatedTo(Windows.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is string streamUrl)
            {
                LoadingOverlay.Visibility = Visibility.Visible;
                PlaybackErrorPanel.Visibility = Visibility.Collapsed;
                try
                {
                    await _player.PlayStreamAsync(streamUrl);
                }
                catch (Exception)
                {
                    PlaybackErrorText.Text = Services.LocalizedText.Get("Player_Failed", "This stream could not be played. It may use an unsupported format or codec.");
                    PlaybackErrorPanel.Visibility = Visibility.Visible;
                }
                finally
                {
                    LoadingOverlay.Visibility = Visibility.Collapsed;
                }
            }
        }

        public void HandleBack()
        {
            if (Frame?.CanGoBack == true)
            {
                Frame.GoBack();
                return;
            }

            // If playback was launched directly, there is no details page to restore.
            Frame?.Navigate(typeof(MainPage), null, new SlideNavigationTransitionInfo
            {
                Effect = SlideNavigationTransitionEffect.FromLeft
            });
        }

        protected override void OnNavigatedFrom(Windows.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            _player.Stop();
            base.OnNavigatedFrom(e);
        }
    }
}
