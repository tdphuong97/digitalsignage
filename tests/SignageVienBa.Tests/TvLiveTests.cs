using System.Net;
using System.Text;
using SignageVienBa.Core;

namespace SignageVienBa.Tests;

public class TvLiveTests
{
    // 2026-11-09 is a Monday.
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 9, 30, 0, TimeSpan.Zero);
    private static long Ms(DateTimeOffset t) => t.ToUnixTimeMilliseconds();

    private static string Json(DateTimeOffset seen1, bool sw1 = true) => $$"""
        {"now":{{Ms(Now)}},"screens":{
          "1":{"seen":{{Ms(seen1)}},"v":"2","playing":"01-Chào.jpg","loadedAt":{{Ms(Now.AddHours(-1))}},"online":true,"sw":{{(sw1 ? "true" : "false")}},"files":5,
               "ua":"Mozilla/5.0 (Web0S; Linux/SmartTV)","events":[{"at":{{Ms(Now.AddMinutes(-90))}},"type":"load","detail":"offline"},{"at":{{Ms(Now.AddMinutes(-89))}},"type":"offline-copy","detail":"playlist"}]},
          "x":{"seen":1},
          "3":"broken"} }
        """;

    [Fact]
    public void Parses_the_workers_status()
    {
        var live = LiveStatus.Parse(Json(Now.AddSeconds(-40)));
        Assert.Equal(Now, live.ServerNow);
        var tv1 = Assert.IsType<TvStatus>(live.For(new TvScreen(1)));
        Assert.Equal("01-Chào.jpg", tv1.Playing);
        Assert.Equal("2", tv1.PageVersion);
        Assert.True(tv1.OfflineReady);
        Assert.Equal(5, tv1.Files);
        Assert.Equal(Now.AddHours(-1), tv1.LoadedAt);
        Assert.Equal(["load", "offline-copy"], tv1.Events.Select(e => e.Type));
        Assert.Null(live.For(new TvScreen(2)));
        Assert.Single(live.Screens);
    }

    [Fact]
    public void States_follow_the_heartbeat_age_and_the_tv_timers()
    {
        var monday0930 = new DateTime(2026, 11, 9, 9, 30, 0);
        var monday1900 = new DateTime(2026, 11, 9, 19, 0, 0);
        var saturday = new DateTime(2026, 11, 14, 10, 0, 0);
        var fresh = LiveStatus.Parse(Json(Now.AddMinutes(-1)));
        var stale = LiveStatus.Parse(Json(Now.AddMinutes(-40)));
        TvStatus? S(LiveStatus l) => l.For(new TvScreen(1));

        Assert.Equal(LiveState.Playing, Live.StateOf(S(fresh), fresh, monday0930));
        Assert.Equal(LiveState.Silent, Live.StateOf(S(stale), stale, monday0930));
        Assert.Equal(LiveState.Off, Live.StateOf(S(stale), stale, monday1900));
        Assert.Equal(LiveState.Off, Live.StateOf(S(stale), stale, saturday));
        Assert.Equal(LiveState.Unknown, Live.StateOf(null, stale, monday0930));
        // just after 8:00 the TV is still starting: not yet "silent"
        Assert.Equal(LiveState.Off, Live.StateOf(S(stale), stale, new DateTime(2026, 11, 9, 8, 2, 0)));
    }

    [Fact]
    public void Diagnosis_warns_about_a_silent_tv_and_one_that_never_reported()
    {
        var monday0930 = new DateTime(2026, 11, 9, 9, 30, 0);
        var o = Live.Diagnose(LiveStatus.Parse(Json(Now.AddMinutes(-40))), monday0930);
        Assert.Equal(CheckStatus.Warning, o.Status);
        Assert.Contains("TV 1 mất tín hiệu từ", o.Detail);
        Assert.Contains("TV 2 chưa gửi tín hiệu lần nào", o.Detail);
        Assert.Contains("remote", o.Fix);

        Assert.Equal(CheckStatus.Skipped, Live.Diagnose(null, monday0930).Status);
    }

    [Fact]
    public void Diagnosis_names_what_a_playing_tv_shows_and_whether_it_can_start_offline()
    {
        var monday0930 = new DateTime(2026, 11, 9, 9, 30, 0);
        var o = Live.Diagnose(LiveStatus.Parse(Json(Now.AddMinutes(-1), sw1: false)), monday0930);
        Assert.Contains("TV 1 đang chiếu 01-Chào.jpg", o.Detail);
        Assert.Contains("chưa lưu sẵn để chạy khi mất mạng", o.Detail);
    }

    [Fact]
    public async Task Status_is_null_on_a_server_without_it_and_parsed_otherwise()
    {
        var old = new SignageClient(new HttpClient(new Handler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)))
            { Timeout = Timeout.InfiniteTimeSpan }, "https://signage.example.dev", "");
        Assert.Null(await old.StatusAsync(CancellationToken.None));

        string? asked = null;
        var v2 = new SignageClient(new HttpClient(new Handler(r =>
        {
            asked = r.RequestUri!.AbsoluteUri;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Json(Now), Encoding.UTF8) };
        })) { Timeout = Timeout.InfiniteTimeSpan }, "https://signage.example.dev", "");
        var live = await v2.StatusAsync(CancellationToken.None);
        Assert.Equal("https://signage.example.dev/status", asked);
        Assert.NotNull(live!.For(new TvScreen(1)));

        var broken = new SignageClient(new HttpClient(new Handler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<html>", Encoding.UTF8) }))
            { Timeout = Timeout.InfiniteTimeSpan }, "https://signage.example.dev", "");
        await Assert.ThrowsAsync<SignageException>(() => broken.StatusAsync(CancellationToken.None));
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(respond(request));
    }
}
