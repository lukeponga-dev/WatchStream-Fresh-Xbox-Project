using System;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using WatchStream.Application.Interfaces;
using WatchStream.Infrastructure.Stremio;

namespace WatchStream.Xbox
{
    public sealed partial class AccountPage : Page
    {
        private bool _isPolling;
        private IStremioAccountService _authService;

        public AccountPage()
        {
            this.InitializeComponent();

            // In a real app, this would come from a DI container
            var httpClient = new System.Net.Http.HttpClient();
            _authService = new StremioAccountClient(httpClient);

            this.Loaded += AccountPage_Loaded;
        }

        private async void AccountPage_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var authRequest = await _authService.InitiateDeviceAuthAsync();
                PairingCodeText.Text = authRequest.UserCode.ToUpper();

                _isPolling = true;
                await PollForAuthAsync(authRequest.DeviceCode);
            }
            catch (Exception ex)
            {
                PairingCodeText.Text = "ERROR";
                // In a full app, we'd show a detailed error dialog here
            }
        }

        private async Task PollForAuthAsync(string deviceCode)
        {
            while (_isPolling)
            {
                var success = await _authService.PollForTokenAsync(deviceCode);
                if (success)
                {
                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        // Navigate back to home or show success state
                        Frame?.Navigate(typeof(MainPage));
                    });
                    break;
                }
                await Task.Delay(5000);
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            _isPolling = false;
            Frame?.Navigate(typeof(MainPage));
        }
    }
}
