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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using TheArtOfDev.HtmlRenderer.Adapters.Entities;
using TheArtOfDev.HtmlRenderer.Core;
using TheArtOfDev.HtmlRenderer.Core.Entities;
using TheArtOfDev.HtmlRenderer.WinUI.Utilities;
using Windows.Foundation;

namespace TheArtOfDev.HtmlRenderer.WinUI
{
    /// <summary>
    /// Provides HTML rendering using the text property.<br/>
    /// WinUI 3 control that will render html content in it's client rectangle.<br/>
    /// The control will handle pointer and keyboard events on it to support html text selection,
    /// copy-paste and mouse clicks.<br/>
    /// <para>
    /// The major differential to use HtmlPanel or HtmlLabel is size and scrollbars.<br/>
    /// If the size of the control depends on the html content the HtmlLabel should be used.<br/>
    /// If the size is set by some kind of layout then HtmlPanel is more suitable, also shows scrollbars if the html contents is larger than the control client rectangle.<br/>
    /// </para>
    /// </summary>
    /// <remarks>
    /// WinUI 3's <see cref="Microsoft.UI.Xaml.Controls.Control"/> has no <c>OnRender(DrawingContext)</c>
    /// override point the way WPF's does, so a composed <see cref="CanvasControl"/> child replaces
    /// inheritance-based rendering - see <see cref="OnCanvasDraw"/>. WinUI 3 also has no
    /// <c>EventManager.RegisterRoutedEvent</c> equivalent for user-defined bubbling events, so unlike
    /// <c>HtmlRenderer.WPF</c>'s <c>HtmlControl</c>, whose events are custom <see cref="RoutedEvent"/>s
    /// wrapped in a generic <c>RoutedEventArgs&lt;T&gt;</c>, this control's events are plain
    /// <see cref="EventHandler{TEventArgs}"/> (that wrapper type is dropped entirely, not ported).
    /// </remarks>
    public class HtmlControl : UserControl
    {
        /// <summary>
        /// The cursor shapes this control can be asked to show over the rendered html.
        /// </summary>
        internal enum CursorShape
        {
            Default,
            Hand,
            IBeam,
        }

        #region Fields and Consts

        /// <summary>
        /// How close together (in milliseconds) two pointer presses must land to be treated as a double-click.
        /// </summary>
        private const double DoubleClickThresholdMs = 500;

        /// <summary>
        /// How close together (in DIPs) two pointer presses must land to be treated as a double-click.
        /// </summary>
        private const double DoubleClickThresholdPixels = 4;

        /// <summary>
        /// Underline html container instance.
        /// </summary>
        protected readonly HtmlContainer _htmlContainer;

        /// <summary>
        /// The composed canvas child that all drawing happens on.
        /// </summary>
        protected readonly CanvasControl _canvas;

        /// <summary>
        /// The root panel hosting <see cref="_canvas"/> (and, for <see cref="HtmlPanel"/>, its scrollbars).
        /// </summary>
        /// <remarks>
        /// WPF's own <c>HtmlPanel</c> adds its scrollbars as raw visual children via
        /// <c>FrameworkElement.AddVisualChild</c>/<c>GetVisualChild</c>, a WPF-only low-level visual-tree
        /// API with no WinUI 3 equivalent - <see cref="Microsoft.UI.Xaml.UIElement"/> has no public/protected
        /// extensibility point for adding a child that isn't part of an actual XAML content tree. A real
        /// <see cref="Grid"/> panel is used here instead: still a mechanical, idiomatic-WinUI substitute for
        /// the same "extra chrome children alongside the canvas" need, not a behavioral rewrite.
        /// </remarks>
        protected readonly Grid _rootPanel;

        /// <summary>
        /// the base stylesheet data used in the control
        /// </summary>
        protected CssData _baseCssData;

        /// <summary>
        /// The last position of the scrollbars to know if it has changed to update mouse
        /// </summary>
        protected Point _lastScrollOffset;

        /// <summary>
        /// The dispatcher queue captured at construction time, used to marshal <see cref="HtmlContainer"/>
        /// events (which can fire on a thread-pool thread - see <see cref="HtmlContainerInt"/>'s async
        /// operations) back onto the UI thread, mirroring WPF's <c>CheckAccess()</c>/<c>Dispatcher.Invoke</c>
        /// double-dispatch pattern.
        /// </summary>
        private readonly DispatcherQueue _dispatcherQueue;

        /// <summary>
        /// Tracks the in-flight <see cref="SetTextAsync"/> call, if any, so a newer call can supersede an
        /// older one still awaiting <see cref="HtmlContainer.SetHtml"/>.
        /// </summary>
        private CancellationTokenSource _pendingLoad;

        private RPoint _lastPointerLocation;
        private bool _isLeftButtonPressed;
        private bool _isRightButtonPressed;

        private DateTime _lastPointerPressedTime = DateTime.MinValue;
        private Point _lastPointerPressedPosition;

        #endregion

        #region Dependency properties

        public static readonly DependencyProperty AvoidImagesLateLoadingProperty = DependencyProperty.Register(nameof(AvoidImagesLateLoading), typeof(bool), typeof(HtmlControl), new PropertyMetadata(false, OnDependencyProperty_valueChanged));
        public static readonly DependencyProperty IsSelectionEnabledProperty = DependencyProperty.Register(nameof(IsSelectionEnabled), typeof(bool), typeof(HtmlControl), new PropertyMetadata(true, OnDependencyProperty_valueChanged));
        public static readonly DependencyProperty IsContextMenuEnabledProperty = DependencyProperty.Register(nameof(IsContextMenuEnabled), typeof(bool), typeof(HtmlControl), new PropertyMetadata(true, OnDependencyProperty_valueChanged));
        public static readonly DependencyProperty BaseStylesheetProperty = DependencyProperty.Register(nameof(BaseStylesheet), typeof(string), typeof(HtmlControl), new PropertyMetadata(null, OnDependencyProperty_valueChanged));
        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(HtmlControl), new PropertyMetadata(null, OnDependencyProperty_valueChanged));

        #endregion

        /// <summary>
        /// Creates a new HtmlControl and sets a basic css for it's styling.
        /// </summary>
        protected HtmlControl()
        {
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            _rootPanel = new Grid();
            _canvas = new CanvasControl
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };
            _canvas.Draw += OnCanvasDraw;
            _rootPanel.Children.Add(_canvas);
            Content = _rootPanel;

            _htmlContainer = new HtmlContainer();
            _htmlContainer.LoadComplete += OnLoadCompleteInternal;
            _htmlContainer.LinkClicked += OnLinkClickedInternal;
            _htmlContainer.RenderError += OnRenderErrorInternal;
            _htmlContainer.Refresh += OnRefreshInternal;
            _htmlContainer.StylesheetLoad += OnStylesheetLoadInternal;
            _htmlContainer.ImageLoad += OnImageLoadInternal;
        }

        /// <summary>
        /// Raised when the set html document has been fully loaded.<br/>
        /// Allows manipulation of the html dom, scroll position, etc.
        /// </summary>
        public event EventHandler LoadComplete;

        /// <summary>
        /// Raised when the user clicks on a link in the html.<br/>
        /// Allows canceling the execution of the link.
        /// </summary>
        public event EventHandler<HtmlLinkClickedEventArgs> LinkClicked;

        /// <summary>
        /// Raised when an error occurred during html rendering.<br/>
        /// </summary>
        public event EventHandler<HtmlRenderErrorEventArgs> RenderError;

        /// <summary>
        /// Raised when a stylesheet is about to be loaded by file path or URI by link element.<br/>
        /// </summary>
        public event EventHandler<HtmlStylesheetLoadEventArgs> StylesheetLoad;

        /// <summary>
        /// Raised when an image is about to be loaded by file path or URI.<br/>
        /// </summary>
        public event EventHandler<HtmlImageLoadEventArgs> ImageLoad;

        /// <summary>
        /// Gets or sets a value indicating if image loading only when visible should be avoided (default - false).
        /// </summary>
        public bool AvoidImagesLateLoading
        {
            get { return (bool)GetValue(AvoidImagesLateLoadingProperty); }
            set { SetValue(AvoidImagesLateLoadingProperty, value); }
        }

        /// <summary>
        /// Is content selection is enabled for the rendered html (default - true).
        /// </summary>
        public bool IsSelectionEnabled
        {
            get { return (bool)GetValue(IsSelectionEnabledProperty); }
            set { SetValue(IsSelectionEnabledProperty, value); }
        }

        /// <summary>
        /// Is the build-in context menu enabled and will be shown on mouse right click (default - true)
        /// </summary>
        public bool IsContextMenuEnabled
        {
            get { return (bool)GetValue(IsContextMenuEnabledProperty); }
            set { SetValue(IsContextMenuEnabledProperty, value); }
        }

        /// <summary>
        /// Set base stylesheet to be used by html rendered in the panel.
        /// </summary>
        public string BaseStylesheet
        {
            get { return (string)GetValue(BaseStylesheetProperty); }
            set { SetValue(BaseStylesheetProperty, value); }
        }

        /// <summary>
        /// Gets or sets the text of this panel
        /// </summary>
        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        /// <summary>
        /// Get the currently selected text segment in the html.
        /// </summary>
        public virtual string SelectedText
        {
            get { return _htmlContainer.SelectedText; }
        }

        /// <summary>
        /// Copy the currently selected html segment with style.
        /// </summary>
        public virtual string SelectedHtml
        {
            get { return _htmlContainer.SelectedHtml; }
        }

        /// <summary>
        /// The last known pointer location relative to this control, as tracked from its own pointer
        /// events - WinUI 3 has no queryable global pointer-button state the way WPF's
        /// <see cref="System.Windows.Input.Mouse"/> does, so <see cref="Adapters.ControlAdapter"/> reads
        /// this cached value instead.
        /// </summary>
        internal RPoint LastPointerLocation
        {
            get { return _lastPointerLocation; }
        }

        /// <summary>
        /// Whether the left pointer button was down as of the last pointer event this control observed.
        /// </summary>
        internal bool IsLeftButtonPressed
        {
            get { return _isLeftButtonPressed; }
        }

        /// <summary>
        /// Whether the right pointer button was down as of the last pointer event this control observed.
        /// </summary>
        internal bool IsRightButtonPressed
        {
            get { return _isRightButtonPressed; }
        }

        /// <summary>
        /// Get html from the current DOM tree with inline style.
        /// </summary>
        public virtual string GetHtml()
        {
            return _htmlContainer != null ? _htmlContainer.GetHtml() : null;
        }

        /// <summary>
        /// Get the rectangle of html element as calculated by html layout.
        /// </summary>
        public virtual Rect? GetElementRectangle(string elementId)
        {
            return _htmlContainer != null ? _htmlContainer.GetElementRectangle(elementId) : null;
        }

        /// <summary>
        /// Clear the current selection.
        /// </summary>
        public void ClearSelection()
        {
            _htmlContainer?.ClearSelection();
        }

        /// <summary>
        /// Set the cursor shown over this control's rendered html.
        /// </summary>
        /// <remarks>
        /// <see cref="UIElement.ProtectedCursor"/> is a protected member - it can only be set from within
        /// a <see cref="UIElement"/> subclass's own code, unlike WPF's public <c>Control.Cursor</c>
        /// setter, so <see cref="Adapters.ControlAdapter"/> (an external class) calls through this method
        /// rather than setting a cursor property directly.
        /// </remarks>
        internal void SetCursorShape(CursorShape shape)
        {
            InputSystemCursorShape cursorShape;
            switch (shape)
            {
                case CursorShape.Hand:
                    cursorShape = InputSystemCursorShape.Hand;
                    break;
                case CursorShape.IBeam:
                    cursorShape = InputSystemCursorShape.IBeam;
                    break;
                default:
                    cursorShape = InputSystemCursorShape.Arrow;
                    break;
            }
            ProtectedCursor = InputSystemCursor.Create(cursorShape);
        }

        /// <summary>
        /// Invalidate the composed canvas, causing a repaint.
        /// </summary>
        internal void InvalidateCanvas()
        {
            _canvas?.Invalidate();
        }

        #region Protected methods

        /// <summary>
        /// Perform paint of the html in the control.
        /// </summary>
        private void OnCanvasDraw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            var g = args.DrawingSession;
            var renderSize = new Size(sender.ActualWidth, sender.ActualHeight);

            if (Background is SolidColorBrush backgroundBrush && backgroundBrush.Opacity > 0)
                g.FillRectangle(new Rect(0, 0, renderSize.Width, renderSize.Height), backgroundBrush.Color);

            var borderThickness = BorderThickness;
            if (borderThickness != new Thickness(0) && BorderBrush is SolidColorBrush borderColorBrush)
            {
                var color = borderColorBrush.Color;
                if (borderThickness.Top > 0)
                    g.FillRectangle(new Rect(0, 0, renderSize.Width, borderThickness.Top), color);
                if (borderThickness.Bottom > 0)
                    g.FillRectangle(new Rect(0, renderSize.Height - borderThickness.Bottom, renderSize.Width, borderThickness.Bottom), color);
                if (borderThickness.Left > 0)
                    g.FillRectangle(new Rect(0, 0, borderThickness.Left, renderSize.Height), color);
                if (borderThickness.Right > 0)
                    g.FillRectangle(new Rect(renderSize.Width - borderThickness.Right, 0, borderThickness.Right, renderSize.Height), color);
            }

            var htmlWidth = HtmlWidth(renderSize);
            var htmlHeight = HtmlHeight(renderSize);
            if (_htmlContainer != null && htmlWidth > 0 && htmlHeight > 0)
            {
                var clip = new Rect(Padding.Left + borderThickness.Left, Padding.Top + borderThickness.Top, htmlWidth, htmlHeight);
                using (g.CreateLayer(1f, clip))
                {
                    _htmlContainer.Location = new Point(Padding.Left + borderThickness.Left, Padding.Top + borderThickness.Top);
                    _htmlContainer.PerformPaint(g, clip);
                }

                if (!_lastScrollOffset.Equals(_htmlContainer.ScrollOffset))
                {
                    _lastScrollOffset = _htmlContainer.ScrollOffset;
                    InvokeMouseMove();
                }
            }
        }

        /// <summary>
        /// Handle pointer move to handle hover cursor and text selection.
        /// </summary>
        protected override void OnPointerMoved(PointerRoutedEventArgs e)
        {
            base.OnPointerMoved(e);
            UpdatePointerState(e);
            _htmlContainer?.HandleMouseMove(this, Utils.Convert(_lastPointerLocation));
        }

        /// <summary>
        /// Handle pointer exit to handle cursor change.
        /// </summary>
        protected override void OnPointerExited(PointerRoutedEventArgs e)
        {
            base.OnPointerExited(e);
            _htmlContainer?.HandleMouseLeave(this);
        }

        /// <summary>
        /// Handle pointer press to handle selection, and hand-rolled double-click detection.
        /// </summary>
        protected override void OnPointerPressed(PointerRoutedEventArgs e)
        {
            base.OnPointerPressed(e);
            UpdatePointerState(e);

            var point = e.GetCurrentPoint(this).Position;
            var now = DateTime.UtcNow;
            var isDoubleClick = (now - _lastPointerPressedTime).TotalMilliseconds <= DoubleClickThresholdMs
                                 && Distance(point, _lastPointerPressedPosition) <= DoubleClickThresholdPixels;

            if (isDoubleClick)
            {
                // Reset so a third rapid click starts a fresh detection window rather than chaining.
                _lastPointerPressedTime = DateTime.MinValue;
                _htmlContainer?.HandleMouseDoubleClick(this, e);
            }
            else
            {
                _lastPointerPressedTime = now;
                _lastPointerPressedPosition = point;
                _htmlContainer?.HandleMouseDown(this, e);
            }
        }

        /// <summary>
        /// Handle pointer release to handle selection and link click.
        /// </summary>
        protected override void OnPointerReleased(PointerRoutedEventArgs e)
        {
            base.OnPointerReleased(e);
            UpdatePointerState(e);
            _htmlContainer?.HandleMouseUp(this, e);
        }

        /// <summary>
        /// Handle key down event for selection, copy and scrollbars handling.
        /// </summary>
        protected override void OnKeyDown(KeyRoutedEventArgs e)
        {
            base.OnKeyDown(e);
            _htmlContainer?.HandleKeyDown(this, e);
        }

        /// <summary>
        /// Propagate the LoadComplete event from root container.
        /// </summary>
        protected virtual void OnLoadComplete(EventArgs e)
        {
            LoadComplete?.Invoke(this, e);
        }

        /// <summary>
        /// Propagate the LinkClicked event from root container.
        /// </summary>
        protected virtual void OnLinkClicked(HtmlLinkClickedEventArgs e)
        {
            LinkClicked?.Invoke(this, e);
        }

        /// <summary>
        /// Propagate the Render Error event from root container.
        /// </summary>
        protected virtual void OnRenderError(HtmlRenderErrorEventArgs e)
        {
            RenderError?.Invoke(this, e);
        }

        /// <summary>
        /// Propagate the stylesheet load event from root container.
        /// </summary>
        protected virtual void OnStylesheetLoad(HtmlStylesheetLoadEventArgs e)
        {
            StylesheetLoad?.Invoke(this, e);
        }

        /// <summary>
        /// Propagate the image load event from root container.
        /// </summary>
        protected virtual void OnImageLoad(HtmlImageLoadEventArgs e)
        {
            ImageLoad?.Invoke(this, e);
        }

        /// <summary>
        /// Handle html renderer invalidate and re-layout as requested.
        /// </summary>
        protected virtual void OnRefresh(HtmlRefreshEventArgs e)
        {
            if (e.Layout)
                InvalidateMeasure();
            _canvas?.Invalidate();
        }

        /// <summary>
        /// Get the width the HTML has to render in (not including vertical scroll iff it is visible)
        /// </summary>
        protected virtual double HtmlWidth(Size size)
        {
            return size.Width - Padding.Left - Padding.Right - BorderThickness.Left - BorderThickness.Right;
        }

        /// <summary>
        /// Get the height the HTML has to render in (not including horizontal scroll iff it is visible)
        /// </summary>
        protected virtual double HtmlHeight(Size size)
        {
            return size.Height - Padding.Top - Padding.Bottom - BorderThickness.Top - BorderThickness.Bottom;
        }

        /// <summary>
        /// call mouse move to handle paint after scroll or html change affecting mouse cursor.
        /// </summary>
        protected virtual void InvokeMouseMove()
        {
            _htmlContainer.HandleMouseMove(this, Utils.Convert(_lastPointerLocation));
        }

        /// <summary>
        /// Sets the html of this control and awaits the async load - the real entry point behind the
        /// <see cref="TextProperty"/>/<see cref="BaseStylesheetProperty"/> dependency-property callbacks.
        /// </summary>
        public async Task SetTextAsync(string html, CancellationToken cancellationToken = default)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _pendingLoad?.Cancel();
            _pendingLoad = cts;

            try
            {
                await _htmlContainer.SetHtml(html, _baseCssData);
            }
            catch (Exception ex)
            {
                if (cts == _pendingLoad)
                {
                    OnRenderError(new HtmlRenderErrorEventArgs(HtmlRenderErrorType.General, "Failed to set html", ex));
                }
                return;
            }

            if (cts != _pendingLoad || cts.IsCancellationRequested)
            {
                return;
            }

            InvalidateMeasure();
            _canvas?.Invalidate();
            InvokeMouseMove();
        }

        #endregion

        #region Private methods

        private void UpdatePointerState(PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(this);
            _isLeftButtonPressed = point.Properties.IsLeftButtonPressed;
            _isRightButtonPressed = point.Properties.IsRightButtonPressed;
            _lastPointerLocation = Utils.Convert(point.Position);
        }

        private static double Distance(Point a, Point b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Handle when dependency property value changes to update the underline HtmlContainer with the new value.
        /// </summary>
        private static void OnDependencyProperty_valueChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            var control = dependencyObject as HtmlControl;
            if (control != null)
            {
                var htmlContainer = control._htmlContainer;
                if (e.Property == AvoidImagesLateLoadingProperty)
                {
                    htmlContainer.AvoidImagesLateLoading = (bool)e.NewValue;
                }
                else if (e.Property == IsSelectionEnabledProperty)
                {
                    htmlContainer.IsSelectionEnabled = (bool)e.NewValue;
                }
                else if (e.Property == IsContextMenuEnabledProperty)
                {
                    htmlContainer.IsContextMenuEnabled = (bool)e.NewValue;
                }
                else if (e.Property == BaseStylesheetProperty)
                {
                    var baseCssData = HtmlRender.ParseStyleSheet((string)e.NewValue);
                    control._baseCssData = baseCssData;
                    _ = control.SetTextAsync(control.Text);
                }
                else if (e.Property == TextProperty)
                {
                    htmlContainer.ScrollOffset = new Point(0, 0);
                    _ = control.SetTextAsync((string)e.NewValue);
                }
            }
        }

        #region Private event handlers (marshal onto the UI thread)

        private void OnLoadCompleteInternal(object sender, EventArgs e)
        {
            if (_dispatcherQueue.HasThreadAccess)
                OnLoadComplete(e);
            else
                _dispatcherQueue.TryEnqueue(() => OnLoadComplete(e));
        }

        private void OnLinkClickedInternal(object sender, HtmlLinkClickedEventArgs e)
        {
            if (_dispatcherQueue.HasThreadAccess)
                OnLinkClicked(e);
            else
                _dispatcherQueue.TryEnqueue(() => OnLinkClicked(e));
        }

        private void OnRenderErrorInternal(object sender, HtmlRenderErrorEventArgs e)
        {
            if (_dispatcherQueue.HasThreadAccess)
                OnRenderError(e);
            else
                _dispatcherQueue.TryEnqueue(() => OnRenderError(e));
        }

        private void OnStylesheetLoadInternal(object sender, HtmlStylesheetLoadEventArgs e)
        {
            if (_dispatcherQueue.HasThreadAccess)
                OnStylesheetLoad(e);
            else
                _dispatcherQueue.TryEnqueue(() => OnStylesheetLoad(e));
        }

        private void OnImageLoadInternal(object sender, HtmlImageLoadEventArgs e)
        {
            if (_dispatcherQueue.HasThreadAccess)
                OnImageLoad(e);
            else
                _dispatcherQueue.TryEnqueue(() => OnImageLoad(e));
        }

        private void OnRefreshInternal(object sender, HtmlRefreshEventArgs e)
        {
            if (_dispatcherQueue.HasThreadAccess)
                OnRefresh(e);
            else
                _dispatcherQueue.TryEnqueue(() => OnRefresh(e));
        }

        #endregion

        #endregion
    }
}
