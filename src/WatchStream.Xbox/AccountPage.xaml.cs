using System;
using System.Threading;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using WatchStream.Core.Contracts;
using WatchStream.Xbox.Services;

namespace WatchStream.Xbox
{
    public sealed partial class AccountPage : Page
    {
        private readonly IStremioAccountService _accountService =
            new StremioAccountService(new SecureSessionStore());

        public AccountPage() { InitializeComponent(); }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var state = await _accountService.GetSessionStateAsync(CancellationToken.None);
                ShowSignedIn(state == AccountSessionState.SignedIn);
                if (state == AccountSessionState.SignedOut) EmailBox.Focus(FocusState.Programmatic);
            }
            catch (Exception)
            {
                ShowSignedIn(false);
                StatusText.Text = "Couldn't read the saved session. You can sign in again.";
            }
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
                StatusText.Text = "Signed out. The saved session was cleared.";
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
                EmailBox.Focus(FocusState.Programmatic);
            }
        }

        private void ShowSignedIn(bool signedIn)
        {
            SignedOutPanel.Visibility = signedIn ? Visibility.Collapsed : Visibility.Visible;
            SignedInPanel.Visibility = signedIn ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SetBusy(bool busy)
        {
            SignInButton.IsEnabled = !busy;
            BusyRing.IsActive = busy;
            BusyRing.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        }

        private void HomeButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame != null && Frame.CanGoBack) Frame.GoBack();
            else Frame?.Navigate(typeof(MainPage));
        }
    }
}
