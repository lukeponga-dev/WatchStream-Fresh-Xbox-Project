using System;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
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
        private bool _pairingPageOpened;
        private static readonly HttpClient FacebookHttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };

        public AccountPage()
        {
            InitializeComponent();
        }

        private async void AccountPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (_currentLink == null && !_creatingLink)
                await CreateLinkAsync();
        }

        private async void FacebookButton_Click(object sender, RoutedEventArgs e)
        {
            FacebookButton.IsEnabled = false;
            _pollingCancellation?.Cancel();
            StatusText.Text = Services.LocalizedText.Get("Account_OpeningFacebook", "Opening Stremio Facebook sign-in...");
            try
            {
                var credentials = await AcquireFacebookCredentialsAsync(CancellationToken.None);
                StatusText.Text = Services.LocalizedText.Get("Account_FinishingSignIn", "Finishing sign-in with Stremio...");
                _linkService ??= new StremioLinkService(_sessionStore);
                if (await _linkService.CompleteFacebookSignInAsync(credentials.Email, credentials.Password, CancellationToken.None))
                {
                    StatusText.Text = Services.LocalizedText.Get("Account_SignedIn", "Signed in. Loading WatchStream...");
                    Frame?.Navigate(typeof(MainPage));
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                StatusText.Text = ex.Message;
            }
            finally
            {
                FacebookButton.IsEnabled = true;
            }
        }

        private sealed class FacebookCredentials
        {
            public string Email { get; init; } = string.Empty;
            public string Password { get; init; } = string.Empty;
        }

        /// <summary>
        /// Mirrors Stremio Web's official useFacebookLogin flow. Stremio hosts the
        /// Facebook OAuth integration; the Xbox client never needs its own Meta App ID.
        /// </summary>
        private static async Task<FacebookCredentials> AcquireFacebookCredentialsAsync(CancellationToken cancellationToken)
        {
            var stateBytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(stateBytes);
            var state = BitConverter.ToString(stateBytes).Replace("-", string.Empty).ToLowerInvariant();
            var loginUri = new Uri("https://www.strem.io/login-fb/" + Uri.EscapeDataString(state));
            if (!await Launcher.LaunchUriAsync(loginUri))
                throw new InvalidOperationException(Services.LocalizedText.Get("Account_OpenFailed", "Couldn't open Stremio's Facebook sign-in page."));

            const int maxTries = 25;
            for (var attempt = 0; attempt < maxTries; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    using (var response = await FacebookHttpClient.GetAsync(
                        "https://www.strem.io/login-fb-get-acc/" + Uri.EscapeDataString(state), cancellationToken))
                    {
                        if (response.IsSuccessStatusCode)
                        {
                            using (var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
                            {
                                if (document.RootElement.TryGetProperty("user", out var user) &&
                                    user.ValueKind == JsonValueKind.Object &&
                                    TryReadJsonString(user, "email", out var email) &&
                                    TryReadJsonString(user, "fbLoginToken", out var password))
                                    return new FacebookCredentials { Email = email, Password = password };
                            }
                        }
                    }
                }
                catch (HttpRequestException) { /* transient network issue; retry like Stremio Web */ }
                catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { /* request timeout; retry */ }

                if (attempt < maxTries - 1)
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }

            throw new InvalidOperationException(Services.LocalizedText.Get("Account_FacebookTimeout", "Facebook sign-in wasn't completed in time. Return to WatchStream and try again."));
        }

        private static bool TryReadJsonString(JsonElement element, string property, out string value)
        {
            value = string.Empty;
            if (!element.TryGetProperty(property, out var candidate) || candidate.ValueKind != JsonValueKind.String)
                return false;
            value = candidate.GetString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(value);
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
            PairingCodeText.Text = "LOADINGâ€¦";
            StatusText.Text = "Requesting a secure account-link code from Stremioâ€¦";
            _currentLink = null;
            _pairingPageOpened = false;

            try
            {
                _linkService ??= new StremioLinkService(_sessionStore);
                var link = await _linkService.CreateLinkAsync(cancellationToken);
                if (cancellationToken.IsCancellationRequested) return;

                _currentLink = link;
                PairingCodeText.Text = link.Code.ToUpperInvariant();
                RenderQrPayload(link.QrCode);
                PairingLinkButton.Content = "Open Stremio pairing page";
                StatusText.Text = Services.LocalizedText.Get("Account_LinkInstructions", "Open the pairing page, sign in to Stremio, and approve this device. Then return here and select the button again to finish linking.");
            }
            catch (OperationCanceledException) { }
            catch (Exception)
            {
                PairingCodeText.Text = "UNAVAILABLE";
                StatusText.Text = Services.LocalizedText.Get("Account_ConnectionFailed", "Couldn't reach Stremio. Check your connection and try again.");
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
                            StatusText.Text = "Account connected. Loading WatchStreamâ€¦";
                            Frame?.Navigate(typeof(MainPage));
                        });
                        return;
                    }

                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        if (!cancellationToken.IsCancellationRequested)
                            StatusText.Text = "Waiting for approval on your phone or computerâ€¦";
                    });
                }
                catch (OperationCanceledException) { return; }
                catch (StremioLinkException ex)
                {
                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        if (!cancellationToken.IsCancellationRequested)
                            StatusText.Text = ex.Message;
                    });
                    return;
                }
                catch (Exception)
                {
                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        if (!cancellationToken.IsCancellationRequested)
                            StatusText.Text = "Stremio is temporarily unreachable. Retrying automaticallyâ€¦";
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
            if (_currentLink == null || _linkService == null)
            {
                await CreateLinkAsync();
                return;
            }

            if (!_pairingPageOpened)
            {
                var target = _currentLink.Link;
                if (!Uri.TryCreate(target, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
                    uri = new Uri("https://link.stremio.com/");

                var launched = await Launcher.LaunchUriAsync(uri);
                if (launched)
                {
                    _pairingPageOpened = true;
                    PairingLinkButton.Content = "I've approved â€” finish linking";
                    StatusText.Text = "Complete sign-in and approve the device in the browser. Return to WatchStream and select â€œI've approved â€” finish linkingâ€.";
                }
                else
                {
                    StatusText.Text = "Couldn't open the pairing page. Open the displayed link on another device, approve it, then select the button again.";
                    _pairingPageOpened = true;
                    PairingLinkButton.Content = "I've approved â€” finish linking";
                }
                return;
            }

            PairingLinkButton.IsEnabled = false;
            StatusText.Text = "Checking the approved pairing code with Stremioâ€¦";
            try
            {
                if (await _linkService.TryCompleteLinkAsync(_currentLink.Code, _pollingCancellation?.Token ?? CancellationToken.None))
                {
                    StatusText.Text = "Account connected. Loading WatchStreamâ€¦";
                    Frame?.Navigate(typeof(MainPage));
                }
                else
                {
                    StatusText.Text = "Stremio hasn't confirmed approval yet. Finish approving the device in your browser, then try again.";
                }
            }
            catch (OperationCanceledException) { }
            catch (StremioLinkException ex)
            {
                StatusText.Text = ex.Message;
            }
            catch (Exception)
            {
                StatusText.Text = "Stremio couldn't finish linking. Confirm approval and try again.";
            }
            finally
            {
                PairingLinkButton.IsEnabled = true;
            }
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
