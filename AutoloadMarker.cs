using System;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace STS2_RunLibrary;

// The one-shot marker the STS2 Save Sync app writes into the game's user data dir after a successful play-link
// load, just before it launches the game:
//   {"v":1,"run_id":"<16 hex>","steam_id":"<17 digits>","mode":"modded"|"regular","profile":<int>,"created":<unix s>}
// Parsing and the checks that need only the marker and plain local facts live here (pure, unit tested). The
// checks that need the game's save (has a multiplayer save, its run id) live in AutoLoad.cs.
public sealed record AutoloadMarker(int Version, string RunId, string SteamId, string Mode, int Profile, long Created)
{
    public const string FileName = "sts2savesync-autoload.json";
    public const int SupportedVersion = 1;

    private static readonly Regex RunIdRe = new("^[0-9a-f]{16}$", RegexOptions.CultureInvariant);
    private static readonly Regex SteamIdRe = new("^[0-9]{17}$", RegexOptions.CultureInvariant);

    // Returns the marker, or null with a reason. An unsupported "v" is reported before any other field so a
    // future format is never half-read.
    public static AutoloadMarker? Parse(string json, out string error)
    {
        error = "";
        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException) { error = "not valid JSON"; return null; }
        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) { error = "not a JSON object"; return null; }

            if (!TryInt(root, "v", out int v)) { error = "missing or invalid v"; return null; }
            if (v != SupportedVersion) { error = $"unsupported version v={v}"; return null; }

            if (!TryString(root, "run_id", out string runId) || !RunIdRe.IsMatch(runId))
            { error = "missing or invalid run_id"; return null; }
            if (!TryString(root, "steam_id", out string steamId) || !SteamIdRe.IsMatch(steamId))
            { error = "missing or invalid steam_id"; return null; }
            if (!TryString(root, "mode", out string mode) || (mode != "modded" && mode != "regular"))
            { error = "missing or invalid mode"; return null; }
            if (!TryInt(root, "profile", out int profile) || profile < 0)
            { error = "missing or invalid profile"; return null; }
            if (!root.TryGetProperty("created", out var c) || c.ValueKind != JsonValueKind.Number || !c.TryGetInt64(out long created))
            { error = "missing or invalid created"; return null; }

            return new AutoloadMarker(v, runId, steamId, mode, profile, created);
        }
    }

    private static bool TryInt(JsonElement root, string name, out int value)
    {
        value = 0;
        return root.TryGetProperty(name, out var e) && e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out value);
    }

    private static bool TryString(JsonElement root, string name, out string value)
    {
        value = "";
        if (!root.TryGetProperty(name, out var e) || e.ValueKind != JsonValueKind.String) return false;
        value = e.GetString() ?? "";
        return true;
    }
}

// Plain facts about the running game that the marker must agree with.
public readonly record struct LocalGame(string SteamId, bool Modded, int ProfileId);

public static class AutoloadRules
{
    public const long MaxAgeSeconds = 5 * 60;
    public const long MaxFutureSkewSeconds = 60;

    public static string ModeName(bool modded) => modded ? "modded" : "regular";

    // Null when the marker applies to this game; otherwise the reason it does not.
    public static string? Reject(AutoloadMarker m, long nowUnixSeconds, LocalGame local)
    {
        long age = nowUnixSeconds - m.Created;
        if (age > MaxAgeSeconds) return $"stale: created {age}s ago (limit {MaxAgeSeconds}s)";
        if (age < -MaxFutureSkewSeconds) return $"created {-age}s in the future (limit {MaxFutureSkewSeconds}s)";
        if (m.SteamId != local.SteamId) return $"steam_id {m.SteamId} is not the local Steam user {local.SteamId}";
        string mode = ModeName(local.Modded);
        if (m.Mode != mode) return $"mode {m.Mode} does not match the game ({mode})";
        if (m.Profile != local.ProfileId) return $"profile {m.Profile} is not the active profile {local.ProfileId}; not switching profiles";
        return null;
    }
}
