using System.Text.Json.Nodes;

namespace LiteDrops.Mining;

public sealed record ChannelRef(string Id, string Login, string DisplayName, int Viewers);

public sealed class Drop
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required int RequiredMinutes { get; init; }
    public required DateTime StartAt { get; init; }
    public required DateTime EndAt { get; init; }
    public IReadOnlyList<string> BenefitIds { get; init; } = [];
    public IReadOnlyList<string> PreconditionIds { get; init; } = [];
    public int CurrentMinutes { get; set; }
    public bool IsClaimed { get; set; }
    public string? InstanceId { get; set; }

    public bool IsComplete => CurrentMinutes >= RequiredMinutes;
    public bool IsActive(DateTime now) => StartAt <= now && now < EndAt;

    public static Drop Parse(JsonNode node, IReadOnlyDictionary<string, DateTime> claimedBenefits)
    {
        var benefitIds = (node["benefitEdges"] as JsonArray ?? [])
            .Select(e => (string?)e?["benefit"]?["id"])
            .OfType<string>()
            .ToList();
        var drop = new Drop
        {
            Id = (string)node["id"]!,
            Name = (string?)node["name"] ?? "?",
            RequiredMinutes = (int?)node["requiredMinutesWatched"] ?? 0,
            StartAt = ParseTime(node["startAt"]),
            EndAt = ParseTime(node["endAt"]),
            BenefitIds = benefitIds,
            PreconditionIds = (node["preconditionDrops"] as JsonArray ?? [])
                .Select(p => (string?)p?["id"]).OfType<string>().ToList(),
        };
        if (node["self"] is JsonObject self)
        {
            drop.CurrentMinutes = (int?)self["currentMinutesWatched"] ?? 0;
            drop.IsClaimed = (bool?)self["isClaimed"] ?? false;
            drop.InstanceId = (string?)self["dropInstanceID"];
        }
        else if (benefitIds.Count > 0
                 && benefitIds.All(b => claimedBenefits.TryGetValue(b, out var at) && at >= drop.StartAt))
        {
            // not in the inventory, but every reward was already awarded during this drop's window
            drop.IsClaimed = true;
            drop.CurrentMinutes = drop.RequiredMinutes;
        }
        return drop;
    }

    public static DateTime ParseTime(JsonNode? node)
        => node is null ? DateTime.MaxValue : DateTime.Parse((string)node!, null,
            System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal);
}

public sealed class Campaign
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string GameId { get; init; }
    public required string GameName { get; init; }
    public required DateTime EndAt { get; init; }
    public required IReadOnlyList<Drop> Drops { get; init; }
    public bool? AccountLinked { get; init; }
    public bool InInventory { get; init; }
    public List<ChannelRef> Channels { get; } = [];

    /// <summary>The drop that is earned next: unclaimed, active, with its preconditions met.</summary>
    public Drop? NextDrop(DateTime now) => Drops
        .Where(d => !d.IsClaimed && !d.IsComplete && d.IsActive(now)
                    && d.PreconditionIds.All(p => Drops.FirstOrDefault(x => x.Id == p) is not { IsComplete: false }))
        .OrderBy(d => d.RequiredMinutes - d.CurrentMinutes)
        .FirstOrDefault();

    public bool CanEarn(DateTime now) => AccountLinked != false && now < EndAt && NextDrop(now) is not null;

    public double Progress
    {
        get
        {
            var total = Drops.Sum(d => d.RequiredMinutes);
            return total == 0 ? 0 : Drops.Sum(d => Math.Min(d.CurrentMinutes, d.RequiredMinutes)) / (double)total;
        }
    }
}

public enum MinerStatus { Starting, Mining, Idle, LoginRequired, Error }

public sealed record WatchInfo(
    ChannelRef Channel, string GameName, string CampaignName, DateTime CampaignEndAt,
    string DropName, int CurrentMinutes, int RequiredMinutes);

public sealed record CampaignRow(string GameName, string CampaignName, double Progress, bool Earnable);

public sealed record MinerSnapshot(
    MinerStatus Status, string StatusText, WatchInfo? Watching, IReadOnlyList<CampaignRow> Campaigns);
