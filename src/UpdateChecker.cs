using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Nodes;

namespace LiteDrops;

public sealed record UpdateInfo(string Version, string Url);

/// <summary>Asks GitHub whether a newer release exists. Failures are silent.</summary>
public static class UpdateChecker
{
    const string LatestRelease = "https://api.github.com/repos/naoki6310/LiteDrops/releases/latest";

    public static string CurrentVersion
    {
        get
        {
            var v = typeof(UpdateChecker).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
            var plus = v.IndexOf('+');
            return plus < 0 ? v : v[..plus]; // drop the "+commit" suffix
        }
    }

    /// <summary>True when <paramref name="latest"/> ("1.5.0" or "v1.5.0") is newer than <paramref name="current"/>.</summary>
    internal static bool IsNewer(string latest, string current) =>
        Version.TryParse(latest.TrimStart('v'), out var l) && Version.TryParse(current.TrimStart('v'), out var c) && l > c;

    public static async Task<UpdateInfo?> CheckAsync(CancellationToken ct)
    {
        try
        {
            // short-lived client: nothing stays in memory between the daily checks
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd($"LiteDrops/{CurrentVersion}");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            var json = await http.GetFromJsonAsync<JsonObject>(LatestRelease, ct);
            var tag = ((string?)json?["tag_name"])?.TrimStart('v');
            var url = (string?)json?["html_url"];
            if (tag is null || url is null) return null;
            return IsNewer(tag, CurrentVersion) ? new UpdateInfo(tag, url) : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            return null;
        }
    }
}
