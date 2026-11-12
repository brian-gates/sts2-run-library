using System;
using System.IO;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Platform;
using MegaCrit.Sts2.Core.Platform.Steam;
using MegaCrit.Sts2.Core.Saves;

namespace STS2_RunLibrary;

// Auto-load into the multiplayer Load lobby when the companion app left a matching marker.
//
// Runs from a postfix on NMainMenu._Ready (profiles and saves are initialized by then: _Ready itself reads
// SaveManager.Instance). The open path is the one the game's own "-fastmp load" uses in
// NMainMenu.CheckCommandLineArgs: OpenMultiplayerSubmenu() then NMultiplayerSubmenu.StartHost(run), where run is
// the result of SaveManager.LoadAndCanonicalizeMultiplayerRunSave(localPlayerId), exactly what clicking
// Multiplayer then Load does (NMultiplayerSubmenu.StartLoad). StartHost starts the Steam host and pushes
// NMultiplayerLoadGameScreen.InitializeAsHost (a LoadRunLobby), with all the game's normal checks and UI.
//
// This feature never writes or modifies saves. The only file it touches is the marker, which it deletes.
internal static class AutoLoad
{
    public static void OnMainMenuReady(NMainMenu menu)
    {
        string path;
        try { path = Path.Combine(OS.GetUserDataDir(), AutoloadMarker.FileName); }
        catch (Exception e) { RunLibraryMod.Log($"autoload: could not resolve user data dir: {e.Message}"); return; }
        if (!File.Exists(path)) return; // the normal case: no play link was used

        // Read, then delete before deciding anything, so a marker never triggers twice (valid or not).
        string json;
        try { json = File.ReadAllText(path); }
        catch (Exception e) { RunLibraryMod.Log($"autoload: marker at {path} unreadable: {e.Message}"); TryDelete(path); return; }
        if (!TryDelete(path)) { RunLibraryMod.Log("autoload: skipped, marker could not be deleted (would re-trigger)"); return; }
        RunLibraryMod.Log($"autoload: marker found and deleted: {path}");

        var marker = AutoloadMarker.Parse(json, out string parseError);
        if (marker is null) { RunLibraryMod.Log($"autoload: skipped, invalid marker: {parseError}"); return; }
        RunLibraryMod.Log($"autoload: marker run_id={marker.RunId} steam_id={marker.SteamId} mode={marker.Mode} profile={marker.Profile} created={marker.Created}");

        try { Decide(menu, marker); }
        catch (Exception e) { RunLibraryMod.Log($"autoload: failed, staying on the main menu: {e}"); }
    }

    private static void Decide(NMainMenu menu, AutoloadMarker marker)
    {
        // Same platform choice as NMultiplayerSubmenu.StartLoad/StartHostAsync.
        if (!SteamInitializer.Initialized || CommandLineHelper.HasArg("fastmp"))
        { RunLibraryMod.Log("autoload: skipped, Steam is not initialized (or -fastmp is set)"); return; }
        ulong localId = PlatformUtil.GetLocalPlayerId(PlatformType.Steam);

        var local = new LocalGame(localId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            UserDataPathProvider.IsRunningModded, SaveManager.Instance.CurrentProfileId);
        string? reject = AutoloadRules.Reject(marker, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), local);
        if (reject != null) { RunLibraryMod.Log($"autoload: skipped, {reject}"); return; }

        if (!SaveManager.Instance.HasMultiplayerRunSave)
        { RunLibraryMod.Log($"autoload: skipped, profile {local.ProfileId} ({local.Mode()}) has no multiplayer run save"); return; }

        var result = SaveManager.Instance.LoadAndCanonicalizeMultiplayerRunSave(localId);
        if (!result.Success || result.SaveData == null)
        { RunLibraryMod.Log($"autoload: skipped, the game could not load the multiplayer save (status={result.Status}); use Multiplayer > Load to see the game's message"); return; }
        var run = result.SaveData;

        string? seed = run.SerializableRng?.Seed;
        if (string.IsNullOrEmpty(seed)) { RunLibraryMod.Log("autoload: skipped, the save has no rng seed"); return; }
        string saveRunId = RunId.Compute(run.StartTime, seed, run.Players.Select(p => p.NetId));
        if (saveRunId != marker.RunId)
        { RunLibraryMod.Log($"autoload: skipped, the multiplayer save is run {saveRunId}, not {marker.RunId}"); return; }

        if (NModalContainer.Instance?.OpenModal != null)
        { RunLibraryMod.Log($"autoload: skipped, a popup is open ({NModalContainer.Instance.OpenModal.GetType().Name}); use Multiplayer > Load"); return; }

        RunLibraryMod.Log($"autoload: run {saveRunId} matches; opening Multiplayer > Load lobby");
        var submenu = menu.OpenMultiplayerSubmenu();
        submenu.StartHost(run);
        RunLibraryMod.Log("autoload: Load lobby requested");
    }

    private static string Mode(this LocalGame g) => AutoloadRules.ModeName(g.Modded);

    private static bool TryDelete(string path)
    {
        try { File.Delete(path); return !File.Exists(path); }
        catch (Exception e) { RunLibraryMod.Log($"autoload: could not delete marker {path}: {e.Message}"); return false; }
    }
}
