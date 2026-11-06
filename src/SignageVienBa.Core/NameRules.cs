using System.Text;
using System.Text.RegularExpressions;

namespace SignageVienBa.Core;

/// <summary>Turns local file names into R2 keys the Worker and the slideshow page handle safely.</summary>
public static partial class NameRules
{
    // '/' would create folders, '?' and '#' break the slideshow's unescaped src URL,
    // '%' breaks the Worker's decodeURIComponent, the rest are not valid in Windows names.
    private static readonly HashSet<char> Bad = ['/', '\\', '?', '#', '%', ':', '*', '"', '<', '>', '|'];

    private const int MaxStemLength = 120;

    /// <summary>
    /// File name to store on the TV: the original name (NFC, unsafe characters replaced by '-')
    /// with the given extension in lower case.
    /// </summary>
    public static string SafeName(string sourcePath, string extension)
    {
        var stem = Path.GetFileNameWithoutExtension(sourcePath).Normalize(NormalizationForm.FormC);
        var sb = new StringBuilder(stem.Length);
        foreach (var ch in stem)
            sb.Append(char.IsControl(ch) || Bad.Contains(ch) ? '-' : ch);

        var clean = Spaces().Replace(sb.ToString(), " ").Trim().Trim('.').Trim();
        if (clean.Length > MaxStemLength)
        {
            var cut = char.IsHighSurrogate(clean[MaxStemLength - 1]) ? MaxStemLength - 1 : MaxStemLength;
            clean = clean[..cut].TrimEnd();
        }
        if (clean.Length == 0) clean = "noi-dung";
        return clean + extension.ToLowerInvariant();
    }

    /// <summary>"a.mp4" → "a (2).mp4" when "a.mp4" is already taken (case-insensitive).</summary>
    public static string Unique(string name, ICollection<string> taken)
    {
        bool Taken(string n) => taken.Any(t => string.Equals(t, n, StringComparison.OrdinalIgnoreCase));
        if (!Taken(name)) return name;
        var stem = Path.GetFileNameWithoutExtension(name);
        var ext = Path.GetExtension(name);
        for (var i = 2; ; i++)
        {
            var candidate = $"{stem} ({i}){ext}";
            if (!Taken(candidate)) return candidate;
        }
    }

    public static string KeyFor(TvScreen tv, string fileName) => tv.KeyPrefix + fileName;

    /// <summary>What to show the user: the key without the TV's "screenN/" prefix.</summary>
    public static string DisplayName(TvScreen tv, string key) =>
        tv.KeyPrefix.Length > 0 && key.StartsWith(tv.KeyPrefix, StringComparison.Ordinal)
            ? key[tv.KeyPrefix.Length..]
            : key;

    /// <summary>Percent-encodes each path segment, keeping '/' (what the Worker expects).</summary>
    public static string EncodeKey(string key) =>
        string.Join('/', key.Split('/').Select(Uri.EscapeDataString));

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
