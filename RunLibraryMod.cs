using System;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace STS2_RunLibrary;

// Entry point. Run Library is a host-side companion to the STS2 Save Sync app. It never goes online; it only
// reads small local files the app leaves in the game's user data dir.
//
// Features:
//   - Auto-load (AutoLoad.cs): when the app launched the game from a play link, open that run's Load lobby.
[ModInitializer("Initialize")]
public static class RunLibraryMod
{
    private const string HarmonyId = "com.sts2runlibrary";

    public static void Log(string message)
    {
        try { GD.Print($"[RunLibrary] {message}"); } catch { }
    }

    public static void Initialize()
    {
        try
        {
            var ready = AccessTools.Method(typeof(NMainMenu), nameof(NMainMenu._Ready));
            if (ready == null) { Log("NMainMenu._Ready not found; auto-load disabled"); return; }
            var postfix = new HarmonyMethod(typeof(RunLibraryMod).GetMethod(nameof(MainMenuReadyPostfix), BindingFlags.NonPublic | BindingFlags.Static));
            new Harmony(HarmonyId).Patch(ready, postfix: postfix);
            Log("loaded (auto-load ready)");
        }
        catch (Exception e) { Log($"init failed; mod inactive: {e.Message}"); }
    }

    // Never let the mod break the main menu.
    private static void MainMenuReadyPostfix(NMainMenu __instance)
    {
        try { AutoLoad.OnMainMenuReady(__instance); }
        catch (Exception e) { Log($"main menu hook error: {e.Message}"); }
    }
}
