using System;
using System.Threading.Tasks;
using Windows.Foundation;
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
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
                throw new ArgumentException("Playback requires a valid HTTP or HTTPS stream URL.", nameof(url));

            var opened = new TaskCompletionSource<bool>();
            TypedEventHandler<MediaPlayer, object> openedHandler = (sender, args) => opened.TrySetResult(true);
            TypedEventHandler<MediaPlayer, MediaPlayerFailedEventArgs> failedHandler = (sender, args) =>
                opened.TrySetException(new InvalidOperationException(string.IsNullOrWhiteSpace(args.ErrorMessage)
                    ? "The Xbox media player could not open this stream." : args.ErrorMessage));
            _mediaPlayer.MediaOpened += openedHandler;
            _mediaPlayer.MediaFailed += failedHandler;
            try
            {
                _mediaPlayer.Source = Windows.Media.Core.MediaSource.CreateFromUri(uri);
                var completed = await Task.WhenAny(opened.Task, Task.Delay(TimeSpan.FromSeconds(25)));
                if (completed != opened.Task)
                    throw new TimeoutException("The Xbox media player did not open the stream within 25 seconds.");
                await opened.Task;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Playback Error: {ex.Message}");
                throw;
            }
            finally
            {
                _mediaPlayer.MediaOpened -= openedHandler;
                _mediaPlayer.MediaFailed -= failedHandler;
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
