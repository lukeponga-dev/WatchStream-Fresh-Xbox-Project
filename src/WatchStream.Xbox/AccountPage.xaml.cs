using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using WatchStream.Xbox.Services;

namespace WatchStream.Xbox
{
    public sealed partial class AccountPage : Page
    {
        private readonly SecureSessionStore _sessionStore = new SecureSessionStore();
        private StremioLinkService? _linkService;
        private CancellationTokenSource? _pollingCancellation;
        private StremioLinkCode? _currentLink;
        private bool _creatingLink;

        public AccountPage()
        {
            InitializeComponent();
        }

        private async void AccountPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (_currentLink == null && !_creatingLink)
                await CreateLinkAsync();
        }

        private async Task CreateLinkAsync()
        {
            if (_creatingLink) return;
            _creatingLink = true;
            _pollingCancellation?.Cancel();
            _pollingCancellation?.Dispose();
            _pollingCancellation = new CancellationTokenSource();
            var cancellationToken = _pollingCancellation.Token;

            RefreshButton.IsEnabled = false;
            PairingCodeText.Text = "LOADING…";
            StatusText.Text = "Requesting a secure account-link code from Stremio…";
            _currentLink = null;

            try
            {
                _linkService ??= new StremioLinkService(_sessionStore);
                var link = await _linkService.CreateLinkAsync(cancellationToken);
                if (cancellationToken.IsCancellationRequested) return;

                _currentLink = link;
                PairingCodeText.Text = link.Code.ToUpperInvariant();
                RenderQrPayload(link.QrCode);
                PairingLinkButton.Content = "Open Stremio pairing page";
                StatusText.Text = "Waiting for approval on your phone or computer. This screen will update automatically.";
                _ = PollForApprovalAsync(link.Code, cancellationToken);
            }
            catch (OperationCanceledException) { }
            catch (Exception)
            {
                PairingCodeText.Text = "UNAVAILABLE";
                StatusText.Text = "Couldn't reach Stremio. Check your connection and try again.";
            }
            finally
            {
                _creatingLink = false;
                RefreshButton.IsEnabled = true;
            }
        }

        private async Task PollForApprovalAsync(string code, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (_linkService != null && await _linkService.TryCompleteLinkAsync(code, cancellationToken))
                    {
                        await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                        {
                            StatusText.Text = "Account connected. Loading WatchStream…";
                            Frame?.Navigate(typeof(MainPage));
                        });
                        return;
                    }

                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        if (!cancellationToken.IsCancellationRequested)
                            StatusText.Text = "Waiting for approval on your phone or computer…";
                    });
                }
                catch (OperationCanceledException) { return; }
                catch (Exception)
                {
                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        if (!cancellationToken.IsCancellationRequested)
                            StatusText.Text = "Still waiting for Stremio. Retrying automatically…";
                    });
                }

                try { await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken); }
                catch (OperationCanceledException) { return; }
            }
        }

        private void RenderQrPayload(string payload)
        {
            PairingQrView.NavigateToString(string.Empty);
            QrCodeBorder.Visibility = Visibility.Collapsed;
            if (string.IsNullOrWhiteSpace(payload)) return;

            var safePayload = payload.Trim();
            var isImageData = safePayload.StartsWith("data:image/png;base64,", StringComparison.OrdinalIgnoreCase) ||
                              safePayload.StartsWith("data:image/jpeg;base64,", StringComparison.OrdinalIgnoreCase) ||
                              safePayload.StartsWith("data:image/svg+xml;base64,", StringComparison.OrdinalIgnoreCase);
            var isHttpsImageUri = Uri.TryCreate(safePayload, UriKind.Absolute, out var imageUri) &&
                                  imageUri.Scheme == Uri.UriSchemeHttps;
            if (!isImageData && !isHttpsImageUri) return;

            var encoded = System.Net.WebUtility.HtmlEncode(safePayload);
            var html = "<!doctype html><html><head><meta name='viewport' content='width=device-width,initial-scale=1'></head>" +
                       "<body style='margin:0;background:#fff;display:flex;align-items:center;justify-content:center;width:100vw;height:100vh'>" +
                       "<img alt='Stremio pairing QR code' src='" + encoded + "' style='width:100%;height:100%;object-fit:contain' /></body></html>";
            PairingQrView.NavigateToString(html);
            QrCodeBorder.Visibility = Visibility.Visible;
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await CreateLinkAsync();
        }

        private async void PairingLinkButton_Click(object sender, RoutedEventArgs e)
        {
            var target = _currentLink?.Link;
            if (!Uri.TryCreate(target, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
                uri = new Uri("https://link.stremio.com/");
            await Launcher.LaunchUriAsync(uri);
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            _pollingCancellation?.Cancel();
            if (Frame?.CanGoBack == true) Frame.GoBack();
            else Frame?.Navigate(typeof(MainPage));
        }

        protected override void OnNavigatedFrom(Windows.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            _pollingCancellation?.Cancel();
            base.OnNavigatedFrom(e);
        }
    }
}