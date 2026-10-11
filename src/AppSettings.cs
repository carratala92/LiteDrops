using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LiteDrops;

public sealed class GameEntry
{
    public string Name { get; set; } = "";
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Settings stored as litedrops.json next to the exe. The access token is encrypted with
/// Windows DPAPI, so the file is useless on another PC or Windows account.
/// </summary>
public sealed class AppSettings
{
    public List<GameEntry> Games { get; set; } = [];
    public string DeviceId { get; set; } = "";
    public string ProtectedToken { get; set; } = "";
    public bool CloseToTray { get; set; } = true;
    /// <summary>"auto", "en" or "ja".</summary>
    public string Language { get; set; } = "auto";
    /// <summary>Last window position and size as "x,y,width,height".</summary>
    public string? WindowBounds { get; set; }

    [JsonIgnore]
    public string? AccessToken
    {
        get
        {
            if (string.IsNullOrEmpty(ProtectedToken)) return null;
            try
            {
                var data = ProtectedData.Unprotect(
                    Convert.FromBase64String(ProtectedToken), Entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(data);
            }
            catch (CryptographicException) { return null; }
            catch (FormatException) { return null; }
        }
        set
        {
            ProtectedToken = string.IsNullOrEmpty(value)
                ? ""
                : Convert.ToBase64String(ProtectedData.Protect(
                    Encoding.UTF8.GetBytes(value), Entropy, DataProtectionScope.CurrentUser));
        }
    }

    static readonly byte[] Entropy = "LiteDrops.token.v1"u8.ToArray();
    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string FilePath { get; } = Path.Combine(AppContext.BaseDirectory, "litedrops.json");

    public static AppSettings Load()
    {
        AppSettings settings;
        try
        {
            settings = File.Exists(FilePath)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new()
                : new();
        }
        catch (JsonException)
        {
            settings = new();
        }
        if (string.IsNullOrEmpty(settings.DeviceId))
            settings.DeviceId = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        return settings;
    }

    public void Save()
    {
        var tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(this, JsonOptions));
        File.Move(tmp, FilePath, overwrite: true);
    }
}
