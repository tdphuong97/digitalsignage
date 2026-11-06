using SignageVienBa.Core;

namespace SignageVienBa.App;

/// <summary>The strip at the top of the main window that offers, downloads or reports an update.</summary>
public sealed class UpdateBanner : Notifier
{
    public enum Mode { Hidden, Available, Downloading, Failed, Updated }

    private Mode _mode = Mode.Hidden;
    private string _title = "";
    private string _detail = "";
    private double _percent;

    public UpdateInfo? Info { get; private set; }

    public Mode State
    {
        get => _mode;
        private set
        {
            Set(ref _mode, value);
            Raise(nameof(Visible), nameof(Downloading), nameof(PrimaryText), nameof(PrimaryVisible),
                  nameof(SecondaryText), nameof(SecondaryVisible), nameof(Glyph));
        }
    }

    public bool Visible => State != Mode.Hidden;
    public bool Downloading => State == Mode.Downloading;
    public string Title { get => _title; private set => Set(ref _title, value); }
    public string Detail { get => _detail; private set { Set(ref _detail, value); Raise(nameof(HasDetail)); } }
    public bool HasDetail => Detail.Length > 0;
    public double Percent { get => _percent; private set => Set(ref _percent, value); }

    public string Glyph => State switch
    {
        Mode.Failed => Glyphs.Error,
        Mode.Updated => Glyphs.Check,
        _ => "", // Download
    };

    public string PrimaryText => State switch
    {
        Mode.Available => "Cập nhật ngay",
        Mode.Failed => "Thử lại",
        _ => "",
    };
    public bool PrimaryVisible => PrimaryText.Length > 0;

    public string SecondaryText => State switch
    {
        Mode.Available => "Xem thay đổi",
        Mode.Failed or Mode.Updated => "Đóng",
        _ => "",
    };
    public bool SecondaryVisible => SecondaryText.Length > 0;

    public void ShowAvailable(UpdateInfo info)
    {
        Info = info;
        Title = $"Có bản mới {info.Version}";
        var lines = (info.Notes ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(l => l.TrimStart('-', '*', ' '));
        var notes = string.Join(" · ", lines);
        Detail = notes.Length > 0 ? "Có gì mới: " + notes : "Bấm Cập nhật ngay, phần mềm sẽ tự tải, cài và mở lại.";
        State = Mode.Available;
    }

    public void ShowDownloading(double fraction)
    {
        Title = $"Đang tải bản mới {Info?.Version} · {fraction:0%}";
        Detail = "Tải xong phần mềm sẽ tự đóng, cài bản mới rồi mở lại.";
        Percent = fraction * 100;
        State = Mode.Downloading;
    }

    public void ShowFailed(string message)
    {
        Title = "Chưa cập nhật được";
        Detail = message;
        State = Mode.Failed;
    }

    public void ShowUpdated(string version)
    {
        Title = $"Đã cập nhật lên bản {version}";
        Detail = "";
        State = Mode.Updated;
    }

    public void Hide() => State = Mode.Hidden;
}
