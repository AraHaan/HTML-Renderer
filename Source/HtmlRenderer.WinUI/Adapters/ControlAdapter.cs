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

using Microsoft.UI.Xaml.Controls;
using TheArtOfDev.HtmlRenderer.Adapters;
using TheArtOfDev.HtmlRenderer.Adapters.Entities;
using TheArtOfDev.HtmlRenderer.Core.Utils;

namespace TheArtOfDev.HtmlRenderer.WinUI.Adapters
{
    /// <summary>
    /// Adapter for WinUI Control for core.
    /// </summary>
    /// <remarks>
    /// Unlike WPF's <see cref="System.Windows.Input.Mouse"/> static queries, WinUI 3 has no queryable
    /// global mouse-button state - <see cref="HtmlControl"/> caches the last-known pressed/position state
    /// from its own <c>PointerPressed</c>/<c>PointerReleased</c>/<c>PointerMoved</c> handlers, and this
    /// adapter (constructed fresh per call, mirroring <c>HtmlRenderer.WPF</c>'s own <c>ControlAdapter</c>
    /// usage pattern) just reads those cached fields back off it.
    /// </remarks>
    internal sealed class ControlAdapter : RControl
    {
        /// <summary>
        /// the underline WinUI control.
        /// </summary>
        private readonly Control _control;

        /// <summary>
        /// Init.
        /// </summary>
        public ControlAdapter(Control control)
            : base(WinUIAdapter.Instance)
        {
            ArgChecker.AssertArgNotNull(control, "control");

            _control = control;
        }

        /// <summary>
        /// Get the underline WinUI control
        /// </summary>
        public Control Control
        {
            get { return _control; }
        }

        public override RPoint MouseLocation
        {
            get { return (_control as HtmlControl)?.LastPointerLocation ?? RPoint.Empty; }
        }

        public override bool LeftMouseButton
        {
            get { return (_control as HtmlControl)?.IsLeftButtonPressed ?? false; }
        }

        public override bool RightMouseButton
        {
            get { return (_control as HtmlControl)?.IsRightButtonPressed ?? false; }
        }

        public override void SetCursorDefault()
        {
            (_control as HtmlControl)?.SetCursorShape(HtmlControl.CursorShape.Default);
        }

        public override void SetCursorHand()
        {
            (_control as HtmlControl)?.SetCursorShape(HtmlControl.CursorShape.Hand);
        }

        public override void SetCursorIBeam()
        {
            (_control as HtmlControl)?.SetCursorShape(HtmlControl.CursorShape.IBeam);
        }

        public override void DoDragDropCopy(object dragDropData)
        {
            // WinUI 3 has no synchronous "start a drag now" API analogous to WPF's
            // System.Windows.DragDrop.DoDragDrop - a drag operation there is always initiated by the
            // framework via CanDrag/DragStarting on the source element in response to a user gesture, not
            // called imperatively from application code mid-selection. Wiring that up is a real, separate
            // piece of work (hooking DragStarting on the CanvasControl/UserControl and populating its
            // DataPackage from dragDropData) that is out of scope for this foundation skeleton - see the
            // implementation report's deviations section. Copy-to-clipboard (Ctrl+C) is unaffected, since
            // it goes through RAdapter.SetToClipboard, not this method.
        }

        public override void MeasureString(string str, RFont font, double maxWidth, out int charFit, out double charFitWidth)
        {
            using (var g = new GraphicsAdapter(WinUIAdapter.Instance.Device))
            {
                g.MeasureString(str, font, maxWidth, out charFit, out charFitWidth);
            }
        }

        public override void Invalidate()
        {
            (_control as HtmlControl)?.InvalidateCanvas();
        }
    }
}
