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

using Microsoft.Graphics.Canvas.Brushes;
using TheArtOfDev.HtmlRenderer.Adapters;

namespace TheArtOfDev.HtmlRenderer.WinUI.Adapters
{
    /// <summary>
    /// Adapter for Win2D brushes.
    /// </summary>
    internal sealed class BrushAdapter : RBrush
    {
        /// <summary>
        /// The actual Win2D brush instance.
        /// </summary>
        private readonly ICanvasBrush _brush;

        /// <summary>
        /// Init.
        /// </summary>
        public BrushAdapter(ICanvasBrush brush)
        {
            _brush = brush;
        }

        /// <summary>
        /// The actual Win2D brush instance.
        /// </summary>
        public ICanvasBrush Brush
        {
            get { return _brush; }
        }

        /// <summary>
        /// No-op, matching <c>HtmlRenderer.WPF</c>'s own <c>BrushAdapter.Dispose</c> exactly.
        /// </summary>
        /// <remarks>
        /// <see cref="Core.Dom.CssBox.PaintBackground"/> calls <c>brush.Dispose()</c> unconditionally
        /// after every background fill, including for solid-color brushes obtained from
        /// <see cref="TheArtOfDev.HtmlRenderer.Adapters.RAdapter.GetSolidBrush"/> - which is a
        /// process-wide singleton CACHE (see that method's own doc comment: "cached... instance"),
        /// reused for the lifetime of the adapter, not a fresh per-call resource. WPF's <c>Brush</c>
        /// needs no real disposal, so that unconditional call is harmless there - it's the reason this
        /// pattern was never caught before. Win2D's <see cref="ICanvasBrush"/> is a real unmanaged/GPU
        /// resource: actually disposing it here permanently kills the shared cached brush for every
        /// color after its first use, so the SECOND time any element needs (for example) a white
        /// background, the disposed brush silently paints nothing - confirmed by reproducing exactly
        /// this symptom (background-color:white showing the page's own gradient background through
        /// instead) via two consecutive real <see cref="HtmlContainer.PerformPaint"/> calls against the
        /// same document. Not disposing here leaks gradient/texture brushes (the one kind of
        /// <see cref="RBrush"/> that genuinely is fresh-per-call, from
        /// <see cref="WinUIAdapter.CreateLinearGradientBrush"/>/<see cref="GraphicsAdapter.GetTextureBrush"/>)
        /// - accepted deliberately, mirroring WPF's identical no-op for its own gradient/texture
        /// <c>Brush</c> objects, since the alternative (silently breaking every cached solid-color fill
        /// after its first paint) is far worse than a small, bounded resource lifetime extension.
        /// </remarks>
        public override void Dispose()
        {
        }
    }
}
