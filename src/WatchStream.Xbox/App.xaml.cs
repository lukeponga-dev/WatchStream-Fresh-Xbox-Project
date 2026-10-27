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

            // Capture global crashes to identify the exact failure point
            this.UnhandledException += (s, e) =>
            {
                System.Diagnostics.Debug.WriteLine($"[CRITICAL CRASH] {e.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack: {e.Exception.StackTrace}");
                e.Handled = false; // Keep it crashing so we see it in the debugger, but we now have the log
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