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

using Windows.ApplicationModel.DataTransfer;

namespace TheArtOfDev.HtmlRenderer.WinUI.Utilities
{
    /// <summary>
    /// Helper to set HTML fragment and plain text data to the clipboard.
    /// </summary>
    /// <remarks>
    /// <c>HtmlRenderer.WPF</c>'s own <see cref="System.Windows.DataObject"/>-based helper hand-builds the
    /// full CF_HTML header (<c>Version:</c>/<c>StartHTML:</c>/<c>EndHTML:</c>/<c>StartFragment:</c>/
    /// <c>EndFragment:</c> with byte offsets computed via manual UTF-8 byte counting) because WPF's own
    /// clipboard API has no higher-level "give me an HTML fragment, I'll build the header" helper.
    /// <para>
    /// The WinRT clipboard API does: <see cref="HtmlFormatHelper.CreateHtmlFormat"/> takes a plain HTML
    /// fragment string and returns it already wrapped in a correct CF_HTML header (offsets included), and
    /// <see cref="DataPackage.SetHtmlFormat"/> is documented to accept exactly that pre-built CF_HTML
    /// string. That makes WPF's entire byte-offset-arithmetic <c>GetHtmlDataString</c> implementation
    /// (StartFragment/EndFragment comment injection, UTF-8 byte counting, header back-patching)
    /// unnecessary here - confirmed during implementation per the plan's Step 6 note anticipating exactly
    /// this outcome.
    /// </para>
    /// </remarks>
    internal static class ClipboardHelper
    {
        /// <summary>
        /// Create a <see cref="DataPackage"/> with given html and plain-text ready to be used for clipboard
        /// or drag-drop operation.
        /// </summary>
        /// <param name="html">a html fragment</param>
        /// <param name="plainText">the plain text</param>
        public static DataPackage CreateDataObject(string html, string plainText)
        {
            var dataPackage = new DataPackage();
            dataPackage.SetHtmlFormat(HtmlFormatHelper.CreateHtmlFormat(html ?? string.Empty));
            dataPackage.SetText(plainText ?? string.Empty);
            return dataPackage;
        }

        /// <summary>
        /// Clears clipboard and sets the given HTML and plain text fragment to the clipboard.
        /// </summary>
        /// <param name="html">a html fragment</param>
        /// <param name="plainText">the plain text</param>
        public static void CopyToClipboard(string html, string plainText)
        {
            Clipboard.SetContent(CreateDataObject(html, plainText));
        }

        /// <summary>
        /// Clears clipboard and sets the given plain text fragment to the clipboard.
        /// </summary>
        /// <param name="plainText">the plain text</param>
        public static void CopyToClipboard(string plainText)
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(plainText ?? string.Empty);
            Clipboard.SetContent(dataPackage);
        }
    }
}
