using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace SignageVienBa.App;

internal static class LinkCopy
{
    private const string Done = "Đã chép";

    /// <summary>Puts the link on the clipboard and shows "Đã chép" on the button for a moment.</summary>
    public static void Copy(Button button, string url)
    {
        try
        {
            Clipboard.SetText(url);
        }
        catch (ExternalException)
        {
            // another program is holding the clipboard
            MessageBox.Show(Window.GetWindow(button)!, "Không chép được link. Thử bấm lại.", "Signage Viên Ba",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (Equals(button.Content, Done)) return;
        var label = button.Content;
        button.Content = Done;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.6) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            button.Content = label;
        };
        timer.Start();
    }
}
