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

using System;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using TheArtOfDev.HtmlRenderer.Adapters;
using TheArtOfDev.HtmlRenderer.Adapters.Entities;
using TheArtOfDev.HtmlRenderer.Core.Utils;

namespace TheArtOfDev.HtmlRenderer.WinUI.Adapters
{
    /// <summary>
    /// Adapter for WinUI context menu for core.
    /// </summary>
    internal sealed class ContextMenuAdapter : RContextMenu
    {
        #region Fields and Consts

        /// <summary>
        /// the underline WinUI context menu
        /// </summary>
        private readonly MenuFlyout _contextMenu;

        #endregion

        /// <summary>
        /// Init.
        /// </summary>
        public ContextMenuAdapter()
        {
            _contextMenu = new MenuFlyout();
        }

        public override int ItemsCount
        {
            get { return _contextMenu.Items.Count; }
        }

        public override void AddDivider()
        {
            _contextMenu.Items.Add(new MenuFlyoutSeparator());
        }

        public override void AddItem(string text, bool enabled, EventHandler onClick)
        {
            ArgChecker.AssertArgNotNullOrEmpty(text, "text");
            ArgChecker.AssertArgNotNull(onClick, "onClick");

            var item = new MenuFlyoutItem { Text = text, IsEnabled = enabled };
            item.Click += (sender, args) => onClick(sender, EventArgs.Empty);
            _contextMenu.Items.Add(item);
        }

        public override void RemoveLastDivider()
        {
            if (_contextMenu.Items.Count > 0 && _contextMenu.Items[_contextMenu.Items.Count - 1] is MenuFlyoutSeparator)
                _contextMenu.Items.RemoveAt(_contextMenu.Items.Count - 1);
        }

        public override void Show(RControl parent, RPoint location)
        {
            var control = ((ControlAdapter)parent).Control;
            _contextMenu.ShowAt(control, new FlyoutShowOptions
            {
                Position = new Windows.Foundation.Point(location.X, location.Y),
                ShowMode = FlyoutShowMode.Standard,
            });
        }

        public override void Dispose()
        {
            _contextMenu.Hide();
            _contextMenu.Items.Clear();
        }
    }
}
