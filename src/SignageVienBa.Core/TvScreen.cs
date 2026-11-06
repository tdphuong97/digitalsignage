namespace SignageVienBa.Core;

/// <summary>
/// One TV. The Worker keeps TV 1's files at the bucket root (backwards compatible with the
/// first version) and every other TV's files under "screenN/".
/// </summary>
public sealed record TvScreen(int Number)
{
    public string Label => $"TV {Number}";

    public string KeyPrefix => Number == 1 ? "" : $"screen{Number}/";

    public static IReadOnlyList<TvScreen> All { get; } = [new(1), new(2)];
}
