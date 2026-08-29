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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using TheArtOfDev.HtmlRenderer.Adapters;
using TheArtOfDev.HtmlRenderer.Adapters.Entities;
using TheArtOfDev.HtmlRenderer.Core.Network;
using TheArtOfDev.HtmlRenderer.WinUI.Utilities;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI.ViewManagement;

namespace TheArtOfDev.HtmlRenderer.WinUI.Adapters
{
    /// <summary>
    /// Adapter for WinUI 3 / Win2D platform.
    /// </summary>
    internal sealed class WinUIAdapter : RAdapter
    {
        #region Fields and Consts

        // One HttpClient shared for the adapter's (process) lifetime - see WpfAdapter's own field for why
        // this must be declared before _instance (static field initializer ordering).
        private static readonly HttpClient _sharedHttpClient = new HttpClient();

        /// <summary>
        /// Singleton instance of global adapter.
        /// </summary>
        private static readonly WinUIAdapter _instance = new WinUIAdapter();

        /// <summary>
        /// List of valid predefined color names in lower-case
        /// </summary>
        private static readonly List<string> ValidColorNamesLc;

        /// <summary>
        /// The shared Win2D device this whole adapter (and every control it services) draws with -
        /// created once via <see cref="CanvasDevice.GetSharedDevice"/> before any <see cref="CanvasControl"/>
        /// exists, confirmed usable that way by the plan's Step 1 spike.
        /// </summary>
        private readonly CanvasDevice _device;

        // Backs LoadFontFaceFontInt's temp-file registration - CanvasFontSet's documented in-memory story
        // is a file Uri, mirroring WPF's own Fonts.GetFontFamilies(Uri) constraint (see that class's own
        // remarks) - one directory per process, cleaned up by the OS's normal temp-file housekeeping.
        private static readonly string _fontFaceTempDirectory = CreateFontFaceTempDirectory();

        private static string CreateFontFaceTempDirectory()
        {
            var dir = Path.Combine(Path.GetTempPath(), "HtmlRenderer.FontFace." + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        #endregion

        static WinUIAdapter()
        {
            ValidColorNamesLc = new List<string>();
            foreach (var colorProp in typeof(Colors).GetProperties(BindingFlags.Public | BindingFlags.Static))
            {
                ValidColorNamesLc.Add(colorProp.Name.ToLower());
            }
        }

        /// <summary>
        /// Init installed font families and set default font families mapping.
        /// </summary>
        private WinUIAdapter()
        {
            _device = CanvasDevice.GetSharedDevice();

            // Interactive UI backend - fetching a real http(s): image or stylesheet out of the box is the
            // expected behavior, same reasoning as WpfAdapter's own constructor.
            NetworkLoader = new HttpClientNetworkLoader(_sharedHttpClient, (Uri)null);

            AddFontFamilyMapping("monospace", "Courier New");
            AddFontFamilyMapping("Helvetica", "Arial");

            try
            {
                var systemFonts = CanvasFontSet.GetSystemFontSet();
                var addedFamilies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var font in systemFonts.Fonts)
                {
                    var name = GetPreferredFamilyName(font.FamilyNames);
                    if (name != null && addedFamilies.Add(name))
                    {
                        try
                        {
                            AddFontFamily(new FontFamilyAdapter(name));
                        }
                        catch
                        {
                        }
                    }
                }
            }
            catch
            {
                // System font enumeration is a nice-to-have (helps IsFontExists recognize installed
                // families upfront); its failure shouldn't prevent the adapter itself from initializing.
            }

            var uiSettings = new UISettings();
            uiSettings.ColorValuesChanged += (sender, e) =>
            {
                var previous = _colorScheme;
                _colorScheme = null;
                if (previous.HasValue && previous.Value != SystemColorScheme)
                    OnColorSchemeChanged();
            };
        }

        /// <summary>
        /// Singleton instance of global adapter.
        /// </summary>
        public static WinUIAdapter Instance
        {
            get { return _instance; }
        }

        /// <summary>
        /// The shared Win2D device this adapter and every control it services draws with.
        /// </summary>
        public CanvasDevice Device
        {
            get { return _device; }
        }

        /// <summary>
        /// Rendering onto a Windows surface, so the document should follow the user's app theme.
        /// Cached and invalidated on a system preference change rather than read per query.
        /// </summary>
        public override RColorScheme SystemColorScheme
        {
            get
            {
                if (SystemColorSchemeOverride.HasValue)
                    return SystemColorSchemeOverride.Value;
                if (!_colorScheme.HasValue)
                    _colorScheme = WindowsTheme.GetAppsColorScheme();
                return _colorScheme.Value;
            }
        }

        /// <summary>
        /// Cached app theme; null when it needs to be re-read.
        /// </summary>
        private RColorScheme? _colorScheme;

        protected override RColor GetColorInt(string colorName)
        {
            if (!ValidColorNamesLc.Contains(colorName.ToLower()))
                return RColor.Empty;

            var prop = typeof(Colors).GetProperty(colorName, BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase);
            if (prop == null)
                return RColor.Empty;

            var color = (Windows.UI.Color)prop.GetValue(null);
            return Utilities.Utils.Convert(color);
        }

        protected override RPen CreatePen(RColor color)
        {
            return new PenAdapter(GetSolidColorBrush(color));
        }

        protected override RBrush CreateSolidBrush(RColor color)
        {
            return new BrushAdapter(GetSolidColorBrush(color));
        }

        protected override RBrush CreateLinearGradientBrush(RPoint p1, RPoint p2, (RColor Color, double Position)[] stops)
        {
            var gradientStops = new CanvasGradientStop[stops.Length];
            for (int i = 0; i < stops.Length; i++)
            {
                gradientStops[i] = new CanvasGradientStop
                {
                    Color = Utilities.Utils.Convert(stops[i].Color),
                    Position = (float)stops[i].Position,
                };
            }

            var brush = new CanvasLinearGradientBrush(_device, gradientStops);
            brush.StartPoint = new System.Numerics.Vector2((float)p1.X, (float)p1.Y);
            brush.EndPoint = new System.Numerics.Vector2((float)p2.X, (float)p2.Y);
            return new BrushAdapter(brush);
        }

        protected override RImage ConvertImageInt(object image)
        {
            return image is CanvasBitmap bitmap ? new ImageAdapter(bitmap) : null;
        }

        protected override RImage ImageFromStreamInt(Stream memoryStream)
        {
            var randomAccessStream = ToRandomAccessStreamAsync(memoryStream).GetAwaiter().GetResult();
            var bitmap = CanvasBitmap.LoadAsync(_device, randomAccessStream).AsTask().GetAwaiter().GetResult();
            return new ImageAdapter(bitmap);
        }

        protected override RFont CreateFontInt(string family, double size, RFontStyle style)
        {
            return new FontAdapter(_device, family, size, style);
        }

        protected override RFont CreateFontInt(RFontFamily family, double size, RFontStyle style)
        {
            return new FontAdapter(_device, ((FontFamilyAdapter)family).Name, size, style);
        }

        /// <summary>
        /// Loads one <c>@font-face</c> face's bytes as a Win2D <see cref="CanvasFontSet"/> via a temp file,
        /// mirroring <c>HtmlRenderer.WPF</c>'s own <c>WpfAdapter.LoadFontFaceFontInt</c> (same reasoning:
        /// there is no supported in-memory-only load path). Confirmed by the plan's Step 1 spike:
        /// <see cref="CanvasFontSet"/>'s constructor takes a single <see cref="Uri"/>, not an array.
        /// </summary>
        protected override RFontFamily LoadFontFaceFontInt(byte[] fontBytes, string filePath)
        {
            var faceDirectory = Path.Combine(_fontFaceTempDirectory, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(faceDirectory);
            var tempFilePath = Path.Combine(faceDirectory, "face.ttf");
            File.WriteAllBytes(tempFilePath, fontBytes);

            var fontSet = new CanvasFontSet(new Uri(tempFilePath));
            if (fontSet.Fonts.Count == 0)
                return null;

            var name = GetPreferredFamilyName(fontSet.Fonts[0].FamilyNames);
            return name != null ? new FontFamilyAdapter(name, fontSet) : null;
        }

        protected override object GetClipboardDataObjectInt(string html, string plainText)
        {
            return ClipboardHelper.CreateDataObject(html, plainText);
        }

        protected override void SetToClipboardInt(string text)
        {
            ClipboardHelper.CopyToClipboard(text);
        }

        protected override void SetToClipboardInt(string html, string plainText)
        {
            ClipboardHelper.CopyToClipboard(html, plainText);
        }

        protected override void SetToClipboardInt(RImage image)
        {
            var bitmap = ((ImageAdapter)image).Bitmap;
            var stream = new InMemoryRandomAccessStream();
            bitmap.SaveAsync(stream, CanvasBitmapFileFormat.Png).AsTask().GetAwaiter().GetResult();

            var dataPackage = new DataPackage();
            dataPackage.SetBitmap(RandomAccessStreamReference.CreateFromStream(stream));
            Clipboard.SetContent(dataPackage);
        }

        protected override RContextMenu CreateContextMenuInt()
        {
            return new ContextMenuAdapter();
        }

        /// <summary>
        /// Show a save-file dialog and encode the image to the chosen path.
        /// </summary>
        /// <remarks>
        /// An unpackaged WinUI 3 app's <see cref="FileSavePicker"/> needs an owner HWND handed to it via
        /// <see cref="WinRT.Interop.InitializeWithWindowAttribute"/>'s runtime counterpart - resolved here
        /// from the passed-in control's <c>XamlRoot</c> via <see cref="Win32Interop.GetWindowFromWindowId"/>,
        /// since this adapter (a process-wide singleton) has no <c>Window</c> reference of its own to fall
        /// back on. If no control is given, or its window can't be resolved, the save is silently skipped -
        /// this is a real, narrow gap versus WPF's parameterless <c>SaveFileDialog.ShowDialog()</c>; see the
        /// implementation report.
        /// </remarks>
        protected override void SaveToFileInt(RImage image, string name, string extension, RControl control = null)
        {
            var element = (control as ControlAdapter)?.Control;
            if (element?.XamlRoot == null)
                return;

            var hwnd = Win32Interop.GetWindowFromWindowId(element.XamlRoot.ContentIslandEnvironment.AppWindowId);
            if (hwnd == IntPtr.Zero)
                return;

            var picker = new FileSavePicker
            {
                SuggestedFileName = name,
                DefaultFileExtension = extension,
            };
            picker.FileTypeChoices.Add("Image", new List<string> { ".png", ".bmp", ".jpg", ".tif", ".gif" });
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = picker.PickSaveFileAsync().AsTask().GetAwaiter().GetResult();
            if (file == null)
                return;

            var format = GetBitmapFileFormat(Path.GetExtension(file.Path));
            var bitmap = ((ImageAdapter)image).Bitmap;
            using (var stream = file.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite).AsTask().GetAwaiter().GetResult())
            {
                bitmap.SaveAsync(stream, format).AsTask().GetAwaiter().GetResult();
            }
        }

        #region Private/Protected methods

        /// <summary>
        /// Get solid color brush for the given color.
        /// </summary>
        private CanvasSolidColorBrush GetSolidColorBrush(RColor color)
        {
            return new CanvasSolidColorBrush(_device, Utilities.Utils.Convert(color));
        }

        /// <summary>
        /// Pick a family name from a font's family-names table, preferring the "en-us" entry (matching
        /// WPF's own <c>XmlLanguage</c>-keyed preference in its <c>FontFamilyAdapter</c>) and falling back
        /// to whatever entry exists first.
        /// </summary>
        private static string GetPreferredFamilyName(IReadOnlyDictionary<string, string> familyNames)
        {
            if (familyNames == null || familyNames.Count == 0)
                return null;

            if (familyNames.TryGetValue("en-us", out var name))
                return name;

            foreach (var kvp in familyNames)
                return kvp.Value;

            return null;
        }

        /// <summary>
        /// Get the Win2D bitmap encoding format to use for the given file extension. Default is PNG.
        /// </summary>
        private static CanvasBitmapFileFormat GetBitmapFileFormat(string ext)
        {
            switch (ext.ToLower())
            {
                case ".jpg":
                case ".jpeg":
                    return CanvasBitmapFileFormat.Jpeg;
                case ".bmp":
                    return CanvasBitmapFileFormat.Bmp;
                case ".tif":
                case ".tiff":
                    return CanvasBitmapFileFormat.Tiff;
                case ".gif":
                    return CanvasBitmapFileFormat.Gif;
                default:
                    return CanvasBitmapFileFormat.Png;
            }
        }

        /// <summary>
        /// Copy a .NET <see cref="Stream"/> into an in-memory WinRT <see cref="IRandomAccessStream"/> -
        /// the shape Win2D's <see cref="CanvasBitmap.LoadAsync(ICanvasResourceCreator,IRandomAccessStream)"/>
        /// needs, since <see cref="RAdapter.ImageFromStreamInt"/>'s seam only ever gives this a plain
        /// .NET stream.
        /// </summary>
        private static async Task<IRandomAccessStream> ToRandomAccessStreamAsync(Stream stream)
        {
            byte[] bytes;
            using (var memory = new MemoryStream())
            {
                await stream.CopyToAsync(memory).ConfigureAwait(false);
                bytes = memory.ToArray();
            }

            var randomAccessStream = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(randomAccessStream.GetOutputStreamAt(0)))
            {
                writer.WriteBytes(bytes);
                await writer.StoreAsync();
                await writer.FlushAsync();
                writer.DetachStream();
            }

            randomAccessStream.Seek(0);
            return randomAccessStream;
        }

        #endregion
    }
}
