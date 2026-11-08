using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SignageVienBa.Core;

namespace SignageVienBa.App;

/// <summary>One line of the diagnosis window.</summary>
public sealed class CheckRow(string title) : Notifier
{
    private CheckOutcome _outcome = Waiting;
    private static readonly CheckOutcome Waiting = new(CheckStatus.Running, "Đang kiểm tra…");

    public string Title { get; } = title;

    public CheckOutcome Outcome
    {
        get => _outcome;
        set { Set(ref _outcome, value); Raise(nameof(Status), nameof(Detail), nameof(Fix), nameof(HasFix)); }
    }

    public CheckStatus Status => _outcome.Status;
    public string Detail => _outcome.Detail;
    public string Fix => _outcome.Fix;
    public bool HasFix => _outcome.Fix.Length > 0;

    public void Reset() => Outcome = Waiting;
}

/// <summary>
/// "Chẩn đoán": checks the server, the API key, what each TV plays, the slideshow page, ffmpeg, disk space, the
/// version, the old Task Scheduler sync and WebView2, and says what to do about each problem. The result can be
/// copied for IT; it never holds the API key. Only reads, except the same harmless key check as Kiểm tra kết nối.
/// </summary>
public partial class DiagnosticsWindow : Window
{
    private static DiagnosticsWindow? _open;
    private readonly AppSettings _settings;
    private readonly Func<Task<UpdateCheck>> _checkUpdate;
    private bool _running;

    private readonly CheckRow _server = new("Kết nối máy chủ");
    private readonly CheckRow _key = new("Mã API");
    private readonly CheckRow _tv1 = new("Nội dung TV 1");
    private readonly CheckRow _tv2 = new("Nội dung TV 2");
    private readonly CheckRow _page = new("Trang chiếu trên TV");
    private readonly CheckRow _live = new("Tín hiệu từ TV");
    private LiveStatus? _lastLive;
    private readonly CheckRow _oldSync = new("Lịch đồng bộ cũ trên máy này");
    private readonly CheckRow _ffmpeg = new("Bộ nén video");
    private readonly CheckRow _disk = new("Ổ đĩa");
    private readonly CheckRow _version = new("Phiên bản phần mềm");
    private readonly CheckRow _viewer = new("Cửa sổ xem và hướng dẫn");

    public ObservableCollection<CheckRow> Rows { get; }

    private DiagnosticsWindow(AppSettings settings, Func<Task<UpdateCheck>> checkUpdate)
    {
        _settings = settings;
        _checkUpdate = checkUpdate;
        Rows = [_server, _key, _tv1, _tv2, _page, _live, _oldSync, _ffmpeg, _disk, _version, _viewer];
        InitializeComponent();
        DataContext = this;
        ContentRendered += async (_, _) => await RunAsync();
    }

    /// <summary>Opens the window and runs the checks; if it is already open, brings it forward and runs them again.</summary>
    public static void Open(Window owner, AppSettings settings, Func<Task<UpdateCheck>> checkUpdate)
    {
        if (_open is { } open)
        {
            if (open.WindowState == WindowState.Minimized) open.WindowState = WindowState.Normal;
            open.Activate();
            _ = open.RunAsync();
            return;
        }
        _open = new DiagnosticsWindow(settings, checkUpdate) { Owner = owner };
        _open.Closed += (_, _) => _open = null;
        _open.Show();
    }

    private async Task RunAsync()
    {
        if (_running) return;
        _running = true;
        RerunButton.IsEnabled = CopyButton.IsEnabled = false;
        foreach (var row in Rows) row.Reset();
        SetSummary("Đang kiểm tra…", "MutedBrush", "Kiểm tra kết nối, mã API, nội dung trên 2 TV và máy này. Mất vài giây.");
        try
        {
            var local = Task.WhenAll(
                Run(_oldSync, () => Task.Run(() => Diagnosis.OldSync(OldSyncTasks.Find()))),
                Run(_ffmpeg, CheckFfmpegAsync),
                Run(_disk, () => Task.FromResult(CheckDisk())),
                Run(_version, CheckVersionAsync),
                Run(_viewer, () => Task.FromResult(CheckViewer())));
            await CheckServerAndTvsAsync();
            await local;
        }
        finally
        {
            _running = false;
            RerunButton.IsEnabled = CopyButton.IsEnabled = true;
            Summarize();
        }
    }

    /// <summary>Runs one check; anything unexpected becomes "could not check" instead of a crash.</summary>
    private static async Task Run(CheckRow row, Func<Task<CheckOutcome>> check)
    {
        try
        {
            row.Outcome = await check();
        }
        catch (Exception e)
        {
            Trace.TraceWarning($"Diagnosis '{row.Title}' failed: {e}");
            row.Outcome = CheckOutcome.Warn("Không kiểm tra được: " + e.Message,
                "Bấm Chạy lại. Vẫn vậy thì chép kết quả gửi IT.");
        }
    }

    private async Task CheckServerAndTvsAsync()
    {
        var dependents = new[] { _key, _tv1, _tv2, _page, _live };
        SignageClient client;
        try
        {
            client = _settings.CreateClient();
        }
        catch (SignageException e)
        {
            _server.Outcome = CheckOutcome.Fail(e.Message, "Vào Cài đặt, sửa Địa chỉ máy chủ (hỏi IT nếu không rõ).");
            foreach (var row in dependents) row.Outcome = CheckOutcome.Skip("Bỏ qua vì chưa kết nối được máy chủ.");
            return;
        }

        var tv1 = new TvScreen(1);
        var tv2 = new TvScreen(2);
        IReadOnlyList<string> keys1;
        var clock = Stopwatch.StartNew();
        try
        {
            keys1 = await client.ListAsync(tv1, CancellationToken.None);
        }
        catch (SignageException e)
        {
            _server.Outcome = CheckOutcome.Fail(e.Message,
                "Mở thử một trang web trên máy này. Không vào được thì kiểm tra mạng, Wi-Fi; vào được thì báo IT.");
            foreach (var row in dependents) row.Outcome = CheckOutcome.Skip("Bỏ qua vì chưa kết nối được máy chủ.");
            return;
        }
        _server.Outcome = CheckOutcome.Ok($"Máy chủ trả lời sau {clock.ElapsedMilliseconds} ms.");
        _tv1.Outcome = Diagnosis.TvContent(tv1, keys1);

        await Task.WhenAll(
            Run(_tv2, async () =>
            {
                try { return Diagnosis.TvContent(tv2, await client.ListAsync(tv2, CancellationToken.None)); }
                catch (SignageException e) { return CheckOutcome.Fail(e.Message, "Bấm Chạy lại sau ít phút. Vẫn lỗi thì báo IT."); }
            }),
            Run(_key, async () =>
            {
                if (!_settings.IsComplete)
                    return CheckOutcome.Fail("Chưa nhập mã API, nên chưa đưa lên hay xóa được.", "Vào Cài đặt, nhập mã API (hỏi IT).");
                try
                {
                    await client.CheckAsync(CancellationToken.None);
                    return CheckOutcome.Ok("Mã API đúng, đưa lên và xóa được.");
                }
                catch (SignageException e)
                {
                    return CheckOutcome.Fail(e.Message, "Vào Cài đặt, nhập lại mã API. Không có mã thì hỏi IT.");
                }
            }),
            Run(_live, async () =>
            {
                try
                {
                    _lastLive = await client.StatusAsync(CancellationToken.None);
                    return Live.Diagnose(_lastLive, DateTime.Now);
                }
                catch (SignageException e)
                {
                    return CheckOutcome.Warn(e.Message, "Bấm Chạy lại sau ít phút.");
                }
            }),
            Run(_page, async () =>
            {
                try
                {
                    await client.CheckPageAsync(tv1, CancellationToken.None);
                    return CheckOutcome.Ok("Máy chủ trả về đúng trang mà TV mở để chiếu.");
                }
                catch (SignageException e)
                {
                    return CheckOutcome.Fail(e.Message, "Báo IT: trang chiếu (index.html) trên máy chủ bị thiếu hoặc lỗi.");
                }
            }));
    }

    private static async Task<CheckOutcome> CheckFfmpegAsync()
    {
        if (VideoTools.Locate() is not { } tools)
            return CheckOutcome.Fail("Không tìm thấy bộ nén video (thư mục ffmpeg cạnh phần mềm). Ảnh vẫn đưa lên được, video thì không.",
                "Cài lại phần mềm bằng bộ cài mới nhất.");
        try
        {
            return CheckOutcome.Ok($"Sẵn sàng ({await tools.VersionAsync(CancellationToken.None)}).");
        }
        catch (SignageException e)
        {
            return CheckOutcome.Fail(e.Message, "Cài lại phần mềm bằng bộ cài mới nhất. Phần mềm diệt virus có thể đã chặn ffmpeg.");
        }
    }

    private CheckOutcome CheckDisk()
    {
        // the drive videos are compressed on (the temp folder)
        var root = Path.GetPathRoot(Path.GetFullPath(Uploader.TempFolder)) ?? "C:\\";
        var outcome = Diagnosis.DiskSpace(new DriveInfo(root).AvailableFreeSpace);
        return outcome with { Detail = $"Ổ {root.TrimEnd('\\')} {char.ToLower(outcome.Detail[0])}{outcome.Detail[1..]}" };
    }

    private async Task<CheckOutcome> CheckVersionAsync()
    {
        var check = await _checkUpdate();
        if (check.Available) return CheckOutcome.Warn(check.Message, "Bấm Cập nhật ngay ở đầu cửa sổ chính (hoặc trong Cài đặt).");
        if (check.Failed) return CheckOutcome.Warn(check.Message, "Kiểm tra mạng rồi bấm Chạy lại.");
        return CheckOutcome.Ok(check.Message);
    }

    private static CheckOutcome CheckViewer()
    {
        if (!File.Exists(MainWindow.GuidePath))
            return CheckOutcome.Warn("Không tìm thấy file hướng dẫn cạnh phần mềm.", "Cài lại phần mềm bằng bộ cài mới nhất.");
        if (WebViews.RuntimeVersion() is not { } version)
            return CheckOutcome.Warn("Máy thiếu Microsoft Edge WebView2 Runtime, nên hướng dẫn và nút Xem mở bằng trình duyệt.",
                "Nhờ IT cài WebView2 Runtime (bản Evergreen) của Microsoft.");
        return CheckOutcome.Ok($"Sẵn sàng (WebView2 {version}).");
    }

    private void Summarize()
    {
        var errors = Rows.Count(r => r.Status == CheckStatus.Error);
        var warnings = Rows.Count(r => r.Status == CheckStatus.Warning);
        var when = $"Kiểm tra lúc {DateTime.Now:HH:mm}.";
        if (errors > 0)
            SetSummary($"Có {errors} lỗi cần xử lý", "DangerBrush", $"{when} Làm theo dòng Cách xử lý ở từng mục có dấu ✕.");
        else if (warnings > 0)
            SetSummary($"Có {warnings} điều cần xem", "WarnBrush", $"{when} Phần mềm vẫn dùng được; xem dòng Cách xử lý ở mục có dấu !.");
        else
            SetSummary("Mọi thứ đều ổn", "SuccessBrush", when);
    }

    private void SetSummary(string text, string brushKey, string detail)
    {
        SummaryText.Text = text;
        SummaryText.Foreground = (Brush)FindResource(brushKey);
        SummaryDetail.Text = detail;
    }

    private async void Rerun_Click(object sender, RoutedEventArgs e) => await RunAsync();

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        var report = Diagnosis.Report(MainWindow.CurrentVersion, _settings.WorkerUrl, System.Environment.OSVersion.Version.ToString(),
            DateTime.Now, Rows.Select(r => (r.Title, r.Outcome)));
        // what each TV's slideshow page reported lately (load, offline start, skipped files, reloads)
        if (_lastLive is { } live)
            foreach (var tv in TvScreen.All)
                if (live.For(tv) is { } s)
                    report += $"\nNhật ký {tv.Label} (trang chiếu bản {s.PageVersion}, {s.Browser}):\n"
                              + string.Join("\n", Live.EventLines(s).TakeLast(20).Select(l => "  " + l)) + "\n";
        LinkCopy.Copy((Button)sender, report);
    }
}
