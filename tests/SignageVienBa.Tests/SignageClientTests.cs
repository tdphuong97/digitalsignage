using System.Net;
using System.Text;
using SignageVienBa.Core;

namespace SignageVienBa.Tests;

public class SignageClientTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"upload-{Guid.NewGuid():N}.bin");

    public SignageClientTests() => File.WriteAllBytes(_file, Enumerable.Range(0, 300_000).Select(i => (byte)i).ToArray());

    public void Dispose() => File.Delete(_file);

    private sealed class FakeHandler(Func<HttpRequestMessage, byte[]?, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, byte[]? Body)> Seen { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.Content is null ? null : await request.Content.ReadAsByteArrayAsync(ct);
            Seen.Add((request, body));
            return respond(request, body);
        }
    }

    private static (SignageClient, FakeHandler) Make(Func<HttpRequestMessage, byte[]?, HttpResponseMessage> respond)
    {
        var handler = new FakeHandler(respond);
        var http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        return (new SignageClient(http, "https://signage.example.dev/", "secret"), handler);
    }

    private static HttpResponseMessage Ok(string body = "OK") =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8) };

    [Fact]
    public async Task List_reads_the_screens_manifest_without_the_key()
    {
        var (client, handler) = Make((_, _) => Ok("""["screen2/a.mp4","screen2/b.jpg"]"""));
        var files = await client.ListAsync(new TvScreen(2), CancellationToken.None);
        Assert.Equal(["screen2/a.mp4", "screen2/b.jpg"], files);
        var req = handler.Seen.Single().Request;
        Assert.Equal("https://signage.example.dev/manifest.json?screen=2", req.RequestUri!.AbsoluteUri);
        Assert.False(req.Headers.Contains("X-Api-Key"));
    }

    [Fact]
    public async Task Upload_posts_the_bytes_with_key_length_and_encoded_name()
    {
        var (client, handler) = Make((_, _) => Ok());
        var reports = new List<double>();
        await client.UploadAsync("screen2/Khai trương.mp4", _file, new SyncProgress(reports), CancellationToken.None);

        var (req, body) = handler.Seen.Single();
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.Equal("https://signage.example.dev/upload?name=screen2/Khai%20tr%C6%B0%C6%A1ng.mp4", req.RequestUri!.AbsoluteUri);
        Assert.Equal("secret", req.Headers.GetValues("X-Api-Key").Single());
        Assert.Equal(300_000, req.Content!.Headers.ContentLength);
        Assert.Equal(File.ReadAllBytes(_file), body);
        Assert.Equal(1.0, reports[^1]);
        Assert.True(reports.SequenceEqual(reports.Order()));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "Mã API không đúng")]
    [InlineData(HttpStatusCode.RequestEntityTooLarge, "quá lớn")]
    [InlineData(HttpStatusCode.BadGateway, "Máy chủ đang lỗi (502)")]
    public async Task Server_errors_become_plain_messages(HttpStatusCode code, string expected)
    {
        var (client, _) = Make((_, _) => new HttpResponseMessage(code));
        var e = await Assert.ThrowsAsync<SignageException>(() =>
            client.UploadAsync("a.jpg", _file, null, CancellationToken.None));
        Assert.Contains(expected, e.Message);
    }

    [Fact]
    public async Task Network_failure_becomes_a_plain_message()
    {
        var (client, _) = Make((_, _) => throw new HttpRequestException("boom"));
        var e = await Assert.ThrowsAsync<SignageException>(() => client.ListAsync(new TvScreen(1), CancellationToken.None));
        Assert.Contains("Không kết nối được", e.Message);
    }

    [Fact]
    public async Task Files_over_95_mb_are_refused_before_sending()
    {
        var big = Path.Combine(Path.GetTempPath(), $"big-{Guid.NewGuid():N}.bin");
        using (var fs = File.Create(big)) fs.SetLength(SignageClient.MaxUploadBytes + 1);
        try
        {
            var (client, handler) = Make((_, _) => Ok());
            await Assert.ThrowsAsync<SignageException>(() => client.UploadAsync("a.jpg", big, null, CancellationToken.None));
            Assert.Empty(handler.Seen);
        }
        finally { File.Delete(big); }
    }

    [Fact]
    public async Task Delete_sends_the_key_and_check_uses_a_harmless_name()
    {
        var (client, handler) = Make((_, _) => Ok("[]"));
        await client.DeleteAsync("screen2/a b.mp4", CancellationToken.None);
        await client.CheckAsync(CancellationToken.None);
        Assert.Equal(HttpMethod.Delete, handler.Seen[0].Request.Method);
        Assert.Equal("https://signage.example.dev/upload?name=screen2/a%20b.mp4", handler.Seen[0].Request.RequestUri!.AbsoluteUri);
        Assert.EndsWith("name=__kiem-tra-ket-noi__", handler.Seen[2].Request.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public void Bad_addresses_are_refused() =>
        Assert.Throws<SignageException>(() => new SignageClient(new HttpClient(), "signage.vienba", "k"));

    [Fact]
    public void File_uri_is_encoded_for_the_browser()
    {
        var client = new SignageClient(new HttpClient(), "https://signage.example.dev", "k");
        Assert.Equal("https://signage.example.dev/screen2/a%20%23b.mp4", client.FileUri("screen2/a #b.mp4").AbsoluteUri);
    }

    [Fact]
    public async Task Page_check_accepts_the_slideshow_and_refuses_errors_or_other_pages()
    {
        var (good, handler) = Make((_, _) => Ok("<!doctype html><video id=\"vid\" playsinline></video>"));
        await good.CheckPageAsync(new TvScreen(2), CancellationToken.None);
        Assert.Equal("https://signage.example.dev/?screen=2", handler.Seen.Single().Request.RequestUri!.AbsoluteUri);
        Assert.False(handler.Seen.Single().Request.Headers.Contains("X-Api-Key"));

        var (missing, _) = Make((_, _) => new HttpResponseMessage(HttpStatusCode.NotFound));
        var e = await Assert.ThrowsAsync<SignageException>(() => missing.CheckPageAsync(new TvScreen(1), CancellationToken.None));
        Assert.Contains("404", e.Message);

        var (other, _) = Make((_, _) => Ok("<html>Cloudflare error</html>"));
        await Assert.ThrowsAsync<SignageException>(() => other.CheckPageAsync(new TvScreen(1), CancellationToken.None));
    }

    [Theory]
    [InlineData("https://signage.example.dev")]
    [InlineData(" https://signage.example.dev/ ")]
    public void Page_uri_is_the_link_each_tv_opens(string baseUrl)
    {
        var client = new SignageClient(new HttpClient(), baseUrl, "");
        Assert.Equal("https://signage.example.dev/", client.PageUri(new TvScreen(1)).AbsoluteUri);
        Assert.Equal("https://signage.example.dev/?screen=2", client.PageUri(new TvScreen(2)).AbsoluteUri);
    }
}

/// <summary>IProgress that records synchronously (Progress&lt;T&gt; posts to the thread pool).</summary>
internal sealed class SyncProgress(List<double> sink) : IProgress<double>
{
    public void Report(double value)
    {
        lock (sink) sink.Add(value);
    }
}
