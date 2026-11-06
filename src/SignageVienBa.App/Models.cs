using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using SignageVienBa.Core;

namespace SignageVienBa.App;

public abstract class Notifier : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    protected void Raise(params string[] names)
    {
        foreach (var n in names) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}

public static class Glyphs
{
    public const string Video = "";
    public const string Photo = "";
    public const string File = "";
    public const string Check = "";
    public const string Error = "";
}

/// <summary>A file that is on a TV now.</summary>
public sealed class RemoteItem(int number, string key, string displayName)
{
    public int Number { get; } = number;
    public string Key { get; } = key;
    public string DisplayName { get; } = displayName;
    public bool IsVideo { get; } = key.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase);
    public string Glyph => IsVideo ? Glyphs.Video : Glyphs.Photo;
    public string KindText => IsVideo ? "Video" : "Ảnh";
}

public enum QueueState { Waiting, Running, Done, Failed }

/// <summary>A file the user added, on its way to the TV.</summary>
public sealed class QueueItem : Notifier
{
    private QueueState _state;
    private string _status = "";
    private double _percent;
    private bool _replaces;

    public QueueItem(string sourcePath, string targetName)
    {
        SourcePath = sourcePath;
        TargetName = targetName;
        Kind = MediaTypes.Of(sourcePath);
        try { SizeBytes = new FileInfo(sourcePath).Length; } catch (IOException) { }
        if (Kind == MediaKind.Unsupported)
        {
            var ext = Path.GetExtension(sourcePath).ToLowerInvariant();
            Fail(ext.Length == 0 ? "Không hỗ trợ loại file này" : $"Không hỗ trợ file {ext} — chỉ nhận ảnh JPG/PNG và video");
        }
        else
        {
            ResetToWaiting();
        }
    }

    public string SourcePath { get; }
    public string TargetName { get; }
    public MediaKind Kind { get; }
    public long SizeBytes { get; }

    public string Glyph => Kind switch { MediaKind.Video => Glyphs.Video, MediaKind.Image => Glyphs.Photo, _ => Glyphs.File };

    public QueueState State { get => _state; private set { Set(ref _state, value); Raise(nameof(CanRemove), nameof(ShowProgress)); } }
    public string Status { get => _status; private set => Set(ref _status, value); }
    public double Percent { get => _percent; private set => Set(ref _percent, value); }
    public bool CanRemove => State != QueueState.Running;
    public bool ShowProgress => State == QueueState.Running;

    /// <summary>A file with this name is already on the TV and will be replaced.</summary>
    public bool Replaces
    {
        get => _replaces;
        set
        {
            Set(ref _replaces, value);
            if (State == QueueState.Waiting) ResetToWaiting();
        }
    }

    private string SizeText => SizeBytes switch
    {
        >= 1L << 30 => $"{SizeBytes / (double)(1L << 30):0.0} GB",
        >= 1L << 20 => $"{SizeBytes / (double)(1L << 20):0.#} MB",
        _ => $"{Math.Max(1, SizeBytes / 1024)} KB",
    };

    public void ResetToWaiting(string? note = null)
    {
        State = QueueState.Waiting;
        Percent = 0;
        var kind = Kind == MediaKind.Video ? "Video" : "Ảnh";
        var extra = note ?? (Replaces ? "sẽ thay bản cùng tên trên TV" : null);
        Status = $"Chờ · {kind} {SizeText}" + (extra is null ? "" : $" · {extra}");
    }

    public void Report(JobProgress p)
    {
        // Progress<T> posts asynchronously: a late report must not undo Done, Failed or a stop.
        if (State != QueueState.Running) return;
        Percent = p.Fraction * 100;
        Status = p.Stage switch
        {
            JobStage.Checking => "Đang kiểm tra video…",
            JobStage.Compressing => $"Đang nén video · {p.Fraction:0%}",
            _ => $"Đang tải lên · {p.Fraction:0%}",
        };
    }

    public void Start()
    {
        State = QueueState.Running;
        Percent = 0;
        Status = "Đang chuẩn bị…";
    }

    public void Succeed(string tvLabel)
    {
        State = QueueState.Done;
        Percent = 100;
        Status = $"Đã đưa lên {tvLabel}";
    }

    public void Fail(string message)
    {
        State = QueueState.Failed;
        Status = message;
    }
}
