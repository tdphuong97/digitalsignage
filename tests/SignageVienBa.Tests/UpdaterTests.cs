using System.Net;
using System.Security.Cryptography;
using System.Text;
using SignageVienBa.Core;

namespace SignageVienBa.Tests;

public class UpdaterTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("signage-upd-").FullName;
    private static readonly byte[] Installer = Enumerable.Range(0, 200_000).Select(i => (byte)(i * 7)).ToArray();
    private static readonly string InstallerSha = Convert.ToHexString(SHA256.HashData(Installer));

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private sealed class Fake(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<string> Urls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Urls.Add(request.RequestUri!.AbsoluteUri);
            return Task.FromResult(respond(request));
        }
    }

    private static string Feed(string version, string? sha = null, long? size = null) => $$"""
        {
          "version": "{{version}}",
          "date": "2026-11-06",
          "notes": "Sửa lỗi A\nThêm B",
          "page": "https://github.com/tdphuong97/digitalsignage/releases/tag/v{{version}}",
          "windows": { "url": "https://example.test/setup.exe", "size": {{size ?? Installer.Length}}, "sha256": "{{sha ?? InstallerSha}}" }
        }
        """;

    private static Updater Make(string feedJson, byte[]? installer = null, HttpStatusCode code = HttpStatusCode.OK)
    {
        var http = new HttpClient(new Fake(req => req.RequestUri!.AbsolutePath.EndsWith("latest.json")
            ? new HttpResponseMessage(code) { Content = new StringContent(feedJson, Encoding.UTF8) }
            : new HttpResponseMessage(code) { Content = new ByteArrayContent(installer ?? Installer) }));
        return new Updater(http, "https://example.test/latest.json");
    }

    [Theory]
    [InlineData("1.0.1", "1.0.0", true)]
    [InlineData("1.10.0", "1.9.3", true)]
    [InlineData("1.0", "1.0.0", false)]
    [InlineData("1.0.0", "1.0.0", false)]
    [InlineData("0.9.9", "1.0.0", false)]
    [InlineData("abc", "1.0.0", false)]
    public void Versions_compare_as_numbers(string candidate, string current, bool newer) =>
        Assert.Equal(newer, Updater.IsNewer(candidate, current));

    [Fact]
    public async Task Check_returns_the_newer_version_with_its_installer()
    {
        var info = await Make(Feed("1.2.0")).CheckAsync("1.0.0", CancellationToken.None);
        Assert.NotNull(info);
        Assert.Equal("1.2.0", info.Version);
        Assert.Contains("Thêm B", info.Notes);
        Assert.Equal(InstallerSha, info.Package!.Sha256);
        Assert.Equal(Installer.Length, info.Package.Size);
    }

    [Fact]
    public async Task Check_returns_null_when_already_newest() =>
        Assert.Null(await Make(Feed("1.0.0")).CheckAsync("1.0.0", CancellationToken.None));

    [Fact]
    public async Task Check_failures_become_a_plain_message()
    {
        var e = await Assert.ThrowsAsync<UpdateException>(() => Make("", code: HttpStatusCode.NotFound).CheckAsync("1.0.0", CancellationToken.None));
        Assert.Contains("404", e.Message);
        await Assert.ThrowsAsync<UpdateException>(() => Make("not json").CheckAsync("1.0.0", CancellationToken.None));
    }

    [Fact]
    public async Task Download_keeps_a_matching_installer()
    {
        var updater = Make(Feed("1.2.0"));
        var info = (await updater.CheckAsync("1.0.0", CancellationToken.None))!;
        var reports = new List<double>();
        var path = await updater.DownloadAsync(info, _dir, new SyncProgress(reports), CancellationToken.None);
        Assert.Equal("SignageVienBa-Setup-1.2.0.exe", Path.GetFileName(path));
        Assert.Equal(Installer, await File.ReadAllBytesAsync(path));
        Assert.Equal(1.0, reports[^1]);
    }

    [Fact]
    public async Task Download_throws_away_a_wrong_or_short_installer()
    {
        var badSha = Make(Feed("1.2.0", sha: new string('0', 64)));
        var info = (await badSha.CheckAsync("1.0.0", CancellationToken.None))!;
        var e = await Assert.ThrowsAsync<UpdateException>(() => badSha.DownloadAsync(info, _dir, null, CancellationToken.None));
        Assert.Contains("bị hỏng", e.Message);
        Assert.Empty(Directory.GetFiles(_dir));

        var shortFile = Make(Feed("1.2.0"), installer: Installer[..1000]);
        info = (await shortFile.CheckAsync("1.0.0", CancellationToken.None))!;
        e = await Assert.ThrowsAsync<UpdateException>(() => shortFile.DownloadAsync(info, _dir, null, CancellationToken.None));
        Assert.Contains("không đủ", e.Message);
        Assert.Empty(Directory.GetFiles(_dir));
    }

    [Fact]
    public void Default_feed_is_the_public_repos_latest_release() =>
        Assert.Equal("https://github.com/tdphuong97/digitalsignage/releases/latest/download/latest.json", Updater.DefaultFeed);
}
