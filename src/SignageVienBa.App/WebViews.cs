using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using SignageVienBa.Core;

namespace SignageVienBa.App;

/// <summary>
/// What the guide window and the viewer window share: one WebView2 profile, the same locked-down settings,
/// and the browser as a fallback on the rare PC without the WebView2 runtime.
/// </summary>
internal static class WebViews
{
    private static Task<CoreWebView2Environment>? _environment;

    public static bool RuntimeInstalled() => RuntimeVersion() is not null;

    /// <summary>The installed WebView2 runtime's version, or null when there is none.</summary>
    public static string? RuntimeVersion()
    {
        try { return CoreWebView2Environment.GetAvailableBrowserVersionString() is { Length: > 0 } v ? v : null; }
        catch (Exception e) when (e is WebView2RuntimeNotFoundException or DllNotFoundException) { return null; }
    }

    // One browser profile for the app, kept out of the roaming profile (the installer removes it on uninstall).
    public static Task<CoreWebView2Environment> Environment() =>
        _environment ??= CoreWebView2Environment.CreateAsync(null, Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "SignageVienBa", "WebView2"));

    public static void Configure(CoreWebView2 core)
    {
        var s = core.Settings;
        s.AreDefaultContextMenusEnabled = false;
        s.AreBrowserAcceleratorKeysEnabled = false;
        s.IsStatusBarEnabled = false;
        s.IsPasswordAutosaveEnabled = false;
        s.IsGeneralAutofillEnabled = false;
#if !DEBUG
        s.AreDevToolsEnabled = false;
#endif
    }

    /// <summary>A web link clicked inside one of the app's windows: http(s) goes to the user's browser, anything else nowhere.</summary>
    public static void OpenLinkInBrowser(string url)
    {
        if (!GuideLinks.OpensInBrowser(url)) return;
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })?.Dispose(); }
        catch (Win32Exception) { }
    }

    /// <summary>Opens a file or page in the default browser, as the app did before it had its own windows.</summary>
    public static void OpenInBrowser(Window? owner, string target)
    {
        try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true })?.Dispose(); }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            const string text = "Không mở được: ";
            if (owner is { IsLoaded: true })
                MessageBox.Show(owner, text + ex.Message, "Signage Viên Ba", MessageBoxButton.OK, MessageBoxImage.Warning);
            else
                MessageBox.Show(text + ex.Message, "Signage Viên Ba", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
