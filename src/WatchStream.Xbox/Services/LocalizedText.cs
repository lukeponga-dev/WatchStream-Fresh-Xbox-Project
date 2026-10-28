using System;
using Windows.ApplicationModel.Resources;

namespace WatchStream.Xbox.Services
{
    internal static class LocalizedText
    {
        private static readonly ResourceLoader Loader = ResourceLoader.GetForCurrentView();
        public static string Get(string key, string fallback)
        {
            try
            {
                var value = Loader.GetString(key);
                return string.IsNullOrWhiteSpace(value) ? fallback : value;
            }
            catch { return fallback; }
        }
    }
}
