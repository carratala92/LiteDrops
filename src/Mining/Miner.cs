using System.Net;
using System.Text.Json.Nodes;
using LiteDrops.Twitch;

namespace LiteDrops.Mining;

/// <summary>
/// The mining loop: find campaigns for the selected games, pick a live channel that progresses
/// one of them, "watch" it by fetching the stream playlist, and claim finished drops.
/// </summary>
public sealed class Miner(TwitchApi api, AppSettings settings, TwitchUser user)
{
    static readonly TimeSpan WatchInterval = TimeSpan.FromSeconds(20);
    static readonly TimeSpan ProgressCheckInterval = TimeSpan.FromMinutes(1);
    static readonly TimeSpan NoProgressTimeout = TimeSpan.FromMinutes(7);
    static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(30);
    static readonly TimeSpan IdleRetry = TimeSpan.FromMinutes(5);
    static readonly TimeSpan BadChannelCooldown = TimeSpan.FromMinutes(30);
    const int StreamsPerGame = 30;

    readonly Dictionary<string, (string Id, string Slug)?> _gameCache = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, DateTime> _badChannels = [];
    List<Campaign> _campaigns = [];
    CancellationTokenSource _wake = new();
    MinerSnapshot _snapshot = new(MinerStatus.Starting, "Starting…", null, []);

    public event Action<MinerSnapshot>? Changed;
    public event Action<string, string>? Claimed; // game, drop
    public MinerSnapshot Snapshot => _snapshot;

    /// <summary>Interrupts waiting/watching so the game selection is re-read right away.</summary>
    public void Wake()
    {
        var old = Interlocked.Exchange(ref _wake, new CancellationTokenSource());
        old.Cancel();
        old.Dispose();
    }

    public async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                Publish(MinerStatus.Starting, L.LookingForCampaigns, null);
                await RefreshAsync(ct);
                var target = PickTarget();
                if (target is null)
                {
                    var anyGame = settings.Games.Any(g => g.Enabled);
                    Publish(MinerStatus.Idle,
                        anyGame ? L.NoLiveDrops : L.SelectGame, null);
                    await WaitAsync(IdleRetry, ct);
                    continue;
                }
                await WatchAsync(target.Value.Campaign, target.Value.Channel, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (AuthExpiredException)
            {
                Log.Warn("Login expired");
                Publish(MinerStatus.LoginRequired, L.LoginExpired, null);
                throw;
            }
            catch (Exception ex) when (ex is TwitchApiException or HttpRequestException or TaskCanceledException)
            {
                Log.Error(ex.Message);
                Publish(MinerStatus.Error, L.ErrorRetry(ex.Message), null);
                await WaitAsync(TimeSpan.FromMinutes(1), ct);
            }
        }
    }

    // ------------------------------------------------------------ campaigns

    async Task RefreshAsync(CancellationToken ct)
    {
        var wanted = settings.Games.Where(g => g.Enabled).Select(g => g.Name).ToList();
        // match campaigns by game ID; names differ between endpoints ("Tom Clancy's ...")
        var wantedGames = new List<(string Name, string Id, string Slug)>();
        foreach (var name in wanted)
        {
            if (await ResolveGameAsync(name, ct) is { } g) wantedGames.Add((name, g.Id, g.Slug));
        }
        var nameById = wantedGames.GroupBy(g => g.Id).ToDictionary(g => g.Key, g => g.First().Name);

        var inventory = await api.GqlAsync(Gql.Inventory(), ct);
        var inv = inventory["data"]?["currentUser"]?["inventory"];
        var claimedBenefits = new Dictionary<string, DateTime>();
        foreach (var b in inv?["gameEventDrops"] as JsonArray ?? [])
        {
            if ((string?)b?["id"] is { } id)
                claimedBenefits[id] = Drop.ParseTime(b!["lastAwardedAt"]);
        }

        var campaigns = new Dictionary<string, Campaign>();
        foreach (var c in inv?["dropCampaignsInProgress"] as JsonArray ?? [])
        {
            if (c?["game"] is null) continue;
            var campaign = ParseCampaign(c, claimedBenefits, inInventory: true,
                nameById.GetValueOrDefault((string?)c["game"]!["id"] ?? ""));
            campaigns[campaign.Id] = campaign;
        }
        await ClaimFinishedAsync(campaigns.Values, ct);

        // discover campaigns through live, drops-enabled channels of the selected games
        foreach (var (gameName, gameId, slug) in wantedGames)
        {
            var streams = await api.GqlAsync(Gql.DropsStreams(slug, StreamsPerGame), ct);
            var channels = (streams["data"]?["game"]?["streams"]?["edges"] as JsonArray ?? [])
                .Select(e => e?["node"])
                .Where(n => n?["broadcaster"] is JsonObject)
                .Select(n => new ChannelRef(
                    (string)n!["broadcaster"]!["id"]!,
                    (string)n["broadcaster"]!["login"]!,
                    (string?)n["broadcaster"]!["displayName"] ?? (string)n["broadcaster"]!["login"]!,
                    (int?)n["viewersCount"] ?? 0))
                .ToList();
            foreach (var batch in channels.Chunk(20))
            {
                var results = await api.GqlBatchAsync(batch.Select(ch => Gql.AvailableDrops(ch.Id)).ToList(), ct);
                for (var i = 0; i < batch.Length; i++)
                {
                    foreach (var c in results[i]["data"]?["channel"]?["viewerDropCampaigns"] as JsonArray ?? [])
                    {
                        if ((string?)c?["game"]?["id"] != gameId) continue;
                        if (c!["timeBasedDrops"] is not JsonArray { Count: > 0 }) continue;
                        var id = (string)c["id"]!;
                        if (!campaigns.TryGetValue(id, out var campaign))
                        {
                            campaign = ParseCampaign(c, claimedBenefits, inInventory: false, gameName);
                            campaigns[id] = campaign;
                        }
                        if (!campaign.Channels.Any(x => x.Id == batch[i].Id))
                            campaign.Channels.Add(batch[i]);
                    }
                }
            }
        }

        await FillRewardTypesAsync(campaigns.Values.Where(c => nameById.ContainsKey(c.GameId)), ct);

        var now = DateTime.UtcNow;
        _campaigns = campaigns.Values
            .Where(c => nameById.ContainsKey(c.GameId) && c.EndAt > now)
            .OrderBy(c => wantedGames.FindIndex(g => g.Id == c.GameId))
            .ThenBy(c => c.BadgeOrEmoteOnly) // in-game items first, chat badges / emotes last
            .ThenBy(c => c.EndAt)
            .ToList();
        Log.Info($"Found {_campaigns.Count} campaign(s), {_campaigns.Count(c => Wanted(c, now))} earnable, " +
                 $"{_campaigns.Count(c => c.BadgeOrEmoteOnly)} badge/emote only");
    }

    bool Wanted(Campaign c, DateTime now) => c.CanEarn(now) && (settings.MineBadges || !c.BadgeOrEmoteOnly);

    /// <summary>
    /// AvailableDrops doesn't say whether a reward is an in-game item or a chat badge / emote.
    /// Ask one live channel per campaign; on failure the types stay unknown (treated as items).
    /// </summary>
    async Task FillRewardTypesAsync(IEnumerable<Campaign> campaigns, CancellationToken ct)
    {
        var missing = campaigns.Where(c => c.Channels.Count > 0 && c.Drops.All(d => d.RewardTypes.Count == 0)).ToList();
        if (missing.Count == 0) return;
        var channelIds = missing.Select(c => c.Channels[0].Id).Distinct().ToList();
        var dropsById = missing.SelectMany(c => c.Drops).GroupBy(d => d.Id).ToDictionary(g => g.Key, g => g.First());
        try
        {
            foreach (var batch in channelIds.Chunk(20))
            {
                var results = await api.GqlBatchAsync(batch.Select(Gql.RewardTypes).ToList(), ct);
                foreach (var result in results)
                {
                    foreach (var c in result["data"]?["channel"]?["viewerDropCampaigns"] as JsonArray ?? [])
                    foreach (var d in c?["timeBasedDrops"] as JsonArray ?? [])
                    {
                        if ((string?)d?["id"] is not { } dropId || !dropsById.TryGetValue(dropId, out var drop)) continue;
                        drop.RewardTypes.Clear();
                        drop.RewardTypes.AddRange((d!["benefitEdges"] as JsonArray ?? [])
                            .Select(e => (string?)e?["benefit"]?["distributionType"])
                            .OfType<string>());
                    }
                }
            }
        }
        catch (TwitchApiException ex)
        {
            Log.Warn($"Reward types unavailable: {ex.Message}");
        }
    }

    Campaign ParseCampaign(JsonNode c, IReadOnlyDictionary<string, DateTime> claimed, bool inInventory, string? gameName = null)
    {
        var drops = (c["timeBasedDrops"] as JsonArray ?? [])
            .Where(d => d is not null)
            .Select(d => Drop.Parse(d!, claimed))
            .ToList();
        var game = c["game"]!;
        bool? linked = (bool?)c["self"]?["isAccountConnected"];
        if (linked is null)
        {
            // discovered campaigns don't say; reuse what an inventory campaign of the game says
            linked = _campaigns.FirstOrDefault(x => x.GameId == (string?)game["id"] && x.AccountLinked is not null)
                ?.AccountLinked;
        }
        return new Campaign
        {
            Id = (string)c["id"]!,
            Name = ((string?)c["name"])?.Trim() ?? "?",
            GameId = (string?)game["id"] ?? "",
            GameName = gameName ?? (string?)game["displayName"] ?? (string?)game["name"] ?? "?",
            EndAt = Drop.ParseTime(c["endAt"]),
            Drops = drops,
            AccountLinked = linked,
            InInventory = inInventory,
        };
    }

    async Task<(string Id, string Slug)?> ResolveGameAsync(string name, CancellationToken ct)
    {
        if (_gameCache.TryGetValue(name, out var cached)) return cached;
        var result = await api.GqlAsync(Gql.GameSlug(name), ct);
        var game = result["data"]?["game"];
        (string, string)? value = game is null ? null : ((string)game["id"]!, (string)game["slug"]!);
        if (value is null) Log.Warn($"Unknown game on Twitch: {name}");
        _gameCache[name] = value;
        return value;
    }

    async Task ClaimFinishedAsync(IEnumerable<Campaign> campaigns, CancellationToken ct)
    {
        foreach (var campaign in campaigns)
        {
            foreach (var drop in campaign.Drops.Where(d => d.IsComplete && !d.IsClaimed && d.IsActive(DateTime.UtcNow)))
            {
                var instance = drop.InstanceId ?? $"{user.Id}#{campaign.Id}#{drop.Id}";
                var result = await api.GqlAsync(Gql.ClaimDrop(instance), ct);
                var status = (string?)result["data"]?["claimDropRewards"]?["status"];
                if (status is "ELIGIBLE_FOR_ALL" or "DROP_INSTANCE_ALREADY_CLAIMED")
                {
                    drop.IsClaimed = true;
                    Log.Info($"Claimed: {campaign.GameName} / {drop.Name}");
                    Claimed?.Invoke(campaign.GameName, drop.Name);
                }
                else
                {
                    var error = (string?)result["errors"]?[0]?["message"] ?? status ?? "no status";
                    Log.Warn($"Claim failed for {drop.Name}: {error}");
                }
            }
        }
    }

    (Campaign Campaign, ChannelRef Channel)? PickTarget()
    {
        var now = DateTime.UtcNow;
        foreach (var key in _badChannels.Where(kv => kv.Value < now).Select(kv => kv.Key).ToList())
            _badChannels.Remove(key);
        foreach (var campaign in _campaigns.Where(c => Wanted(c, now)))
        {
            var channel = campaign.Channels.FirstOrDefault(ch => !_badChannels.ContainsKey(ch.Id));
            if (channel is not null) return (campaign, channel);
        }
        return null;
    }

    // ------------------------------------------------------------ watching

    async Task WatchAsync(Campaign campaign, ChannelRef channel, CancellationToken ct)
    {
        var drop = campaign.NextDrop(DateTime.UtcNow)!;
        Log.Info($"Watching {channel.Login} for {campaign.GameName} / {campaign.Name} / {drop.Name}");
        PublishWatching(campaign, channel, drop);

        var watcher = new StreamWatcher(api, channel.Login);
        var started = DateTime.UtcNow;
        var lastProgressAt = started;
        var nextCheck = started + ProgressCheckInterval;
        var lastMinutes = drop.CurrentMinutes;
        var failures = 0;

        while (true)
        {
            if (await watcher.TickAsync(ct)) failures = 0;
            else if (++failures >= 3)
            {
                Log.Info($"{channel.Login} went offline or stopped serving the stream");
                MarkBad(channel);
                return;
            }

            var now = DateTime.UtcNow;
            if (now >= nextCheck)
            {
                nextCheck = now + ProgressCheckInterval;
                var result = await api.GqlAsync(Gql.CurrentDrop(channel.Id), ct);
                var session = result["data"]?["currentUser"]?["dropCurrentSession"];
                var dropId = (string?)session?["dropID"];
                var minutes = (int?)session?["currentMinutesWatched"] ?? 0;
                var current = string.IsNullOrEmpty(dropId) ? null : campaign.Drops.FirstOrDefault(d => d.Id == dropId);
                if (current is not null)
                {
                    // minutes count for every drop of the campaign at once
                    var delta = minutes - current.CurrentMinutes;
                    if (delta > 0)
                    {
                        foreach (var d in campaign.Drops.Where(d => !d.IsClaimed))
                            d.CurrentMinutes = Math.Min(d.RequiredMinutes, d.CurrentMinutes + delta);
                        current.CurrentMinutes = minutes;
                    }
                    drop = current;
                    PublishWatching(campaign, channel, drop);
                    if (minutes > lastMinutes)
                    {
                        lastMinutes = minutes;
                        lastProgressAt = now;
                    }
                    if (current.IsComplete)
                    {
                        Log.Info($"Drop finished: {current.Name}");
                        return; // the refresh claims it and picks the next target
                    }
                }
                if (now - lastProgressAt > NoProgressTimeout)
                {
                    Log.Warn($"No progress on {channel.Login} for {NoProgressTimeout.TotalMinutes:0} min, switching");
                    MarkBad(channel);
                    return;
                }
            }
            if (now - started > RefreshInterval) return;
            if (!await WaitAsync(WatchInterval, ct)) return; // woken up: selection changed
        }
    }

    void MarkBad(ChannelRef channel) => _badChannels[channel.Id] = DateTime.UtcNow + BadChannelCooldown;

    /// <summary>Waits for the delay; returns false if <see cref="Wake"/> interrupted it.</summary>
    async Task<bool> WaitAsync(TimeSpan delay, CancellationToken ct)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _wake.Token);
        try
        {
            await Task.Delay(delay, linked.Token);
            return true;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return false;
        }
    }

    // ------------------------------------------------------------ UI snapshot

    void PublishWatching(Campaign campaign, ChannelRef channel, Drop drop) => Publish(
        MinerStatus.Mining, L.Mining,
        new WatchInfo(channel, campaign.GameName, campaign.Name, campaign.EndAt, drop.Name,
            drop.CurrentMinutes, drop.RequiredMinutes));

    void Publish(MinerStatus status, string text, WatchInfo? watching)
    {
        var now = DateTime.UtcNow;
        var rows = _campaigns
            .Select(c => new CampaignRow(c.GameName, c.Name, c.Progress, Wanted(c, now)))
            .ToList();
        _snapshot = new MinerSnapshot(status, text, watching, rows);
        Changed?.Invoke(_snapshot);
    }
}

/// <summary>
/// Simulates a viewer by periodically fetching the stream's lowest quality playlist and
/// sending HEAD requests for its new segments, without downloading any video data.
/// </summary>
sealed class StreamWatcher(TwitchApi api, string login)
{
    Uri? _playlist;
    readonly Queue<string> _seen = new();

    public async Task<bool> TickAsync(CancellationToken ct)
    {
        try
        {
            _playlist ??= await GetPlaylistAsync(ct);
            if (_playlist is null) return false;

            using var req = new HttpRequestMessage(HttpMethod.Get, _playlist);
            req.Headers.ConnectionClose = true;
            using var resp = await api.Http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
            {
                _playlist = null;
                return false;
            }
            var segments = (await resp.Content.ReadAsStringAsync(ct))
                .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Where(l => l.StartsWith("http", StringComparison.Ordinal))
                .ToList();
            if (segments.Count == 0)
            {
                _playlist = null;
                return false;
            }
            foreach (var segment in segments.Where(s => !_seen.Contains(s)))
            {
                using var head = new HttpRequestMessage(HttpMethod.Head, segment);
                using var headResp = await api.Http.SendAsync(head, ct);
                if (headResp.StatusCode != HttpStatusCode.OK) return false;
                _seen.Enqueue(segment);
                if (_seen.Count > 30) _seen.Dequeue();
            }
            return true;
        }
        catch (HttpRequestException)
        {
            _playlist = null;
            return false;
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            _playlist = null;
            return false;
        }
    }

    async Task<Uri?> GetPlaylistAsync(CancellationToken ct)
    {
        var result = await api.GqlAsync(Gql.PlaybackAccessToken(login), ct);
        var token = result["data"]?["streamPlaybackAccessToken"];
        if (token is null) return null;
        var url = $"https://usher.ttvnw.net/api/channel/hls/{Uri.EscapeDataString(login)}.m3u8" +
                  $"?sig={Uri.EscapeDataString((string)token["signature"]!)}" +
                  $"&token={Uri.EscapeDataString((string)token["value"]!)}" +
                  $"&allow_source=true&p={Random.Shared.Next(1_000_000, 9_999_999)}";
        using var resp = await api.Http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return null;
        var lines = (await resp.Content.ReadAsStringAsync(ct))
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(l => l.StartsWith("http", StringComparison.Ordinal))
            .ToList();
        // variants are listed best first; the last one is the lightest
        return lines.Count == 0 ? null : new Uri(lines[^1]);
    }
}
