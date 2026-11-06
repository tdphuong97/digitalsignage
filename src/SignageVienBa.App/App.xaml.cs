using System.IO;
using System.Windows;
using System.Windows.Threading;
using SignageVienBa.Core;

namespace SignageVienBa.App;

public partial class App : Application
{
    // Held while the app runs; an update's installer waits for it to disappear before replacing files.
    private static Mutex? _running;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandled;
        _running = new Mutex(false, @"Local\SignageVienBa-" + Environment.UserName);
        Uploader.CleanTempFolder();
        Updater.CleanDownloads();

        // Files dropped on the shortcut or the exe arrive as arguments; the installer adds --updated.
        var updated = e.Args.Contains("--updated");
        var paths = e.Args.Where(a => a != "--updated").ToList();
        var window = new MainWindow(AppSettings.Load(), paths, updated);
        MainWindow = window;
        window.Show();
    }

    private void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log(e.Exception);
        MessageBox.Show("Có lỗi không mong muốn: " + e.Exception.Message + "\n\nPhần mềm vẫn chạy tiếp. Nếu lỗi lặp lại, gửi file log.txt trong "
                        + AppSettings.DataFolder + " cho bộ phận IT.", "Signage Viên Ba", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    public static void Log(Exception e)
    {
        try
        {
            Directory.CreateDirectory(AppSettings.DataFolder);
            File.AppendAllText(Path.Combine(AppSettings.DataFolder, "log.txt"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {e}\n\n");
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
