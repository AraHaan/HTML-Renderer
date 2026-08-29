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

using Microsoft.Graphics.Canvas.Text;
using TheArtOfDev.HtmlRenderer.Adapters;

namespace TheArtOfDev.HtmlRenderer.WinUI.Adapters
{
    /// <summary>
    /// Adapter for Win2D/DirectWrite font family object for core.
    /// </summary>
    /// <remarks>
    /// Unlike WPF's <see cref="System.Windows.Media.FontFamily"/>, which is itself the object that must
    /// stay alive to keep a loaded font usable, Win2D/DirectWrite resolves a <see cref="CanvasTextFormat.FontFamily"/>
    /// string by name at layout-construction time. For an <c>@font-face</c> family that name only resolves
    /// while the backing <see cref="CanvasFontSet"/> that registered it is still alive - so this adapter
    /// holds a strong reference to that font set (null for ordinary system-installed families) purely to
    /// keep it from being garbage collected for as long as this <see cref="RFontFamily"/> handle is
    /// referenced (by <see cref="TheArtOfDev.HtmlRenderer.Core.Handlers.FontsHandler"/>'s own cache).
    /// </remarks>
    internal sealed class FontFamilyAdapter : RFontFamily
    {
        /// <summary>
        /// the family name, as resolved by DirectWrite at text-layout time.
        /// </summary>
        private readonly string _name;

        /// <summary>
        /// Strong reference to the backing font set for an <c>@font-face</c> family, keeping it alive for
        /// as long as this handle is referenced. Null for ordinary system-installed families.
        /// </summary>
        private readonly CanvasFontSet _fontSet;

        /// <summary>
        /// Init.
        /// </summary>
        public FontFamilyAdapter(string name, CanvasFontSet fontSet = null)
        {
            _name = name;
            _fontSet = fontSet;
        }

        /// <summary>
        /// the backing font set for an <c>@font-face</c> family, or null for an ordinary system family.
        /// </summary>
        public CanvasFontSet FontSet
        {
            get { return _fontSet; }
        }

        public override string Name
        {
            get { return _name; }
        }
    }
}
