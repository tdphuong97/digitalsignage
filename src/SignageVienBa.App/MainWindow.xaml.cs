using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SignageVienBa.Core;

namespace SignageVienBa.App;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly AppSettings _settings;
    private readonly IReadOnlyList<string> _startupPaths;
    private readonly bool _justUpdated;
    private readonly Dictionary<int, IReadOnlyList<string>> _remoteKeys = [];
    private readonly Dictionary<int, string> _counts = new() { [1] = "Đang tải…", [2] = "Đang tải…" };
    private TvScreen _tv = new(1);
    private CancellationTokenSource? _run;
    private int _loadVersion;
    private bool _busy;
    private string _remoteMessage = "";
    private bool _remoteFailed;
    private string? _lastResult;

    public MainWindow(AppSettings settings, IReadOnlyList<string> startupPaths, bool justUpdated = false)
    {
        _settings = settings;
        _startupPaths = startupPaths;
        _justUpdated = justUpdated;
        InitializeComponent();
        DataContext = this;
        Queue.CollectionChanged += (_, e) =>
        {
            if (e.NewItems is not null)
                foreach (QueueItem q in e.NewItems) q.PropertyChanged += (_, _) => QueueChanged();
            QueueChanged();
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // ---------- bound state ----------

    public UpdateBanner Update { get; } = new();
    public ObservableCollection<RemoteItem> RemoteItems { get; } = [];
    public ObservableCollection<QueueItem> Queue { get; } = [];

    public string Tv1Count => _counts[1];
    public string Tv2Count => _counts[2];

    public string RemoteTitle => _remoteKeys.TryGetValue(_tv.Number, out var keys)
        ? $"Đang chiếu trên {_tv.Label} · {keys.Count}"
        : $"Đang chiếu trên {_tv.Label}";

    public string RemoteMessage => _remoteMessage;
    public bool ShowRemoteMessage => _remoteMessage.Length > 0;
    public bool RemoteFailed => _remoteFailed;

    public bool IsBusy => _busy;
    public bool IsIdle => !_busy;
    public bool CanSwitchTv => !_busy;

    private int WaitingCount => Queue.Count(q => q.State == QueueState.Waiting);
    public bool QueueEmpty => Queue.Count == 0;
    public bool QueueHasItems => Queue.Count > 0;
    public double DropZoneHeight => QueueEmpty ? 180 : 0;
    public bool HasFinished => !_busy && Queue.Any(q => q.State is QueueState.Done or QueueState.Failed);
    public bool CanStart => !_busy && WaitingCount > 0;
    public string StartText => WaitingCount > 0 ? $"Đưa lên {_tv.Label} ({WaitingCount} file)" : $"Đưa lên {_tv.Label}";

    public string Summary
    {
        get
        {
            if (_busy)
            {
                var running = Queue.FirstOrDefault(q => q.State == QueueState.Running);
                var left = WaitingCount;
                return running is null
                    ? $"Đang đưa lên {_tv.Label}…"
                    : $"Đang đưa lên {_tv.Label} · còn {left} file chờ sau file này";
            }
            if (WaitingCount > 0) return $"{WaitingCount} file chờ đưa lên {_tv.Label}";
            return _lastResult ?? (QueueEmpty ? "Chưa có file nào trong danh sách" : "");
        }
    }

    private void QueueChanged()
    {
        Raise(nameof(QueueEmpty));
        Raise(nameof(QueueHasItems));
        Raise(nameof(DropZoneHeight));
        Raise(nameof(HasFinished));
        Raise(nameof(CanStart));
        Raise(nameof(StartText));
        Raise(nameof(Summary));
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        Raise(nameof(IsBusy));
        Raise(nameof(IsIdle));
        Raise(nameof(CanSwitchTv));
        QueueChanged();
    }

    private void SetRemoteMessage(string message, bool failed)
    {
        _remoteMessage = message;
        _remoteFailed = failed;
        Raise(nameof(RemoteMessage));
        Raise(nameof(ShowRemoteMessage));
        Raise(nameof(RemoteFailed));
    }

    private void SetCount(int tv, string text)
    {
        _counts[tv] = text;
        Raise(tv == 1 ? nameof(Tv1Count) : nameof(Tv2Count));
    }

    private static string CountText(int n) => n == 0 ? "Chưa có nội dung" : $"{n} nội dung";

    // ---------- start / close ----------

    private async void Window_ContentRendered(object? sender, EventArgs e)
    {
        if (!_settings.IsComplete) OpenSettings(firstRun: true);
        if (_startupPaths.Count > 0) AddPaths(_startupPaths);
        if (VideoTools.Locate() is null)
            MessageBox.Show(this, "Không tìm thấy bộ nén video (thư mục ffmpeg cạnh phần mềm). Ảnh vẫn đưa lên được, nhưng video thì không. Hãy cài lại phần mềm.",
                "Signage Viên Ba", MessageBoxButton.OK, MessageBoxImage.Warning);
        await Task.WhenAll(LoadRemoteAsync(), LoadCountAsync(new TvScreen(2)));

        if (_justUpdated) Update.ShowUpdated(CurrentVersion);
        else await CheckForUpdateAsync();
        // The PC may keep the window open for days: look again every few hours.
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromHours(6) };
        timer.Tick += async (_, _) => { if (Update.State == UpdateBanner.Mode.Hidden) await CheckForUpdateAsync(); };
        timer.Start();
    }

    // ---------- updates ----------

    public static string CurrentVersion => typeof(App).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    /// <summary>Looks for a newer release; returns a sentence for the Settings window.</summary>
    public async Task<string> CheckForUpdateAsync()
    {
        try
        {
            var info = await new Updater(AppSettings.Http, Updater.Feed).CheckAsync(CurrentVersion, CancellationToken.None);
            if (info is null) return $"Bạn đang dùng bản mới nhất ({CurrentVersion}).";
            Update.ShowAvailable(info);
            return $"Có bản mới {info.Version}. Bấm Cập nhật ngay ở đầu cửa sổ chính.";
        }
        catch (UpdateException e)
        {
            return e.Message;
        }
    }

    private async void UpdatePrimary_Click(object sender, RoutedEventArgs e)
    {
        if (Update.Info is null)
        {
            Update.Hide();
            await CheckForUpdateAsync();
            return;
        }
        if (_busy)
        {
            MessageBox.Show(this, "Đang đưa file lên TV. Đợi xong rồi bấm Cập nhật ngay.", "Signage Viên Ba",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var info = Update.Info;
        Update.ShowDownloading(0);
        string installer;
        try
        {
            installer = await new Updater(AppSettings.Http, Updater.Feed).DownloadAsync(info, Updater.DownloadFolder,
                new Progress<double>(f => { if (Update.State == UpdateBanner.Mode.Downloading) Update.ShowDownloading(f); }),
                CancellationToken.None);
        }
        catch (UpdateException ex)
        {
            Update.ShowFailed(ex.Message);
            return;
        }

        try
        {
            // The installer waits for this window to close, installs silently, then opens the new version.
            Process.Start(new ProcessStartInfo(installer, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /UPDATE=1") { UseShellExecute = true });
        }
        catch (Win32Exception ex)
        {
            Update.ShowFailed("Không chạy được bộ cài bản mới: " + ex.Message);
            return;
        }
        Application.Current.Shutdown();
    }

    private void UpdateSecondary_Click(object sender, RoutedEventArgs e)
    {
        if (Update.State == UpdateBanner.Mode.Available && Update.Info?.Page is { } page)
        {
            try { Process.Start(new ProcessStartInfo(page) { UseShellExecute = true }); }
            catch (Win32Exception) { }
            return;
        }
        Update.Hide();
    }

    // The user guide (docs\, user pages only) is built into HuongDan.html next to the exe by build.ps1.
    internal static string GuidePath =>
        Environment.GetEnvironmentVariable("SIGNAGE_GUIDE") is { Length: > 0 } custom
            ? custom
            : Path.Combine(AppContext.BaseDirectory, "HuongDan.html");

    private void Guide_Click(object sender, RoutedEventArgs e) => OpenGuide();

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.F1) return;
        e.Handled = true;
        OpenGuide();
    }

    private void OpenGuide()
    {
        var path = GuidePath;
        if (!File.Exists(path))
        {
            MessageBox.Show(this, "Không tìm thấy file hướng dẫn. Hãy cài lại phần mềm bằng bộ cài mới nhất.",
                "Signage Viên Ba", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            MessageBox.Show(this, "Không mở được hướng dẫn: " + ex.Message, "Signage Viên Ba",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!_busy) return;
        var answer = MessageBox.Show(this, "Đang đưa file lên TV. Thoát bây giờ sẽ dừng giữa chừng.\n\nVẫn thoát?",
            "Signage Viên Ba", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes) { e.Cancel = true; return; }
        _run?.Cancel();
    }

    // ---------- TV list ----------

    private async void Tv_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        _tv = new TvScreen(int.Parse((string)((FrameworkElement)sender).Tag));
        _lastResult = null;
        Raise(nameof(RemoteTitle));
        QueueChanged();
        await LoadRemoteAsync();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadRemoteAsync();

    private async Task LoadRemoteAsync(bool quiet = false)
    {
        var tv = _tv;
        var version = ++_loadVersion;
        if (!quiet)
        {
            RemoteItems.Clear();
            SetRemoteMessage("Đang tải danh sách…", failed: false);
        }
        if (!_settings.IsComplete)
        {
            SetRemoteMessage("Chưa nhập mã API. Bấm Cài đặt ở góc trên bên phải để bắt đầu.", failed: false);
            SetCount(tv.Number, "Chưa cài đặt");
            return;
        }
        try
        {
            var keys = await _settings.CreateClient().ListAsync(tv, CancellationToken.None);
            if (version != _loadVersion) return;
            _remoteKeys[tv.Number] = keys;
            RemoteItems.Clear();
            for (var i = 0; i < keys.Count; i++)
                RemoteItems.Add(new RemoteItem(i + 1, keys[i], NameRules.DisplayName(tv, keys[i])));
            SetCount(tv.Number, CountText(keys.Count));
            SetRemoteMessage(keys.Count == 0 ? $"{tv.Label} chưa có nội dung nào.\nThêm ảnh hoặc video ở khung bên phải." : "", failed: false);
            Raise(nameof(RemoteTitle));
            MarkReplacements();
        }
        catch (SignageException ex)
        {
            if (version != _loadVersion) return;
            RemoteItems.Clear();
            SetRemoteMessage(ex.Message, failed: true);
            SetCount(tv.Number, "Không tải được");
        }
    }

    private async Task LoadCountAsync(TvScreen tv)
    {
        if (!_settings.IsComplete) { SetCount(tv.Number, "Chưa cài đặt"); return; }
        try
        {
            var keys = await _settings.CreateClient().ListAsync(tv, CancellationToken.None);
            _remoteKeys[tv.Number] = keys;
            SetCount(tv.Number, CountText(keys.Count));
        }
        catch (SignageException)
        {
            SetCount(tv.Number, "Không tải được");
        }
    }

    /// <summary>Tells waiting files whether they will replace one already on the chosen TV.</summary>
    private void MarkReplacements()
    {
        var keys = _remoteKeys.TryGetValue(_tv.Number, out var k) ? k : [];
        foreach (var q in Queue.Where(q => q.State == QueueState.Waiting))
            q.Replaces = keys.Contains(NameRules.KeyFor(_tv, q.TargetName), StringComparer.OrdinalIgnoreCase);
    }

    private void View_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not RemoteItem item) return;
        try
        {
            var url = _settings.CreateClient().FileUri(item.Key).AbsoluteUri;
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is SignageException or Win32Exception)
        {
            MessageBox.Show(this, ex.Message, "Signage Viên Ba", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not RemoteItem item) return;
        var tv = _tv;
        var answer = MessageBox.Show(this, $"Xóa “{item.DisplayName}” khỏi {tv.Label}?\n\nTV sẽ không chiếu nội dung này nữa.",
            "Xóa khỏi TV", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes) return;
        var button = (Button)sender;
        button.IsEnabled = false;
        try
        {
            await _settings.CreateClient().DeleteAsync(item.Key, CancellationToken.None);
        }
        catch (SignageException ex)
        {
            MessageBox.Show(this, ex.Message, "Không xóa được", MessageBoxButton.OK, MessageBoxImage.Warning);
            button.IsEnabled = true;
            return;
        }
        if (tv == _tv) await LoadRemoteAsync(quiet: true);
    }

    // ---------- adding files ----------

    private void PickFiles_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Chọn ảnh hoặc video",
            Multiselect = true,
            Filter = "Ảnh và video|*.jpg;*.jpeg;*.png;*.mp4;*.mov;*.m4v;*.avi;*.mkv;*.wmv;*.webm;*.mpg;*.mpeg;*.3gp;*.flv;*.ts;*.mts;*.m2ts|Tất cả file|*.*",
        };
        if (dialog.ShowDialog(this) == true) AddPaths(dialog.FileNames);
    }

    private void PickFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Chọn thư mục chứa ảnh, video", Multiselect = true };
        if (dialog.ShowDialog(this) == true) AddPaths(dialog.FolderNames);
    }

    public void AddPaths(IEnumerable<string> paths)
    {
        var list = paths.ToList();
        var files = MediaTypes.Expand(list).ToList();
        var added = 0;
        foreach (var file in files)
        {
            if (Queue.Any(q => q.State == QueueState.Waiting && string.Equals(q.SourcePath, file, StringComparison.OrdinalIgnoreCase)))
                continue;
            var taken = Queue.Where(q => q.State != QueueState.Failed).Select(q => q.TargetName).ToList();
            Queue.Add(new QueueItem(file, NameRules.Unique(Uploader.TargetName(file), taken)));
            added++;
        }
        if (added == 0 && list.Count > 0)
            MessageBox.Show(this, "Không tìm thấy ảnh hoặc video nào trong những gì bạn chọn.", "Signage Viên Ba",
                MessageBoxButton.OK, MessageBoxImage.Information);
        _lastResult = null;
        MarkReplacements();
        QueueChanged();
    }

    private void RemoveQueued_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is QueueItem item && item.CanRemove) Queue.Remove(item);
    }

    private void ClearFinished_Click(object sender, RoutedEventArgs e)
    {
        foreach (var q in Queue.Where(q => q.State is QueueState.Done or QueueState.Failed).ToList()) Queue.Remove(q);
        _lastResult = null;
        QueueChanged();
    }

    // ---------- drag and drop ----------

    private void Window_DragEnter(object sender, DragEventArgs e)
    {
        Window_DragOver(sender, e);
        if (e.Effects != DragDropEffects.None) HighlightDrop(true);
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_DragLeave(object sender, DragEventArgs e) => HighlightDrop(false);

    private void Window_Drop(object sender, DragEventArgs e)
    {
        HighlightDrop(false);
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths) AddPaths(paths);
        e.Handled = true;
    }

    private void HighlightDrop(bool on)
    {
        DropRect.Stroke = (System.Windows.Media.Brush)FindResource(on ? "AccentBrush" : "LineStrongBrush");
        DropRect.Fill = on ? (System.Windows.Media.Brush)FindResource("AccentSoftBrush")
                           : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFA, 0xFB, 0xFD));
        DropText.Text = on ? "Thả vào để thêm vào danh sách" : "Kéo thả ảnh, video hoặc cả thư mục vào đây";
    }

    // ---------- upload ----------

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        if (!_settings.IsComplete && !OpenSettings(firstRun: true)) return;

        var tv = _tv;
        var uploader = new Uploader(VideoTools.Locate(), _settings.CreateClient());
        int ok = 0, failed = 0;
        _run = new CancellationTokenSource();
        _lastResult = null;
        SetBusy(true);
        var stopped = false;
        try
        {
            while (Queue.FirstOrDefault(q => q.State == QueueState.Waiting) is { } item)
            {
                item.Start();
                Raise(nameof(Summary));
                try
                {
                    await uploader.RunAsync(item.SourcePath, item.TargetName, tv, new Progress<JobProgress>(item.Report), _run.Token);
                    item.Succeed(tv.Label);
                    ok++;
                    if (tv == _tv) _ = LoadRemoteAsync(quiet: true);
                }
                catch (OperationCanceledException)
                {
                    item.ResetToWaiting("đã dừng");
                    stopped = true;
                    break;
                }
                catch (SignageException ex)
                {
                    item.Fail(ex.Message);
                    failed++;
                }
                catch (Exception ex)
                {
                    item.Fail("Lỗi không mong muốn: " + ex.Message);
                    App.Log(ex);
                    failed++;
                }
            }
        }
        finally
        {
            _run.Dispose();
            _run = null;
            _lastResult = (ok, failed, stopped) switch
            {
                (_, _, true) => $"Đã dừng. {ok} file đã lên {tv.Label}.",
                (0, > 0, _) => $"Không đưa lên được {failed} file. Xem lỗi ở từng dòng.",
                (_, > 0, _) => $"Xong: {ok} file đã lên {tv.Label}, {failed} file lỗi.",
                _ => $"Xong: {ok} file đã lên {tv.Label}. TV sẽ chiếu từ vòng sau.",
            };
            SetBusy(false);
        }
        if (tv == _tv) await LoadRemoteAsync(quiet: true);
        else await LoadCountAsync(tv);
    }

    private void Stop_Click(object sender, RoutedEventArgs e) => _run?.Cancel();

    // ---------- settings ----------

    private async void Settings_Click(object sender, RoutedEventArgs e)
    {
        if (OpenSettings(firstRun: false))
            await Task.WhenAll(LoadRemoteAsync(), LoadCountAsync(new TvScreen(_tv.Number == 1 ? 2 : 1)));
    }

    private bool OpenSettings(bool firstRun)
    {
        var window = new SettingsWindow(_settings, firstRun, CheckForUpdateAsync) { Owner = this };
        return window.ShowDialog() == true;
    }
}
