using System.Windows;
using SignageVienBa.Core;

namespace SignageVienBa.App;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly Func<Task<string>> _checkUpdate;

    public SettingsWindow(AppSettings settings, bool firstRun, Func<Task<string>> checkUpdate)
    {
        _settings = settings;
        _checkUpdate = checkUpdate;
        InitializeComponent();
        VersionText.Text = $"Phiên bản {MainWindow.CurrentVersion}";
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
            UpdateResult.Text = await _checkUpdate();
        }
        finally
        {
            UpdateCheckButton.IsEnabled = true;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (MakeClient() is null) return;
        _settings.WorkerUrl = UrlBox.Text.Trim().TrimEnd('/');
        _settings.ApiKey = KeyBox.Password;
        try
        {
            _settings.Save();
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            ShowResult("Không lưu được cài đặt: " + ex.Message, ok: false);
            return;
        }
        DialogResult = true;
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
