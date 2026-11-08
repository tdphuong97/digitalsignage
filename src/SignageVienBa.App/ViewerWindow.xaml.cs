using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace SignageVienBa.App;

/// <summary>
/// One window for looking at what is on the TVs: a single photo or video (Xem on a row), or the slideshow page a
/// TV opens (Xem next to its link). The link is shown on top so it can be copied. Clicking another Xem reuses the
/// window. Without the WebView2 runtime the link opens in the browser, as before.
/// </summary>
public partial class ViewerWindow : Window
{
    private static ViewerWindow? _open;
    private Uri _url;
    private bool _ready;

    private ViewerWindow(string heading, Uri url)
    {
        InitializeComponent();
        _url = url;
        SetContent(heading, url);
        Loaded += Window_Loaded;
    }

    /// <summary>Shows <paramref name="url"/> in the viewer, opening the window or reusing the one already open.</summary>
    public static void Open(Window owner, string heading, Uri url)
    {
        if (_open is { } open)
        {
            open.SetContent(heading, url);
            if (open._ready) open.Web.CoreWebView2.Navigate(url.AbsoluteUri);
            if (open.WindowState == WindowState.Minimized) open.WindowState = WindowState.Normal;
            open.Activate();
            return;
        }
        if (!WebViews.RuntimeInstalled())
        {
            WebViews.OpenInBrowser(owner, url.AbsoluteUri);
            return;
        }
        // Portrait, like the TVs, as tall as the screen allows.
        var height = Math.Min(940, SystemParameters.WorkArea.Height - 40);
        _open = new ViewerWindow(heading, url) { Owner = owner, Height = height };
        _open.Closed += (_, _) => _open = null;
        _open.Show();
    }

    private void SetContent(string heading, Uri url)
    {
        _url = url;
        Heading.Text = heading;
        Heading.ToolTip = heading;
        Title = $"Xem · {heading}";
        LinkBox.Text = url.ToString();   // readable (Vietnamese letters, not %C3%A0); Chép link copies the encoded form
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await Web.EnsureCoreWebView2Async(await WebViews.Environment());
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("Viewer window: WebView2 did not start: " + ex);
            var owner = Owner;
            Close();
            WebViews.OpenInBrowser(owner, _url.AbsoluteUri);
            return;
        }

        var core = Web.CoreWebView2;
        WebViews.Configure(core);
        core.NewWindowRequested += (_, a) =>
        {
            a.Handled = true;
            WebViews.OpenLinkInBrowser(a.Uri);
        };
        core.NavigationStarting += (_, a) =>
        {
            // Files and the slideshow live on the signage server; any other address goes to the browser.
            if (Uri.TryCreate(a.Uri, UriKind.Absolute, out var target) &&
                Uri.Compare(target, _url, UriComponents.SchemeAndServer, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) == 0)
                return;
            a.Cancel = true;
            WebViews.OpenLinkInBrowser(a.Uri);
        };
        core.NavigationCompleted += (_, _) =>
        {
            Loading.Visibility = Visibility.Collapsed;
            Web.Visibility = Visibility.Visible;
        };
        _ready = true;
        core.Navigate(_url.AbsoluteUri);
    }

    private void Copy_Click(object sender, RoutedEventArgs e) => LinkCopy.Copy((Button)sender, _url.AbsoluteUri);
}
