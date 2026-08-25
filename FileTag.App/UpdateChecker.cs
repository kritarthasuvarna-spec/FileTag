using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace FileTag.App;

/// <summary>
/// Quiet background check against GitHub's latest release tag. On a newer
/// version: one tray balloon linking to the download page. Never auto-installs,
/// never nags, never surfaces errors (no repo yet / offline are both fine).
/// </summary>
internal static class UpdateChecker
{
    public const string RepoSlug = "kritarthasuvarna-spec/FileTag";
    public static string ReleasesPage => $"https://github.com/{RepoSlug}/releases/latest";

    public static async Task CheckAsync(TrayIcon tray)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("FileTag", "1.0"));
            string json = await http.GetStringAsync($"https://api.github.com/repos/{RepoSlug}/releases/latest");

            using var doc = JsonDocument.Parse(json);
            string? tag = doc.RootElement.GetProperty("tag_name").GetString();
            if (string.IsNullOrEmpty(tag)) return;

            var latest = Version.Parse(tag.TrimStart('v', 'V'));
            var current = typeof(UpdateChecker).Assembly.GetName().Version ?? new Version(1, 0, 0);
            if (latest > current)
            {
                tray.ShowBalloon("FileTag update available",
                    $"Version {latest.ToString(3)} is out. Click the tray icon menu to download.",
                    openUrlOnClick: ReleasesPage);
            }
        }
        catch { /* offline, rate-limited, repo missing — all silently fine */ }
    }
}
