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

using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.UI.Text;
using TheArtOfDev.HtmlRenderer.Adapters;
using TheArtOfDev.HtmlRenderer.Adapters.Entities;

namespace TheArtOfDev.HtmlRenderer.WinUI.Adapters
{
    /// <summary>
    /// Adapter for Win2D font.
    /// </summary>
    /// <remarks>
    /// Win2D/DirectWrite exposes no low-level per-glyph metrics table the way WPF's
    /// <see cref="System.Windows.Media.GlyphTypeface"/> does (see the plan's Step 1 spike risk #2), so
    /// <see cref="Height"/>/<see cref="UnderlineOffset"/> can't be read directly off the font the way
    /// <c>HtmlRenderer.WPF</c>'s own <c>FontAdapter</c> does (<c>FontFamily.LineSpacing</c>/
    /// <c>Typeface.UnderlinePosition</c>). Instead they're derived by laying out a representative probe
    /// string once at construction time and reading <see cref="CanvasTextLayout.LineMetrics"/> -
    /// <see cref="CanvasLineMetrics.Height"/> gives the line height directly, but there is no underline-
    /// position metric at all on Win2D's public surface, so <see cref="UnderlineOffset"/> is a documented
    /// heuristic (see below), not a measured value.
    /// </remarks>
    internal sealed class FontAdapter : RFont
    {
        #region Fields and Consts

        /// <summary>
        /// A representative probe string used to derive line metrics - includes an ascender-tall and a
        /// descender-tall glyph so the measured line height reflects a normal line of text, not just
        /// x-height characters.
        /// </summary>
        private const string ProbeString = "Ág";

        /// <summary>
        /// the underline Win2D text format.
        /// </summary>
        private readonly CanvasTextFormat _textFormat;

        /// <summary>
        /// the size of the font, in points (unscaled) - matches the unit <see cref="TheArtOfDev.HtmlRenderer.Adapters.RAdapter.CreateFont(string,double,RFontStyle)"/>
        /// receives it in, mirroring <c>HtmlRenderer.WPF</c>'s own <c>FontAdapter.Size</c>.
        /// </summary>
        private readonly double _size;

        /// <summary>
        /// Cached font height, in device-independent pixels (Win2D/WinUI's coordinate unit, same as WPF's).
        /// </summary>
        private readonly double _height;

        /// <summary>
        /// the vertical offset of the font underline location from the top of the font.
        /// </summary>
        private readonly double _underlineOffset;

        /// <summary>
        /// Cached font whitespace width.
        /// </summary>
        private double _whitespaceWidth = -1;

        #endregion

        /// <summary>
        /// Init.
        /// </summary>
        /// <param name="device">the device to build the probe layout with</param>
        /// <param name="fontFamily">the font family name (system-installed or an <c>@font-face</c> family registered with a live <see cref="Microsoft.Graphics.Canvas.Text.CanvasFontSet"/>)</param>
        /// <param name="size">the font size, in points</param>
        /// <param name="style">the font style</param>
        public FontAdapter(CanvasDevice device, string fontFamily, double size, RFontStyle style)
        {
            _size = size;

            _textFormat = new CanvasTextFormat
            {
                FontFamily = fontFamily,
                // 96/72 converts points -> device-independent pixels, matching WPF's own FontAdapter,
                // whose Height/UnderlineOffset bake in the same conversion factor and whose DrawString/
                // MeasureString multiply the raw font size by it at every use.
                FontSize = (float)(96d / 72d * size),
                FontWeight = (style & RFontStyle.Bold) == RFontStyle.Bold ? FontWeights.Bold : FontWeights.Normal,
                FontStyle = (style & RFontStyle.Italic) == RFontStyle.Italic ? Windows.UI.Text.FontStyle.Italic : Windows.UI.Text.FontStyle.Normal,
                // The core engine performs its own line-breaking/word-wrap over runs it measures one at a
                // time (see RGraphics.MeasureString) - every layout built from this format, for measuring
                // or for drawing, is expected to be a single unwrapped line.
                WordWrapping = CanvasWordWrapping.NoWrap,
            };

            using (var layout = new CanvasTextLayout(device, ProbeString, _textFormat, float.MaxValue, float.MaxValue))
            {
                var lineMetrics = layout.LineMetrics;
                if (lineMetrics.Length > 0)
                {
                    _height = lineMetrics[0].Height;
                    var baseline = lineMetrics[0].Baseline;
                    // No underline-position metric is exposed by Win2D's public surface - approximate it
                    // as a small step below the baseline, proportional to the line's descent region. This
                    // is a heuristic, not a measured value; see this class's own remarks.
                    _underlineOffset = baseline + (_height - baseline) * 0.15;
                }
                else
                {
                    _height = layout.LayoutBounds.Height;
                    _underlineOffset = _height * 0.85;
                }
            }
        }

        /// <summary>
        /// the underline Win2D text format.
        /// </summary>
        public CanvasTextFormat TextFormat
        {
            get { return _textFormat; }
        }

        public override double Size
        {
            get { return _size; }
        }

        public override double UnderlineOffset
        {
            get { return _underlineOffset; }
        }

        public override double Height
        {
            get { return _height; }
        }

        public override double LeftPadding
        {
            get { return _height / 6f; }
        }

        public override double GetWhitespaceWidth(RGraphics graphics)
        {
            if (_whitespaceWidth < 0)
            {
                _whitespaceWidth = graphics.MeasureString(" ", this).Width;
            }
            return _whitespaceWidth;
        }
    }
}
