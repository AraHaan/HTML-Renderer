// "Therefore those skilled at the unorthodox
// are infinite as heaven and earth,
// inexhaustible as the great rivers.
// When they come to an end,
// they begin again,
// like the days and months;
// they die and are reborn,
// like the four seasons."
//
// - Sun Tsu,
// "The Art of War"

using Microsoft.UI.Xaml;

namespace TheArtOfDev.HtmlRenderer.Demo.WinUI
{
    /// <summary>
    /// Minimal application entry point - hosts one <see cref="MainWindow"/> with a single
    /// <see cref="TheArtOfDev.HtmlRenderer.WinUI.HtmlPanel"/> rendering a hardcoded sample document, just
    /// enough to prove the WinUI 3 control renders real HTML end to end.
    /// </summary>
    public partial class App : Application
    {
        private Window _window;

        public App()
        {
            InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            _window = new MainWindow();
            _window.Activate();
        }
    }
}
