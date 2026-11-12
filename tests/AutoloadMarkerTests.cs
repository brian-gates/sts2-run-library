using STS2_RunLibrary;
using Xunit;

namespace STS2_RunLibrary.Tests;

public class AutoloadMarkerTests
{
    private const string Steam = "76561198000000001";
    private const long Now = 1_800_000_000;
    private static readonly LocalGame Local = new(Steam, Modded: true, ProfileId: 1);

    private static string Json(string v = "1", string runId = "\"ba18b6e0404293b2\"", string steamId = "\"" + Steam + "\"",
        string mode = "\"modded\"", string profile = "1", string created = "1800000000") =>
        $"{{\"v\":{v},\"run_id\":{runId},\"steam_id\":{steamId},\"mode\":{mode},\"profile\":{profile},\"created\":{created}}}";

    private static AutoloadMarker Valid(long created = Now, string mode = "modded", int profile = 1, string steamId = Steam) =>
        new(1, "ba18b6e0404293b2", steamId, mode, profile, created);

    // --- parsing ---

    [Fact]
    public void ParsesAValidMarker()
    {
        var m = AutoloadMarker.Parse(Json(), out string err);
        Assert.NotNull(m);
        Assert.Equal("", err);
        Assert.Equal(new AutoloadMarker(1, "ba18b6e0404293b2", Steam, "modded", 1, 1800000000), m);
    }

    [Fact]
    public void AcceptsRegularModeAndExtraFields()
    {
        string json = Json(mode: "\"regular\"").TrimEnd('}') + ",\"future\":true}";
        Assert.Equal("regular", AutoloadMarker.Parse(json, out _)!.Mode);
    }

    [Theory]
    [InlineData("", "not valid JSON")]
    [InlineData("{", "not valid JSON")]
    [InlineData("[]", "not a JSON object")]
    [InlineData("{}", "missing or invalid v")]
    public void RejectsMalformedDocuments(string json, string expected)
    {
        Assert.Null(AutoloadMarker.Parse(json, out string err));
        Assert.Equal(expected, err);
    }

    [Fact]
    public void RejectsOtherVersionsBeforeReadingOtherFields()
    {
        Assert.Null(AutoloadMarker.Parse("{\"v\":2}", out string err));
        Assert.Equal("unsupported version v=2", err);
        Assert.Null(AutoloadMarker.Parse(Json(v: "\"1\""), out err));
        Assert.Equal("missing or invalid v", err);
    }

    [Theory]
    [InlineData("\"90672A163EE80092\"")] // uppercase
    [InlineData("\"90672a163ee8009\"")]  // 15 chars
    [InlineData("\"ba18b6e0404293b2a\"")]
    [InlineData("\"90672a163ee8009z\"")]
    [InlineData("1234567890123456")]     // number, not string
    [InlineData("null")]
    public void RejectsBadRunId(string runId)
    {
        Assert.Null(AutoloadMarker.Parse(Json(runId: runId), out string err));
        Assert.Equal("missing or invalid run_id", err);
    }

    [Theory]
    [InlineData("\"7656119801071387\"")]  // 16 digits
    [InlineData("\"765611980000000010\"")]
    [InlineData("\"7656119801071387x\"")]
    [InlineData("76561198000000001")]     // number: loses precision in JS, must be a string
    public void RejectsBadSteamId(string steamId)
    {
        Assert.Null(AutoloadMarker.Parse(Json(steamId: steamId), out string err));
        Assert.Equal("missing or invalid steam_id", err);
    }

    [Theory]
    [InlineData("\"Modded\"")]
    [InlineData("\"\"")]
    [InlineData("1")]
    public void RejectsBadMode(string mode)
    {
        Assert.Null(AutoloadMarker.Parse(Json(mode: mode), out string err));
        Assert.Equal("missing or invalid mode", err);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("\"1\"")]
    public void RejectsBadProfile(string profile)
    {
        Assert.Null(AutoloadMarker.Parse(Json(profile: profile), out string err));
        Assert.Equal("missing or invalid profile", err);
    }

    [Theory]
    [InlineData("1800000000.5")]
    [InlineData("\"1800000000\"")]
    [InlineData("null")]
    public void RejectsBadCreated(string created)
    {
        Assert.Null(AutoloadMarker.Parse(Json(created: created), out string err));
        Assert.Equal("missing or invalid created", err);
    }

    // --- rules ---

    [Fact]
    public void AcceptsAFreshMatchingMarker() => Assert.Null(AutoloadRules.Reject(Valid(), Now, Local));

    [Theory]
    [InlineData(0)]
    [InlineData(300)]   // exactly 5 minutes old
    [InlineData(-60)]   // a minute in the future (clock skew)
    public void AcceptsWithinTheFreshnessWindow(long ageSeconds) =>
        Assert.Null(AutoloadRules.Reject(Valid(created: Now - ageSeconds), Now, Local));

    [Fact]
    public void RejectsStaleMarkers() =>
        Assert.StartsWith("stale", AutoloadRules.Reject(Valid(created: Now - 301), Now, Local));

    [Fact]
    public void RejectsMarkersTooFarInTheFuture() =>
        Assert.Contains("in the future", AutoloadRules.Reject(Valid(created: Now + 61), Now, Local));

    [Fact]
    public void RejectsAnotherSteamUser() =>
        Assert.Contains("not the local Steam user", AutoloadRules.Reject(Valid(steamId: "76561198000000002"), Now, Local));

    [Fact]
    public void RejectsModeMismatch()
    {
        Assert.Contains("does not match", AutoloadRules.Reject(Valid(mode: "regular"), Now, Local));
        Assert.Contains("does not match", AutoloadRules.Reject(Valid(mode: "modded"), Now, Local with { Modded = false }));
        Assert.Null(AutoloadRules.Reject(Valid(mode: "regular"), Now, Local with { Modded = false }));
    }

    [Fact]
    public void RejectsAnotherProfileWithoutSwitching() =>
        Assert.Contains("not switching profiles", AutoloadRules.Reject(Valid(profile: 2), Now, Local));
}
