using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using SignageVienBa.Core;

namespace SignageVienBa.App;

/// <summary>
/// The user guide (HuongDan.html, next to the exe) in its own window, shown by WebView2 without a browser or the
/// internet. Web links in the guide open in the user's browser. On the rare PC without the WebView2 runtime the
/// guide opens in the browser instead, as it did in 1.0.1.
/// </summary>
public partial class GuideWindow : Window
{
    private static GuideWindow? _open;
    private readonly string _path;

    private GuideWindow(string path)
    {
        _path = path;
        InitializeComponent();
        Loaded += Window_Loaded;
    }

    /// <summary>Shows the guide beside the app, or brings the guide window that is already open to the front.</summary>
    public static void Open(Window owner, string path)
    {
        if (_open is { } open)
        {
            if (open.WindowState == WindowState.Minimized) open.WindowState = WindowState.Normal;
            open.Activate();
            return;
        }
        if (!WebViews.RuntimeInstalled())
        {
            WebViews.OpenInBrowser(owner, path);
            return;
        }
        _open = new GuideWindow(path) { Owner = owner };
        _open.Closed += (_, _) => _open = null;
        _open.Show();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await Web.EnsureCoreWebView2Async(await WebViews.Environment());
        }
        catch (Exception ex)
        {
            // Whatever stops WebView2 from starting (runtime removed or broken, profile folder not writable),
            // the guide is still readable in the browser.
            Trace.TraceWarning("Guide window: WebView2 did not start: " + ex);
            var owner = Owner;
            Close();
            WebViews.OpenInBrowser(owner, _path);
            return;
        }

        var core = Web.CoreWebView2;
        WebViews.Configure(core);
        core.SetVirtualHostNameToFolderMapping(GuideLinks.Host, Path.GetDirectoryName(Path.GetFullPath(_path))!,
            CoreWebView2HostResourceAccessKind.Allow);
        core.NewWindowRequested += (_, a) =>
        {
            a.Handled = true;
            WebViews.OpenLinkInBrowser(a.Uri);
        };
        core.NavigationStarting += (_, a) =>
        {
            // The guide moves between its pages inside the same document; anything else leaves the window.
            if (GuideLinks.IsGuide(a.Uri)) return;
            a.Cancel = true;
            WebViews.OpenLinkInBrowser(a.Uri);
        };
        core.NavigationCompleted += (_, _) =>
        {
            Loading.Visibility = Visibility.Collapsed;
            Web.Visibility = Visibility.Visible;
        };
        core.Navigate(GuideLinks.PageUrl(_path));
    }
}
