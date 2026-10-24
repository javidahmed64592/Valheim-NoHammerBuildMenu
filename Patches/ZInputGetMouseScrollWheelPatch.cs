using HarmonyLib;

namespace NoHammerBuildMenu.Patches;

/// <summary>
/// Ctrl+scroll adjusts the grid, so stop vanilla from also using it to rotate the piece.
/// </summary>
[HarmonyPatch]
internal static class ZInput_GetMouseScrollWheel_Patch;