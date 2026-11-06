using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace SignageVienBa.Core;

/// <summary>An error with a message meant for the person using the app (Vietnamese).</summary>
public sealed class SignageException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Talks to the signage Worker: list, upload and delete a TV's files.</summary>
public sealed class SignageClient
{
    /// <summary>Cloudflare rejects request bodies over 100 MB; keep a margin.</summary>
    public const long MaxUploadBytes = 95L * 1024 * 1024;

    private static readonly TimeSpan ShortTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ServerSaveTimeout = TimeSpan.FromMinutes(2);

    private readonly HttpClient _http;
    private readonly Uri _base;
    private readonly string _apiKey;

    /// <param name="http">A client with <see cref="HttpClient.Timeout"/> = infinite; timeouts are handled here.</param>
    public SignageClient(HttpClient http, string baseUrl, string apiKey)
    {
        if (!Uri.TryCreate(baseUrl.Trim().TrimEnd('/') + "/", UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            throw new SignageException("Địa chỉ máy chủ không hợp lệ. Vào Cài đặt để sửa.");
        _http = http;
        _base = uri;
        _apiKey = apiKey.Trim();
    }

    public Uri FileUri(string key) => new(_base, NameRules.EncodeKey(key));

    /// <summary>The TV's playlist, in play order.</summary>
    public async Task<IReadOnlyList<string>> ListAsync(TvScreen tv, CancellationToken ct)
    {
        using var cts = WithTimeout(ct, ShortTimeout);
        var body = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Get, new Uri(_base, $"manifest.json?screen={tv.Number}")),
            authed: false, cts.Token, ct);
        try
        {
            return JsonSerializer.Deserialize<List<string>>(body) ?? [];
        }
        catch (JsonException e)
        {
            throw new SignageException("Máy chủ trả về danh sách không đọc được. Kiểm tra địa chỉ trong Cài đặt.", e);
        }
    }

    /// <summary>Uploads one file under <paramref name="key"/>; replaces a file with the same key.</summary>
    public async Task UploadAsync(string key, string filePath, IProgress<double>? progress, CancellationToken ct)
    {
        var length = new FileInfo(filePath).Length;
        if (length > MaxUploadBytes)
            throw new SignageException($"File quá lớn ({length / 1024.0 / 1024:0} MB). Giới hạn tải lên là 95 MB.");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var content = new ProgressFileContent(filePath, length, progress);
        using var watchdog = StartStallWatchdog(content, cts);

        await SendAsync(() =>
        {
            var req = new HttpRequestMessage(HttpMethod.Post, UploadUri(key)) { Content = content };
            return req;
        }, authed: true, cts.Token, ct, isStall: () => watchdog.Fired);
    }

    public async Task DeleteAsync(string key, CancellationToken ct)
    {
        using var cts = WithTimeout(ct, ShortTimeout);
        await SendAsync(() => new HttpRequestMessage(HttpMethod.Delete, UploadUri(key)), authed: true, cts.Token, ct);
    }

    /// <summary>
    /// Checks the address and the API key. The Worker has no read-only authenticated call, so this
    /// deletes a key that never exists: harmless, and it answers 401 when the key is wrong.
    /// </summary>
    public async Task CheckAsync(CancellationToken ct)
    {
        await ListAsync(new TvScreen(1), ct);
        await DeleteAsync("__kiem-tra-ket-noi__", ct);
    }

    private Uri UploadUri(string key) => new(_base, "upload?name=" + NameRules.EncodeKey(key));

    private async Task<string> SendAsync(Func<HttpRequestMessage> make, bool authed, CancellationToken token,
        CancellationToken userToken, Func<bool>? isStall = null)
    {
        using var req = make();
        if (authed) req.Headers.Add("X-Api-Key", _apiKey);
        HttpResponseMessage resp;
        try
        {
            resp = await _http.SendAsync(req, HttpCompletionOption.ResponseContentRead, token);
        }
        catch (OperationCanceledException) when (userToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException e)
        {
            throw new SignageException(isStall?.Invoke() == true
                ? "Mạng bị ngắt giữa chừng (không gửi được dữ liệu trong 60 giây). Thử lại."
                : "Máy chủ không phản hồi. Kiểm tra mạng rồi thử lại.", e);
        }
        catch (HttpRequestException e)
        {
            throw new SignageException("Không kết nối được tới máy chủ. Kiểm tra mạng Internet.", e);
        }

        using (resp)
        {
            if (resp.IsSuccessStatusCode) return await resp.Content.ReadAsStringAsync(CancellationToken.None);
            throw new SignageException(resp.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "Mã API không đúng. Vào Cài đặt để nhập lại.",
                HttpStatusCode.RequestEntityTooLarge => "File quá lớn để tải lên (giới hạn 100 MB).",
                HttpStatusCode.NotFound => "Không tìm thấy máy chủ. Kiểm tra địa chỉ trong Cài đặt.",
                >= HttpStatusCode.InternalServerError => $"Máy chủ đang lỗi ({(int)resp.StatusCode}). Thử lại sau ít phút.",
                _ => $"Máy chủ báo lỗi {(int)resp.StatusCode}.",
            });
        }
    }

    private static CancellationTokenSource WithTimeout(CancellationToken ct, TimeSpan timeout)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);
        return cts;
    }

    /// <summary>Cancels the upload when no bytes go out for a minute, or the server never answers.</summary>
    private static Watchdog StartStallWatchdog(ProgressFileContent content, CancellationTokenSource cts)
    {
        var dog = new Watchdog();
        _ = Task.Run(async () =>
        {
            long lastSent = -1;
            var lastChange = DateTime.UtcNow;
            DateTime? sentAll = null;
            while (!dog.Stopped && !cts.IsCancellationRequested)
            {
                await Task.Delay(1000).ConfigureAwait(false);
                var now = DateTime.UtcNow;
                var sent = content.BytesSent;
                if (sent != lastSent) { lastSent = sent; lastChange = now; }
                if (content.Finished) sentAll ??= now;

                var stalled = sentAll is { } done
                    ? now - done > ServerSaveTimeout
                    : now - lastChange > StallTimeout;
                if (stalled && !dog.Stopped)
                {
                    dog.Fired = true;
                    try { cts.Cancel(); } catch (ObjectDisposedException) { }
                    return;
                }
            }
        });
        return dog;
    }

    private sealed class Watchdog : IDisposable
    {
        public volatile bool Stopped;
        public volatile bool Fired;
        public void Dispose() => Stopped = true;
    }

    /// <summary>Streams a file with a known length (R2 needs it) and reports progress 0..1.</summary>
    private sealed class ProgressFileContent : HttpContent
    {
        private readonly string _path;
        private readonly long _length;
        private readonly IProgress<double>? _progress;
        private long _sent;

        public ProgressFileContent(string path, long length, IProgress<double>? progress)
        {
            _path = path;
            _length = length;
            _progress = progress;
            Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        }

        public long BytesSent => Interlocked.Read(ref _sent);
        public bool Finished { get; private set; }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            SerializeToStreamAsync(stream, context, CancellationToken.None);

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken ct)
        {
            Interlocked.Exchange(ref _sent, 0);
            Finished = false;
            await using var file = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16, useAsync: true);
            var buffer = new byte[1 << 16];
            int read;
            var lastReport = 0.0;
            while ((read = await file.ReadAsync(buffer, ct)) > 0)
            {
                await stream.WriteAsync(buffer.AsMemory(0, read), ct);
                var sent = Interlocked.Add(ref _sent, read);
                var fraction = _length == 0 ? 1 : (double)sent / _length;
                if (fraction - lastReport >= 0.005 || sent == _length)
                {
                    lastReport = fraction;
                    _progress?.Report(fraction);
                }
            }
            Finished = true;
        }

        protected override bool TryComputeLength(out long length)
        {
            length = _length;
            return true;
        }
    }
}
