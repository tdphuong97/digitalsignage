using System.Text;

namespace SignageVienBa.Core;

public enum CheckStatus { Running, Ok, Warning, Error, Skipped }

/// <summary>How one check of the diagnosis went, and what to do about it (Vietnamese, for the person using the app).</summary>
public sealed record CheckOutcome(CheckStatus Status, string Detail, string Fix = "")
{
    public static CheckOutcome Ok(string detail) => new(CheckStatus.Ok, detail);
    public static CheckOutcome Warn(string detail, string fix) => new(CheckStatus.Warning, detail, fix);
    public static CheckOutcome Fail(string detail, string fix) => new(CheckStatus.Error, detail, fix);
    public static CheckOutcome Skip(string detail) => new(CheckStatus.Skipped, detail);
}

/// <summary>The rules behind "Chẩn đoán" that do not need the network, Windows or the UI.</summary>
public static class Diagnosis
{
    // The slideshow plays .mp4 as video and shows everything else as an image.
    private static readonly HashSet<string> Playable = new(StringComparer.OrdinalIgnoreCase)
        { ".mp4", ".jpg", ".jpeg", ".png", ".gif", ".webp" };

    public const long LowDiskBytes = 3L * 1024 * 1024 * 1024;
    public const long CriticalDiskBytes = 1L * 1024 * 1024 * 1024;

    /// <summary>Looks at what a TV plays: nothing at all, half-compressed leftovers, files it cannot show.</summary>
    public static CheckOutcome TvContent(TvScreen tv, IReadOnlyList<string> keys)
    {
        if (keys.Count == 0)
            return CheckOutcome.Warn($"{tv.Label} chưa có nội dung nào, TV đang không chiếu gì.",
                $"Đưa ít nhất một ảnh hoặc video lên {tv.Label}.");

        var names = keys.Select(k => NameRules.DisplayName(tv, k)).ToList();
        var partial = names.Where(n => n.Contains(".tmp", StringComparison.OrdinalIgnoreCase)).ToList();
        var unplayable = names.Except(partial).Where(n => !Playable.Contains(Path.GetExtension(n))).ToList();
        if (partial.Count == 0 && unplayable.Count == 0)
            return CheckOutcome.Ok($"{keys.Count} nội dung, TV chiếu được tất cả.");

        var problems = new List<string>();
        if (partial.Count > 0) problems.Add("file nén dở " + List(partial));
        if (unplayable.Count > 0) problems.Add("file TV không chiếu được " + List(unplayable));
        return CheckOutcome.Warn($"{keys.Count} nội dung, trong đó có {string.Join("; ", problems)}.",
            $"Chọn {tv.Label}, bấm Xóa ở các dòng này. Cần video đó thì đưa lại bản gốc bằng phần mềm.");
    }

    public static CheckOutcome DiskSpace(long freeBytes)
    {
        var free = Size(freeBytes);
        if (freeBytes < CriticalDiskBytes)
            return CheckOutcome.Fail($"Chỉ còn {free} trống. Nén video có thể bị lỗi giữa chừng.",
                "Dọn bớt ổ đĩa (thùng rác, thư mục Downloads) rồi chạy lại chẩn đoán.");
        if (freeBytes < LowDiskBytes)
            return CheckOutcome.Warn($"Còn {free} trống, hơi ít khi nén video dài.",
                "Nên dọn bớt ổ đĩa (thùng rác, thư mục Downloads).");
        return CheckOutcome.Ok($"Còn {free} trống.");
    }

    /// <summary>The old PowerShell sync deletes from the TVs whatever is not in its OneDrive folder.</summary>
    public static CheckOutcome OldSync(IReadOnlyList<(string Task, bool Enabled)> tasks)
    {
        var enabled = tasks.Where(t => t.Enabled).Select(t => t.Task).ToList();
        if (enabled.Count > 0)
            return CheckOutcome.Fail($"Máy này còn lịch đồng bộ cũ đang bật: {List(enabled)}. Lịch này xóa trên TV những gì bạn đưa lên bằng phần mềm.",
                "Nhờ IT tắt lịch này (xem trang Chuyển sang phần mềm mới trong hướng dẫn cho IT).");
        return CheckOutcome.Ok(tasks.Count > 0
            ? "Có lịch đồng bộ cũ nhưng đã tắt."
            : "Không có lịch đồng bộ cũ trên máy này.");
    }

    /// <summary>Text to paste into a message to IT. Holds no API key.</summary>
    public static string Report(string version, string server, string windows, DateTime when,
        IEnumerable<(string Title, CheckOutcome Outcome)> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Chẩn đoán Signage Viên Ba {version} · {when:dd/MM/yyyy HH:mm}");
        sb.AppendLine($"Máy chủ: {server} · Windows {windows}");
        foreach (var (title, o) in rows)
        {
            var tag = o.Status switch
            {
                CheckStatus.Ok => "[OK]",
                CheckStatus.Warning => "[CẢNH BÁO]",
                CheckStatus.Error => "[LỖI]",
                CheckStatus.Skipped => "[BỎ QUA]",
                _ => "[ĐANG KIỂM TRA]",
            };
            sb.AppendLine($"{tag} {title}: {o.Detail}");
            if (o.Fix.Length > 0) sb.AppendLine($"    Cách xử lý: {o.Fix}");
        }
        return sb.ToString();
    }

    private static string List(IReadOnlyList<string> names) =>
        names.Count <= 3 ? string.Join(", ", names) : $"{string.Join(", ", names.Take(3))} và {names.Count - 3} file khác";

    private static string Size(long bytes) =>
        bytes >= 1024L * 1024 * 1024 ? $"{bytes / (1024.0 * 1024 * 1024):0.#} GB" : $"{bytes / (1024.0 * 1024):0} MB";
}
