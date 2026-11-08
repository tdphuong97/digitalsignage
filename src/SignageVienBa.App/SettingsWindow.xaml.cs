using System.Windows;
using SignageVienBa.Core;

namespace SignageVienBa.App;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly Func<Task<UpdateCheck>> _checkUpdate;

    /// <param name="pendingVersion">A newer version the app already knows about, so Cập nhật ngay shows at once.</param>
    public SettingsWindow(AppSettings settings, bool firstRun, Func<Task<UpdateCheck>> checkUpdate, string? pendingVersion = null)
    {
        _settings = settings;
        _checkUpdate = checkUpdate;
        InitializeComponent();
        VersionText.Text = $"Phiên bản {MainWindow.CurrentVersion}";
        if (pendingVersion is not null) ShowUpdate(new UpdateCheck(MainWindow.AvailableText(pendingVersion), true));
        UrlBox.Text = string.IsNullOrWhiteSpace(settings.WorkerUrl) ? AppSettings.DefaultUrl : settings.WorkerUrl;
        KeyBox.Password = settings.ApiKey;
        if (firstRun)
            Intro.Text = "Chào bạn! Nhập mã API một lần để bắt đầu đưa nội dung lên TV. Mã được mã hóa, chỉ dùng được với tài khoản Windows này.";
        Loaded += (_, _) =>
        {
            if (KeyBox.Password.Length == 0) KeyBox.Focus();
        };
    }

    private SignageClient? MakeClient()
    {
        if (KeyBox.Password.Trim().Length == 0)
        {
            ShowResult("Chưa nhập mã API.", ok: false);
            return null;
        }
        try
        {
            return new SignageClient(AppSettings.Http, UrlBox.Text, KeyBox.Password);
        }
        catch (SignageException e)
        {
            ShowResult(e.Message, ok: false);
            return null;
        }
    }

    private async void Check_Click(object sender, RoutedEventArgs e)
    {
        if (MakeClient() is not { } client) return;
        CheckButton.IsEnabled = false;
        ShowResult("Đang kiểm tra…", ok: null);
        try
        {
            await client.CheckAsync(CancellationToken.None);
            ShowResult("Kết nối được, mã API đúng.", ok: true);
        }
        catch (SignageException ex)
        {
            ShowResult(ex.Message, ok: false);
        }
        finally
        {
            CheckButton.IsEnabled = true;
        }
    }

    private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        UpdateCheckButton.IsEnabled = false;
        UpdateResult.Text = "Đang kiểm tra…";
        try
        {
            ShowUpdate(await _checkUpdate());
        }
        finally
        {
            UpdateCheckButton.IsEnabled = true;
        }
    }

    private void ShowUpdate(UpdateCheck check)
    {
        UpdateResult.Text = check.Message;
        UpdateNowButton.Visibility = check.Available ? Visibility.Visible : Visibility.Collapsed;
        UpdateCheckButton.Visibility = check.Available ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>The main window starts the update once this window is closed.</summary>
    public bool UpdateRequested { get; private set; }

    private void UpdateNow_Click(object sender, RoutedEventArgs e)
    {
        // Keep what was typed here if it is complete and valid; the update restarts the app.
        var changed = UrlBox.Text.Trim().TrimEnd('/') != _settings.WorkerUrl || KeyBox.Password != _settings.ApiKey;
        UpdateRequested = true;
        if (changed && TrySave(quiet: true)) DialogResult = true;
        else DialogResult = false;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (TrySave(quiet: false)) DialogResult = true;
    }

    private bool TrySave(bool quiet)
    {
        if (quiet)
        {
            if (KeyBox.Password.Trim().Length == 0) return false;
            try { _ = new SignageClient(AppSettings.Http, UrlBox.Text, KeyBox.Password); }
            catch (SignageException) { return false; }
        }
        else if (MakeClient() is null) return false;

        _settings.WorkerUrl = UrlBox.Text.Trim().TrimEnd('/');
        _settings.ApiKey = KeyBox.Password;
        try
        {
            _settings.Save();
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            if (!quiet) ShowResult("Không lưu được cài đặt: " + ex.Message, ok: false);
            return false;
        }
        return true;
    }

    private void ShowResult(string text, bool? ok)
    {
        CheckResult.Text = text;
        CheckResult.Foreground = (System.Windows.Media.Brush)FindResource(ok switch
        {
            true => "SuccessBrush",
            false => "DangerBrush",
            null => "MutedBrush",
        });
    }
}
