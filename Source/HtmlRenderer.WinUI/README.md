# Welcome to the HTML Renderer WinUI 3 library!

This library provides the rich formatting power of HTML in your WinUI 3 .NET applications using
simple controls or static rendering code.
For more info see HTML Renderer on GitHub: https://github.com/ArthurHub/HTML-Renderer

## FEEDBACK / RELEASE NOTES

If you have problems, wish to report a bug, or have a suggestion, please open an issue on the
HTML Renderer issue page: https://github.com/ArthurHub/HTML-Renderer/issues

For full release notes and all versions see: https://github.com/ArthurHub/HTML-Renderer/releases

---

## Quick Start: Use HTML panel control on a WinUI 3 window

```xaml
<Window x:Class="MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:winui="clr-namespace:TheArtOfDev.HtmlRenderer.WinUI;assembly=HtmlRenderer.WinUI">
    <Grid>
        <winui:HtmlPanel Text="&lt;p&gt; &lt;h1&gt; Hello World &lt;/h1&gt; This is html rendered text&lt;/p&gt;"/>
    </Grid>
</Window>
```

## Quick Start: Create image from HTML snippet

```csharp
class Program
{
    private static async Task Main(string[] args)
    {
        var bitmap = await HtmlRender.RenderToImageAsync("<p><h1>Hello World</h1>This is html rendered text</p>");
        await bitmap.SaveAsync("image.png", CanvasBitmapFileFormat.Png);
    }
}
```
