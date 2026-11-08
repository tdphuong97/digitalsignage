using SignageVienBa.Core;

namespace SignageVienBa.Tests;

public class GuideLinksTests
{
    [Fact]
    public void Page_url_is_the_guide_file_on_the_private_host()
    {
        var url = GuideLinks.PageUrl(@"C:\Users\a\AppData\Local\Programs\SignageVienBa\HuongDan.html");
        Assert.Equal("https://huongdan.example/HuongDan.html", url);
        Assert.True(GuideLinks.IsGuide(url));
        Assert.True(GuideLinks.IsGuide(url + "#/su-dung/dua-anh-video-len-tv"));
        Assert.False(GuideLinks.OpensInBrowser(url));
    }

    [Theory]
    [InlineData("https://github.com/tdphuong97/digitalsignage/releases")]
    [InlineData("http://example.com/")]
    public void Web_links_open_in_the_browser(string url)
    {
        Assert.False(GuideLinks.IsGuide(url));
        Assert.True(GuideLinks.OpensInBrowser(url));
    }

    [Theory]
    [InlineData("http://huongdan.example/HuongDan.html")]
    [InlineData("file:///C:/Windows/System32/cmd.exe")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,hi")]
    [InlineData("about:blank")]
    [InlineData("not a url")]
    public void Other_addresses_are_neither_shown_nor_opened(string url)
    {
        Assert.False(GuideLinks.IsGuide(url));
        Assert.False(GuideLinks.OpensInBrowser(url));
    }
}
