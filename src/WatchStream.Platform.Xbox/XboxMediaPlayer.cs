using System;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.UI.Xaml.Controls;
using ABI.Windows.Media.Core;

namespace WatchStream.Platform.Xbox
{
    public interface IXboxMediaPlayer
    {
        Task PlayStreamAsync(string url);
        void Stop();
        void Seek(TimeSpan position);
        void SetVolume(double volume);
    }

    public class XboxMediaPlayer : IXboxMediaPlayer
    {
        private readonly MediaPlayer _mediaPlayer;

        public XboxMediaPlayer()
        {
            _mediaPlayer = new MediaPlayer();
            _mediaPlayer.AutoPlay = true;
        }

        public async Task PlayStreamAsync(string url)
        {
            try
            {
                var source = Windows.Media.Core.MediaSource.CreateFromUri(new Uri(url));
                _mediaPlayer.Source = source;
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Playback Error: {ex.Message}");
                throw;
            }
        }

        public void Stop()
        {
            _mediaPlayer.Source = null;
        }

        public void Seek(TimeSpan position)
        {
            _mediaPlayer.Position = position;
        }

        public void SetVolume(double volume)
        {
            _mediaPlayer.Volume = volume;
        }

        // Exposed for the UI PlayerPage to bind the video surface
        public MediaPlayer GetMediaPlayer() => _mediaPlayer;
    }
}
