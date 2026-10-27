using System;
using System.Globalization;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media.Imaging;

namespace WatchStream.Xbox.Converters
{
    /// <summary>
    /// Optimized image converter for Xbox to prevent OOM by using DecodePixelWidth.
    /// </summary>
    public class PosterImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is string url && !string.IsNullOrWhiteSpace(url))
            {
                if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
                {
                    var bitmap = new BitmapImage();
                    bitmap.UriSource = uri;

                    // Set DecodePixelWidth to a value appropriate for thumbnails (approx 200px)
                    // This drastically reduces memory usage on Xbox.
                    bitmap.DecodePixelWidth = 200;
                    bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;

                    return bitmap;
                }
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
