using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LiteDrops;

public sealed record ClaimRecord(string Game, string Campaign, string Drop, DateTime ClaimedAt);

/// <summary>Claimed drops, newest first, kept in litedrops-history.json next to the exe.</summary>
public sealed partial class ClaimHistory
{
    const int MaxEntries = 1000;
    static readonly string FilePath = Path.Combine(AppContext.BaseDirectory, "litedrops-history.json");
    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    readonly List<ClaimRecord> _records;

    ClaimHistory(List<ClaimRecord> records) => _records = records;

    public IReadOnlyList<ClaimRecord> Records => _records;

    public static ClaimHistory Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return new ClaimHistory(JsonSerializer.Deserialize<List<ClaimRecord>>(File.ReadAllText(FilePath)) ?? []);
        }
        catch (JsonException) { }
        catch (IOException) { }
        // first run of this version: pick up the claims already written to the log
        var history = new ClaimHistory(ImportFromLog());
        if (history._records.Count > 0) history.Save();
        return history;
    }

    public void Add(string game, string campaign, string drop)
    {
        _records.Insert(0, new ClaimRecord(game, campaign, drop, DateTime.UtcNow));
        if (_records.Count > MaxEntries) _records.RemoveRange(MaxEntries, _records.Count - MaxEntries);
        Save();
    }

    void Save()
    {
        try
        {
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(_records, JsonOptions));
            File.Move(tmp, FilePath, overwrite: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    [GeneratedRegex(@"^(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}) INFO\s+Claimed: (.+?) / (.+)$")]
    private static partial Regex ClaimLine();

    /// <summary>Reads a "Claimed: game / drop" line written by <see cref="Log"/>, null for other lines.</summary>
    internal static ClaimRecord? ParseLogLine(string line)
    {
        var m = ClaimLine().Match(line.TrimEnd());
        if (!m.Success) return null;
        var local = DateTime.ParseExact(m.Groups[1].Value, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        return new ClaimRecord(m.Groups[2].Value, "", m.Groups[3].Value.Trim(),
            DateTime.SpecifyKind(local, DateTimeKind.Local).ToUniversalTime());
    }

    static List<ClaimRecord> ImportFromLog()
    {
        var records = new List<ClaimRecord>();
        foreach (var name in new[] { "litedrops.log.old", "litedrops.log" })
        {
            var path = Path.Combine(AppContext.BaseDirectory, name);
            if (!File.Exists(path)) continue;
            try
            {
                foreach (var line in File.ReadLines(path))
                {
                    if (ParseLogLine(line) is { } record) records.Add(record);
                }
            }
            catch (IOException) { }
        }
        records.Reverse();
        return records;
    }
}
