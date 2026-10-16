using System.Text.Json.Nodes;
using LiteDrops.Mining;

namespace LiteDrops.Tests;

public class CampaignTests
{
    static readonly DateTime Now = new(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);
    static readonly Dictionary<string, DateTime> NoClaims = [];

    static Drop MakeDrop(string id, int minutes, int subs = 0, string? type = "DIRECT_ENTITLEMENT",
        int current = 0, string[]? preconditions = null)
    {
        var node = JsonNode.Parse($$"""
            {
              "id": "{{id}}", "name": "Drop {{id}}",
              "requiredMinutesWatched": {{minutes}}, "requiredSubs": {{subs}},
              "startAt": "2026-10-01T00:00:00Z", "endAt": "2026-11-01T00:00:00Z",
              "benefitEdges": [ { "benefit": { "id": "b{{id}}"{{(type is null ? "" : $", \"distributionType\": \"{type}\"")}} } } ],
              "preconditionDrops": [ {{string.Join(",", (preconditions ?? []).Select(p => $"{{\"id\":\"{p}\"}}"))}} ]
            }
            """)!;
        var drop = Drop.Parse(node, NoClaims);
        drop.CurrentMinutes = current;
        return drop;
    }

    static Campaign MakeCampaign(params Drop[] drops) => new()
    {
        Id = "c1", Name = "Campaign", GameId = "1", GameName = "Game",
        EndAt = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), Drops = drops,
    };

    [Fact]
    public void Watch_time_drop_can_be_earned()
    {
        var campaign = MakeCampaign(MakeDrop("a", 60));
        Assert.True(campaign.CanEarn(Now));
        Assert.Equal("a", campaign.NextDrop(Now)!.Id);
    }

    [Fact]
    public void Subscription_drops_are_not_earnable_by_watching()
    {
        // mission-based drops: Twitch also reports these as "not linked" with a help article link
        var campaign = MakeCampaign(MakeDrop("a", 0, subs: 1));
        campaign.AccountLinked = false;
        campaign.LinkUrl = "https://help.twitch.tv/s/article/mission-based-drops#sub";
        Assert.True(campaign.WatchUnearnable);
        Assert.False(campaign.NeedsLink);
        Assert.False(campaign.CanEarn(Now));
    }

    [Fact]
    public void Unlinked_account_blocks_the_campaign()
    {
        var campaign = MakeCampaign(MakeDrop("a", 60));
        campaign.AccountLinked = false;
        campaign.LinkUrl = "https://example.com/link";
        Assert.True(campaign.NeedsLink);
        Assert.False(campaign.CanEarn(Now));
    }

    [Fact]
    public void Unknown_link_status_does_not_block()
    {
        var campaign = MakeCampaign(MakeDrop("a", 60));
        Assert.Null(campaign.AccountLinked);
        Assert.True(campaign.CanEarn(Now));
    }

    [Theory]
    [InlineData("BADGE", "EMOTE", true)]
    [InlineData("BADGE", "DIRECT_ENTITLEMENT", false)]
    [InlineData(null, null, false)] // unknown kinds count as items
    public void Badge_or_emote_only(string? first, string? second, bool expected)
    {
        var campaign = MakeCampaign(MakeDrop("a", 30, type: first), MakeDrop("b", 60, type: second));
        Assert.Equal(expected, campaign.BadgeOrEmoteOnly);
    }

    [Fact]
    public void Next_drop_waits_for_preconditions_and_skips_finished_ones()
    {
        var first = MakeDrop("a", 30, current: 30);
        var second = MakeDrop("b", 60, current: 30, preconditions: ["a"]);
        var campaign = MakeCampaign(first, second);
        Assert.Equal("b", campaign.NextDrop(Now)!.Id);

        var blocked = MakeCampaign(MakeDrop("a", 30), MakeDrop("b", 20, preconditions: ["a"]));
        Assert.Equal("a", blocked.NextDrop(Now)!.Id);
    }

    [Fact]
    public void Progress_is_the_share_of_watched_minutes()
    {
        var campaign = MakeCampaign(MakeDrop("a", 30, current: 30), MakeDrop("b", 90, current: 30));
        Assert.Equal(0.5, campaign.Progress, 3);
    }
}
