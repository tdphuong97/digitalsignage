using System.Globalization;
using System.Text.Json;

namespace SignageVienBa.Core;

/// <summary>Something the slideshow page on a TV reported: "load", "offline", "skip", "reload", ...</summary>
public sealed record TvEvent(DateTimeOffset At, string Type, string Detail);

/// <summary>The last heartbeat of one TV's slideshow page (Worker v2, POST /heartbeat every minute).</summary>
public sealed record TvStatus(
    DateTimeOffset Seen,
    string PageVersion,
    string Playing,
    DateTimeOffset? LoadedAt,
    bool Online,
    bool OfflineReady,
    int Files,
    string Browser,
    IReadOnlyList<TvEvent> Events);

/// <summary>GET /status: every TV's last heartbeat, and the server's clock to measure their age against.</summary>
public sealed record LiveStatus(DateTimeOffset ServerNow, IReadOnlyDictionary<int, TvStatus> Screens)
{
    public TvStatus? For(TvScreen tv) => Screens.GetValueOrDefault(tv.Number);

    /// <summary>Reads the Worker's /status JSON. Throws JsonException when it is not that.</summary>
    public static LiveStatus Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var now = FromMs(root.GetProperty("now"));
        var screens = new Dictionary<int, TvStatus>();
        if (root.TryGetProperty("screens", out var all) && all.ValueKind == JsonValueKind.Object)
        {
            foreach (var s in all.EnumerateObject())
            {
                if (!int.TryParse(s.Name, NumberStyles.None, CultureInfo.InvariantCulture, out var number)) continue;
                var v = s.Value;
                if (v.ValueKind != JsonValueKind.Object || !v.TryGetProperty("seen", out var seen)) continue;
                var events = new List<TvEvent>();
                if (v.TryGetProperty("events", out var list) && list.ValueKind == JsonValueKind.Array)
                    foreach (var e in list.EnumerateArray())
                        if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty("at", out var at) && at.ValueKind == JsonValueKind.Number)
                            events.Add(new TvEvent(FromMs(at), Str(e, "type"), Str(e, "detail")));
                screens[number] = new TvStatus(
                    FromMs(seen),
                    Str(v, "v"),
                    Str(v, "playing"),
                    v.TryGetProperty("loadedAt", out var loaded) && loaded.ValueKind == JsonValueKind.Number ? FromMs(loaded) : null,
                    !v.TryGetProperty("online", out var online) || online.ValueKind != JsonValueKind.False,
                    v.TryGetProperty("sw", out var sw) && sw.ValueKind == JsonValueKind.True,
                    v.TryGetProperty("files", out var files) && files.TryGetInt32(out var n) ? n : 0,
                    Str(v, "ua"),
                    events);
            }
        }
        return new LiveStatus(now, screens);
    }

    private static DateTimeOffset FromMs(JsonElement e) => DateTimeOffset.FromUnixTimeMilliseconds(e.GetInt64());

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
}

/// <summary>How the app shows one TV's state: a dot colour and a short line, plus the long form for Chẩn đoán.</summary>
public enum LiveState { Unknown, Playing, Silent, Off }

public static class TvSchedule
{
    // The TVs switch themselves on at 8:00 and off at 18:00, Monday to Friday (their own timers, building's choice).
    public static readonly TimeSpan On = new(8, 0, 0);
    public static readonly TimeSpan Off = new(18, 0, 0);
    public const string Text = "8:00–18:00, thứ 2–thứ 6";

    /// <summary>Whether the TVs should be on at this local time, with a few minutes' grace after switching on.</summary>
    public static bool ShouldBeOn(DateTime local) =>
        local.DayOfWeek is >= DayOfWeek.Monday and <= DayOfWeek.Friday
        && local.TimeOfDay >= On + TimeSpan.FromMinutes(5)
        && local.TimeOfDay < Off;
}

public static class Live
{
    /// <summary>A heartbeat older than this means the page is not running (it sends one every minute).</summary>
    public static readonly TimeSpan Silence = TimeSpan.FromMinutes(3);

    public static LiveState StateOf(TvStatus? s, LiveStatus live, DateTime localNow)
    {
        if (s is null) return LiveState.Unknown;
        if (live.ServerNow - s.Seen <= Silence) return LiveState.Playing;
        return TvSchedule.ShouldBeOn(localNow) ? LiveState.Silent : LiveState.Off;
    }

    /// <summary>The short line under the TV list: "Đang chiếu · báo lúc 09:41", "Mất tín hiệu từ 08:02", ...</summary>
    public static string ShortText(TvStatus? s, LiveStatus live, DateTime localNow) => StateOf(s, live, localNow) switch
    {
        LiveState.Playing => $"TV đang chiếu · báo lúc {Local(s!.Seen):HH:mm}",
        LiveState.Silent => $"TV mất tín hiệu từ {When(s!.Seen, localNow)}",
        LiveState.Off => $"TV đang tắt (ngoài giờ) · báo lần cuối {When(s!.Seen, localNow)}",
        _ => "Chưa nhận tín hiệu từ TV",
    };

    /// <summary>The "Tín hiệu từ TV" row of Chẩn đoán, both TVs in one line.</summary>
    public static CheckOutcome Diagnose(LiveStatus? live, DateTime localNow)
    {
        if (live is null)
            return CheckOutcome.Skip("Máy chủ chưa có tính năng theo dõi TV (cần IT cập nhật máy chủ).");
        var parts = new List<string>();
        var worst = CheckStatus.Ok;
        foreach (var tv in TvScreen.All)
        {
            var s = live.For(tv);
            switch (StateOf(s, live, localNow))
            {
                case LiveState.Playing:
                    parts.Add($"{tv.Label} đang chiếu{(s!.Playing.Length > 0 ? " " + NameRules.DisplayName(tv, s.Playing) : "")} (báo lúc {Local(s.Seen):HH:mm})"
                              + (s.OfflineReady ? "" : ", chưa lưu sẵn để chạy khi mất mạng"));
                    break;
                case LiveState.Off:
                    parts.Add($"{tv.Label} đang tắt theo lịch, báo lần cuối {When(s!.Seen, localNow)}");
                    break;
                case LiveState.Silent:
                    parts.Add($"{tv.Label} mất tín hiệu từ {When(s!.Seen, localNow)}");
                    worst = CheckStatus.Warning;
                    break;
                default:
                    parts.Add($"{tv.Label} chưa gửi tín hiệu lần nào");
                    worst = CheckStatus.Warning;
                    break;
            }
        }
        var detail = string.Join(". ", parts) + ".";
        return worst == CheckStatus.Ok
            ? CheckOutcome.Ok(detail)
            : CheckOutcome.Warn(detail,
                $"TV bật theo lịch {TvSchedule.Text}. Trong giờ đó mà mất tín hiệu: ra xem TV, nếu thấy trang trình duyệt trống thì tắt rồi bật lại TV bằng remote. TV chưa gửi tín hiệu lần nào: máy chủ hoặc trang chiếu chưa được cập nhật, hoặc TV chưa mở lại trang chiếu từ đó.");
    }

    /// <summary>Recent events of one TV, newest last, one per line, for the report sent to IT.</summary>
    public static IEnumerable<string> EventLines(TvStatus s) =>
        s.Events.Select(e => $"{Local(e.At):dd/MM HH:mm:ss} {e.Type}{(e.Detail.Length > 0 ? " " + e.Detail : "")}");

    // Server times shown in the PC's time zone; the PC's own clock is not used to judge their age.
    private static DateTime Local(DateTimeOffset at) => at.ToLocalTime().DateTime;

    private static string When(DateTimeOffset at, DateTime localNow)
    {
        var local = Local(at);
        return local.Date == localNow.Date ? local.ToString("HH:mm") : local.ToString("HH:mm dd/MM");
    }
}
