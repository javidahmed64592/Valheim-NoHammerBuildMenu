using HarmonyLib;

namespace NoHammerBuildMenu.Patches;

/// <summary>
/// Prevents Shift+RMB / Ctrl+RMB from also opening the build menu while we use them
/// for the repair and grid toggles, with the phantom hammer or any real hammer.
/// </summary>
[HarmonyPatch(typeof(Player), "UpdateBuildGuiInput")]
internal static class Player_UpdateBuildGuiInput_Patch;