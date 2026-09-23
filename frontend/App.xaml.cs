using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace NeedleDrop
{
    public partial class App : Application
    {
        /// <summary>
        /// Without this, ANY unhandled exception on the UI thread — a bad
        /// network response, a resource lookup, anything — makes WPF hang
        /// the window and then hard-crash the whole process, which is
        /// exactly what "freezes up and practically crashes" looks like
        /// from the outside. This won't fix whatever the underlying bug is,
        /// but it turns a silent freeze/crash into a message box that shows
        /// the real exception, so the game keeps running and the actual
        /// cause is visible instead of guessed at.
        /// </summary>
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            DispatcherUnhandledException += (s, ex) =>
            {
                // A MessageBox shown with NO owner window can, on some
                // multi-monitor/DPI setups, appear off-screen or on a
                // different monitor while still being fully modal — which
                // blocks all input to the main window without ever looking
                // like a dialog is open. That's indistinguishable from a
                // frozen app. Explicitly owning it by the main window (when
                // one exists and is loaded) keeps it positioned relative to
                // a window you can actually see.
                var owner = GetSafeOwnerWindow();
                if (owner is not null)
                    MessageBox.Show(owner, $"Something went wrong, but Needle Drop should keep running:\n\n{ex.Exception}",
                        "Needle Drop — unexpected error", MessageBoxButton.OK, MessageBoxImage.Warning);
                else
                    MessageBox.Show($"Something went wrong, but Needle Drop should keep running:\n\n{ex.Exception}",
                        "Needle Drop — unexpected error", MessageBoxButton.OK, MessageBoxImage.Warning);
                ex.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
            {
                var exObj = ex.ExceptionObject as Exception;
                var owner = GetSafeOwnerWindow();
                if (owner is not null)
                    MessageBox.Show(owner, $"An unrecoverable error occurred:\n\n{exObj}",
                        "Needle Drop — fatal error", MessageBoxButton.OK, MessageBoxImage.Error);
                else
                    MessageBox.Show($"An unrecoverable error occurred:\n\n{exObj}",
                        "Needle Drop — fatal error", MessageBoxButton.OK, MessageBoxImage.Error);
            };

            TaskScheduler.UnobservedTaskException += (s, ex) =>
            {
                // A background Task's exception nobody awaited — log it via
                // Debug output rather than a message box, since these fire
                // off the UI thread and aren't necessarily fatal.
                System.Diagnostics.Debug.WriteLine($"Unobserved task exception: {ex.Exception}");
                ex.SetObserved();
            };
        }

        /// <summary>
        /// MainWindow can be null (very early startup) or not yet visible
        /// (IsLoaded false) — passing either as a MessageBox owner throws or
        /// doesn't help, so this falls back to unowned only in those cases.
        /// </summary>
        private static Window? GetSafeOwnerWindow()
        {
            var main = Current?.MainWindow;
            return main is { IsLoaded: true, IsVisible: true } ? main : null;
        }
    }
}
