using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace WatchStream.Xbox
{
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            InitializeComponent();
        }

        private async void ExploreButton_Click(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "Foundation is running. Next: verify Xbox deployment, then add the account and add-on service adapters.";

            var dialog = new ContentDialog
            {
                Title = "WatchStream foundation",
                Content = "This is the first UI shell. It does not sign in, browse real catalogs, or play media yet.",
                CloseButtonText = "Got it",
                DefaultButton = ContentDialogButton.Close
            };

            await dialog.ShowAsync();
        }
    }
}
