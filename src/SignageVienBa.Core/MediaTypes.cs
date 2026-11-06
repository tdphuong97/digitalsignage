namespace SignageVienBa.Core;

public enum MediaKind { Unsupported, Image, Video }

public static class MediaTypes
{
    // The slideshow shows anything that is not .mp4 as an <img>; the Worker knows these types.
    private static readonly HashSet<string> Images = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png" };

    private static readonly HashSet<string> Videos = new(StringComparer.OrdinalIgnoreCase)
        { ".mp4", ".mov", ".m4v", ".avi", ".mkv", ".wmv", ".webm", ".mpg", ".mpeg", ".3gp", ".flv", ".ts", ".mts", ".m2ts" };

    public static MediaKind Of(string path)
    {
        var ext = Path.GetExtension(path);
        if (Images.Contains(ext)) return MediaKind.Image;
        if (Videos.Contains(ext)) return MediaKind.Video;
        return MediaKind.Unsupported;
    }

    /// <summary>
    /// Expands dropped or picked paths: files are kept as given (unsupported ones too, so the user
    /// sees why they were not sent); folders contribute their supported files, recursively,
    /// skipping hidden and system files.
    /// </summary>
    public static IEnumerable<string> Expand(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (File.Exists(path))
            {
                yield return Path.GetFullPath(path);
            }
            else if (Directory.Exists(path))
            {
                var options = new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true,
                    AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
                };
                var files = Directory.EnumerateFiles(path, "*", options)
                    .Where(f => Of(f) != MediaKind.Unsupported)
                    .Order(StringComparer.CurrentCultureIgnoreCase);
                foreach (var f in files) yield return Path.GetFullPath(f);
            }
        }
    }
}
