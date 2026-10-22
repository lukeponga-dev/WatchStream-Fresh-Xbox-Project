using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using WatchStream.Core.Contracts;
using WatchStream.Xbox.Services;

namespace WatchStream.Xbox
{
    public sealed partial class AccountPage : Page
    {
        private readonly ISessionStore _sessionStore = new SecureSessionStore();
        private readonly IStremioAccountService _accountService;
        private readonly StremioLinkService _linkService;
        private CancellationTokenSource? _linkCancellation;
        private string? _activeCode;
        private bool _showingQr = true;

        public AccountPage()
        {
            InitializeComponent();
            _accountService = new StremioAccountService(_sessionStore);
            _linkService = new StremioLinkService(_sessionStore);
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var state = await _accountService.GetSessionStateAsync(CancellationToken.None);
                ShowSignedIn(state == AccountSessionState.SignedIn);
                if (state == AccountSessionState.SignedOut)
                    await CreateAndShowLinkAsync();
            }
            catch (Exception)
            {
                ShowSignedIn(false);
                StatusText.Text = "Couldn't check the saved session or create a link. Check your connection and try again.";
            }
        }

        private async Task CreateAndShowLinkAsync()
        {
            _linkCancellation?.Cancel();
            _linkCancellation?.Dispose();
            _linkCancellation = new CancellationTokenSource();
            var token = _linkCancellation.Token;
            _activeCode = null;
            RefreshLinkButton.IsEnabled = false;
            SetBusy(true);
            StatusText.Text = "Requesting a secure Stremio link…";
            try
            {
                var link = await _linkService.CreateLinkAsync(token);
                _activeCode = link.Code;
                LinkCodeText.Text = link.Code;
                LinkUrlText.Text = link.Link;
                ShowQrMarkup(link.QrCode, link.Link);
                StatusText.Text = "Waiting for approval on your phone. This screen will update automatically.";
                RefreshLinkButton.IsEnabled = true;
                SetBusy(false);
                _ = PollForApprovalAsync(link.Code, token);
            }
            catch (OperationCanceledException) { }
            catch (Exception)
            {
                StatusText.Text = "Couldn't create a link. Check your internet connection, then request a new code.";
                LinkCodeText.Text = "Unavailable";
                RefreshLinkButton.IsEnabled = true;
                SetBusy(false);
            }
        }

        private async Task PollForApprovalAsync(string code, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(3), token);
                    if (await _linkService.TryCompleteLinkAsync(code, token))
                    {
                        ShowSignedIn(true);
                        StatusText.Text = "Stremio account linked successfully.";
                        _linkCancellation?.Cancel();
                        return;
                    }
                }
                catch (OperationCanceledException) { return; }
                catch (Exception)
                {
                    StatusText.Text = "Waiting for approval. If the link expired, request a new code.";
                }
            }
        }

        private void ShowQrMarkup(string qrValue, string link)
        {
            // Stremio supplies the QR image payload. Keep the image inside the Xbox WebView bounds.
            var qr = WebUtility.HtmlEncode(qrValue);
            var fallback = WebUtility.HtmlEncode(link);
            var html = "<html><head><meta name='viewport' content='width=device-width, initial-scale=1'></head>" +
                       "<body style='margin:0;padding:0;background:white;display:flex;align-items:center;justify-content:center;width:100%;height:100%;overflow:hidden'>" +
                       "<img alt='Stremio account link QR code' style='display:block;width:144px;height:144px;max-width:100%;max-height:100%;object-fit:contain' src='" + qr + "' " +
                       "onerror=\"this.style.display='none';document.getElementById('fallback').style.display='block'\"/>" +
                       "<div id='fallback' style='display:none;color:#111;text-align:center;padding:8px;font:13px sans-serif'>QR unavailable. Open " + fallback + "</div>" +
                       "</body></html>";
            QrWebView.NavigateToString(html);
        }

        private async void RefreshLinkButton_Click(object sender, RoutedEventArgs e)
        {
            await CreateAndShowLinkAsync();
        }

        private void UseEmailButton_Click(object sender, RoutedEventArgs e)
        {
            _showingQr = false;
            QrPanel.Visibility = Visibility.Collapsed;
            SignedOutPanel.Visibility = Visibility.Visible;
            StatusText.Text = "Email/password sign-in sends your credentials only to Stremio over HTTPS.";
            EmailBox.Focus(FocusState.Programmatic);
        }

        private void UseQrButton_Click(object sender, RoutedEventArgs e)
        {
            _showingQr = true;
            SignedOutPanel.Visibility = Visibility.Collapsed;
            QrPanel.Visibility = Visibility.Visible;
            if (string.IsNullOrWhiteSpace(_activeCode))
                _ = CreateAndShowLinkAsync();
            else
                StatusText.Text = "Scan the QR code or enter the displayed code on link.stremio.com.";
        }

        private async void SignInButton_Click(object sender, RoutedEventArgs e)
        {
            var email = EmailBox.Text;
            var password = PasswordBox.Password;
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
            {
                StatusText.Text = "Enter your Stremio email and password.";
                return;
            }

            SetBusy(true);
            StatusText.Text = "Signing in securely…";
            try
            {
                await _accountService.SignInAsync(email, password, CancellationToken.None);
                ShowSignedIn(true);
                StatusText.Text = "Connected. Your password was not saved.";
                _linkCancellation?.Cancel();
            }
            catch (InvalidOperationException ex)
            {
                StatusText.Text = ex.Message;
                ShowSignedIn(false);
            }
            catch (Exception)
            {
                StatusText.Text = "Sign-in couldn't complete. Check your internet connection and try again.";
                ShowSignedIn(false);
            }
            finally
            {
                PasswordBox.Password = string.Empty;
                SetBusy(false);
            }
        }

        private async void SignOutButton_Click(object sender, RoutedEventArgs e)
        {
            SetBusy(true);
            StatusText.Text = "Signing out…";
            try
            {
                await _accountService.SignOutAsync(CancellationToken.None);
                StatusText.Text = "Signed out. Requesting a new account link…";
            }
            catch (Exception)
            {
                StatusText.Text = "The local session was cleared. The remote session may expire separately.";
            }
            finally
            {
                PasswordBox.Password = string.Empty;
                ShowSignedIn(false);
                SetBusy(false);
                _ = CreateAndShowLinkAsync();
            }
        }

        private void ShowSignedIn(bool signedIn)
        {
            QrPanel.Visibility = signedIn ? Visibility.Collapsed : (_showingQr ? Visibility.Visible : Visibility.Collapsed);
            SignedOutPanel.Visibility = signedIn ? Visibility.Collapsed : (_showingQr ? Visibility.Collapsed : Visibility.Visible);
            SignedInPanel.Visibility = signedIn ? Visibility.Visible : Visibility.Collapsed;
            if (signedIn)
            {
                _linkCancellation?.Cancel();
                RefreshLinkButton.IsEnabled = false;
            }
        }

        private void SetBusy(bool busy)
        {
            SignInButton.IsEnabled = !busy;
            RefreshLinkButton.IsEnabled = !busy;
            BusyRing.IsActive = busy;
            BusyRing.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        }

        protected override void OnNavigatedFrom(Windows.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            _linkCancellation?.Cancel();
            _linkCancellation?.Dispose();
            _linkCancellation = null;
            base.OnNavigatedFrom(e);
        }

        private void HomeButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame != null && Frame.CanGoBack) Frame.GoBack();
            else Frame?.Navigate(typeof(MainPage));
        }
    }
}