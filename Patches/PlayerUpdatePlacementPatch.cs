using HarmonyLib;

namespace NoHammerBuildMenu.Patches;

/// <summary>
/// Stops Alt+scroll from also rotating the piece: UpdatePlacement accumulates scroll into
/// the rotation, so restore the rotation state it had before the call.
/// </summary>
[HarmonyPatch(typeof(Player), "UpdatePlacement")]
internal static class Player_UpdatePlacement_Patch;