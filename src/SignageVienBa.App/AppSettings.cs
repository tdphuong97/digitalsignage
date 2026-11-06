using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SignageVienBa.Core;

namespace SignageVienBa.App;

/// <summary>
/// Worker address and API key, in %APPDATA%\SignageVienBa\settings.json (or SIGNAGE_DATA).
/// The key is encrypted with Windows DPAPI for the current user, so the file is useless elsewhere.
/// </summary>
public sealed class AppSettings
{
    public const string DefaultUrl = "https://signage.vienba.workers.dev";

    public string WorkerUrl { get; set; } = DefaultUrl;
    public string ApiKeyProtected { get; set; } = "";

    public static string DataFolder =>
        Environment.GetEnvironmentVariable("SIGNAGE_DATA") is { Length: > 0 } custom
            ? custom
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SignageVienBa");

    private static string FilePath => Path.Combine(DataFolder, "settings.json");

    public string ApiKey
    {
        get
        {
            if (string.IsNullOrEmpty(ApiKeyProtected)) return "";
            try
            {
                var bytes = ProtectedData.Unprotect(Convert.FromBase64String(ApiKeyProtected), Entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(bytes);
            }
            catch (Exception e) when (e is CryptographicException or FormatException)
            {
                return "";
            }
        }
        set => ApiKeyProtected = string.IsNullOrEmpty(value)
            ? ""
            : Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(value.Trim()), Entropy, DataProtectionScope.CurrentUser));
    }

    public bool IsComplete => !string.IsNullOrWhiteSpace(WorkerUrl) && ApiKey.Length > 0;

    private static readonly byte[] Entropy = "SignageVienBa.v1"u8.ToArray();

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { }
        return new AppSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(DataFolder);
        var tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(tmp, FilePath, overwrite: true);
    }

    /// <summary>One HttpClient for the whole app; timeouts are handled per call by SignageClient.</summary>
    public static HttpClient Http { get; } = new(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) })
    {
        Timeout = Timeout.InfiniteTimeSpan,
        DefaultRequestHeaders = { { "User-Agent", "SignageVienBa/1.0" } },
    };

    public SignageClient CreateClient() => new(Http, WorkerUrl, ApiKey);
}
