using System;
using System.Threading;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using WatchStream.Xbox.Services;

namespace WatchStream.Xbox
{
    public sealed partial class ProfilePage : Page
    {
        private readonly StremioAccountService _accountService = new StremioAccountService(new SecureSessionStore());

        public ProfilePage() => InitializeComponent();

        private async void ProfilePage_Loaded(object sender, RoutedEventArgs e)
        {
            SignOutButton.IsEnabled = false;
            try
            {
                var profile = await _accountService.GetProfileAsync(CancellationToken.None);
                if (profile == null)
                {
                    ConnectionStatusText.Text = "Signed out";
                    StatusText.Text = "Your Stremio session is no longer valid. Connect your account again to sync your library and add-ons.";
                    DisplayNameText.Text = "Not signed in";
                    EmailText.Text = "No active Stremio account";
                    SignOutButton.Content = "Connect Stremio";
                    SignOutButton.IsEnabled = true;
                    return;
                }

                DisplayNameText.Text = profile.DisplayName;
                EmailText.Text = string.IsNullOrWhiteSpace(profile.Email) ? "Email not provided by Stremio" : profile.Email;
                ProfileInitials.Text = GetInitials(profile.DisplayName);
                ConnectionStatusText.Text = "Connected to Stremio";
                StatusText.Text = "Your account is connected. WatchStream uses the saved secure session to load your library and add-ons.";
                SignOutButton.IsEnabled = true;
            }
            catch (Exception)
            {
                ConnectionStatusText.Text = "Unable to verify";
                StatusText.Text = "Couldn't refresh your account details. Check your connection and try again.";
                SignOutButton.IsEnabled = true;
            }
        }

        private static string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "S";
            var parts = name.Trim().Split(new[] { ' ', '.', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1) return parts[0].Substring(0, 1).ToUpperInvariant();
            return (parts[0].Substring(0, 1) + parts[parts.Length - 1].Substring(0, 1)).ToUpperInvariant();
        }

        private async void SignOutButton_Click(object sender, RoutedEventArgs e)
        {
            if (SignOutButton.Content?.ToString() == "Connect Stremio")
            {
                Frame?.Navigate(typeof(AccountPage));
                return;
            }

            SignOutButton.IsEnabled = false;
            StatusText.Text = "Signing out of Stremio…";
            try
            {
                await _accountService.SignOutAsync(CancellationToken.None);
                Frame?.Navigate(typeof(MainPage));
            }
            catch (Exception)
            {
                // SignOutAsync clears the local vault in a finally block; return to Home either way.
                Frame?.Navigate(typeof(MainPage));
            }
        }

        private void HomeButton_Click(object sender, RoutedEventArgs e) => Frame?.Navigate(typeof(MainPage));
    }
}
