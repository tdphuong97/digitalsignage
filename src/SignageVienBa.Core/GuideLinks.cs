namespace SignageVienBa.Core;

/// <summary>
/// Addresses for the user guide window. The guide (HuongDan.html) is served from its folder under a private
/// host name; every other address it tries to open goes to the user's browser, or nowhere.
/// </summary>
public static class GuideLinks
{
    /// <summary>The .example name is reserved, so it can never be a real site.</summary>
    public const string Host = "huongdan.example";

    public static string PageUrl(string guidePath) =>
        $"https://{Host}/{Uri.EscapeDataString(Path.GetFileName(guidePath))}";

    /// <summary>True for the guide itself, which the window shows.</summary>
    public static bool IsGuide(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u)
        && u.Scheme == Uri.UriSchemeHttps
        && u.Host.Equals(Host, StringComparison.OrdinalIgnoreCase);

    /// <summary>True for a web page the guide links to: it opens in the user's browser.
    /// Anything else (file:, javascript:, data:, the private host over http, ...) is not opened at all.</summary>
    public static bool OpensInBrowser(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u)
        && (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp)
        && !u.Host.Equals(Host, StringComparison.OrdinalIgnoreCase);
}
