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
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TheArtOfDev.HtmlRenderer.Core.Entities;
using TheArtOfDev.HtmlRenderer.Demo.Common;
using Windows.Storage.Streams;

namespace TheArtOfDev.HtmlRenderer.Demo.WinUI
{
    /// <summary>
    /// Hosts a <see cref="TreeView"/> of the same showcase/test/performance samples the WPF and WinForms
    /// demos expose (via the shared, UI-framework-agnostic <see cref="SamplesLoader"/>/<see cref="HtmlSample"/>
    /// from <c>HtmlRenderer.Demo.Common</c>) next to a <see cref="TheArtOfDev.HtmlRenderer.WinUI.HtmlPanel"/>
    /// that renders whichever sample is selected.
    /// </summary>
    /// <remarks>
    /// Node content is a plain <see cref="string"/> (the sample/category name), matching Microsoft's own
    /// documented <c>RootNodes</c> pattern exactly (see
    /// https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/tree-view) - no
    /// <see cref="TreeView.ItemTemplate"/> is needed for that case, since a string displays directly. An
    /// earlier version wrapped each node's content in a custom object and bound it via
    /// <c>TreeView.ItemTemplate</c>'s <c>{Binding Name}</c>, which rendered no text and never expanded -
    /// switched to this simpler, documented shape instead of chasing the exact binding-context rules for
    /// that pattern. <see cref="_nodeToSample"/> maps each leaf node back to its <see cref="HtmlSample"/>
    /// since the node's own displayed Content is just its name.
    /// </remarks>
    public sealed partial class MainWindow : Window
    {
        private readonly Dictionary<TreeViewNode, HtmlSample> _nodeToSample = new Dictionary<TreeViewNode, HtmlSample>();

        public MainWindow()
        {
            InitializeComponent();

            Title = "HTML Renderer - WinUI 3 Demo";

            SamplesLoader.Init("WinUI", typeof(TheArtOfDev.HtmlRenderer.WinUI.HtmlRender).Assembly.GetName().Version.ToString());

            // Without this, showcase samples' <link rel="Stylesheet" href="StyleSheet"> never resolves, so
            // e.g. Intro.htm's `.whitehole { background-color:white; border-radius:10px }` class is simply
            // never applied at all - not a paint bug, the rule itself never loads. DemoUtils.OnStylesheetLoad
            // is the same platform-agnostic handler the WPF/WinForms demos use.
            RendererPanel.StylesheetLoad += DemoUtils.OnStylesheetLoad;

            // Same idea as StylesheetLoad above: samples reference icons by logical key (e.g.
            // src="HtmlIcon") that DemoUtils.GetImageStream resolves to an embedded resource - without
            // this, every such <img> falls through to the built-in "failed to load" icon instead.
            RendererPanel.ImageLoad += OnImageLoad;

            LoadSamples();
        }

        private void LoadSamples()
        {
            var showcaseRoot = new TreeViewNode { Content = "HTML Renderer" };
            SamplesTreeView.RootNodes.Add(showcaseRoot);
            foreach (var sample in SamplesLoader.ShowcaseSamples)
                AddSampleNode(showcaseRoot, sample);

            var testSamplesRoot = new TreeViewNode { Content = "Test Samples" };
            SamplesTreeView.RootNodes.Add(testSamplesRoot);
            foreach (var sample in SamplesLoader.TestSamples)
                AddSampleNode(testSamplesRoot, sample);

            if (SamplesLoader.PerformanceSamples.Count > 0)
            {
                var perfSamplesRoot = new TreeViewNode { Content = "Performance Samples" };
                SamplesTreeView.RootNodes.Add(perfSamplesRoot);
                foreach (var sample in SamplesLoader.PerformanceSamples)
                    AddSampleNode(perfSamplesRoot, sample);
            }

            showcaseRoot.IsExpanded = true;
            if (showcaseRoot.Children.Count > 0)
            {
                var firstNode = showcaseRoot.Children[0];
                SamplesTreeView.SelectedNode = firstNode;
                ShowSample(_nodeToSample[firstNode]);
            }
        }

        private void AddSampleNode(TreeViewNode root, HtmlSample sample)
        {
            var node = new TreeViewNode { Content = sample.Name };
            _nodeToSample[node] = sample;
            root.Children.Add(node);
        }

        private void OnSamplesTreeViewSelectionChanged(TreeView sender, TreeViewSelectionChangedEventArgs args)
        {
            foreach (var node in sender.SelectedNodes)
            {
                if (_nodeToSample.TryGetValue(node, out var sample))
                {
                    ShowSample(sample);
                    break;
                }
            }
        }

        private void ShowSample(HtmlSample sample)
        {
            try
            {
                RendererPanel.AvoidImagesLateLoading = !sample.FullName.Contains("Many images");
                RendererPanel.Text = sample.Html;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        /// <summary>
        /// Resolves the demo's logical icon keys (e.g. <c>src="HtmlIcon"</c>) via
        /// <see cref="DemoUtils.GetImageStream"/> - real URLs/paths DemoUtils doesn't recognize fall
        /// through untouched (<see cref="HtmlImageLoadEventArgs.Handled"/> left false) so the default
        /// loader still handles them.
        /// </summary>
        private void OnImageLoad(object sender, HtmlImageLoadEventArgs e)
        {
            var stream = DemoUtils.GetImageStream(e.Src);
            if (stream == null)
                return;

            e.Handled = true;
            _ = LoadIconAsync(e, stream);
        }

        private static async Task LoadIconAsync(HtmlImageLoadEventArgs e, Stream stream)
        {
            try
            {
                byte[] bytes;
                using (stream)
                using (var memory = new MemoryStream())
                {
                    await stream.CopyToAsync(memory).ConfigureAwait(false);
                    bytes = memory.ToArray();
                }

                using var randomAccessStream = new InMemoryRandomAccessStream();
                using (var writer = new DataWriter(randomAccessStream.GetOutputStreamAt(0)))
                {
                    writer.WriteBytes(bytes);
                    await writer.StoreAsync();
                    await writer.FlushAsync();
                    writer.DetachStream();
                }
                randomAccessStream.Seek(0);

                var bitmap = await CanvasBitmap.LoadAsync(CanvasDevice.GetSharedDevice(), randomAccessStream);
                e.Callback(bitmap);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                e.Callback();
            }
        }
    }
}
