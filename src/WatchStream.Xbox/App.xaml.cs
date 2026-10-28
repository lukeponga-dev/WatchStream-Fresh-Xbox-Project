using System;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace WatchStream.Xbox
{
    public sealed partial class App : Windows.UI.Xaml.Application
    {
        public App()
        {
            InitializeComponent();
            RequiresPointerMode = ApplicationRequiresPointerMode.WhenRequested;
            Suspending += OnSuspending;

            // Capture the full exception rather than only the generated XAML event-handler frame.
            // Do not mark arbitrary unhandled exceptions as handled: continuing after an unknown
            // XAML failure can leave navigation and page state corrupted.
            this.UnhandledException += (s, e) =>
            {
                var details = "[WATCHSTREAM UNHANDLED EXCEPTION]\r\n" + e.Exception;
                System.Diagnostics.Debug.WriteLine(details);
                try
                {
                    var folder = Windows.Storage.ApplicationData.Current.LocalFolder;
                    var path = System.IO.Path.Combine(folder.Path, "watchstream-crash.log");
                    System.IO.File.AppendAllText(path, DateTimeOffset.UtcNow.ToString("O") + "\r\n" + details + "\r\n\r\n");
                }
                catch (Exception logException)
                {
                    System.Diagnostics.Debug.WriteLine("[WATCHSTREAM CRASH LOG FAILED] " + logException);
                }
                e.Handled = false;
            };
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            if (Window.Current.Content is not Frame rootFrame)
            {
                rootFrame = new Frame();
                rootFrame.NavigationFailed += OnNavigationFailed;
                SystemNavigationManager.GetForCurrentView().BackRequested += OnBackRequested;
                if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
                {
                    // Restore persisted app state here when implemented.
                }
                Window.Current.Content = rootFrame;
            }

            if (!e.PrelaunchActivated)
            {
                if (rootFrame.Content == null) rootFrame.Navigate(typeof(MainPage), e.Arguments);
                Window.Current.Activate();
            }
        }

        // Xbox controller B/back navigates one page when possible. On Home it is consumed
        // instead of unexpectedly closing the app.
        private void OnBackRequested(object sender, BackRequestedEventArgs e)
        {
            e.Handled = true;
            if (Window.Current.Content is not Frame rootFrame) return;

            // Overlay and page navigation are separate layers: dismiss the overlay before
            // touching the navigation stack.
            if (rootFrame.Content is MainPage homePage && homePage.TryCloseNavigation())
                return;

            if (rootFrame.CanGoBack)
            {
                rootFrame.GoBack();
                return;
            }

            // No prior page exists. Keep the app open and let the current page handle Back.
            if (rootFrame.Content is PlayerPage playerPage)
                playerPage.HandleBack();
            else if (rootFrame.Content is DetailsPage detailsPage)
                detailsPage.HandleBack();
        }

        private void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception($"Failed to load page '{e.SourcePageType.FullName}'.");
        }

        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            deferral.Complete();
        }
    }
}