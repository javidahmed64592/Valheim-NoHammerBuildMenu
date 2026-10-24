using HarmonyLib;

namespace NoHammerBuildMenu.Patches;

/// <summary>
/// Moves the aimed placement point so one of the ghost's snap points (or its origin, if it
/// has none) lands on the nearest grid vertex. This runs before vanilla positions the ghost
/// and applies its own piece-to-piece snapping, so snap points of existing pieces within
/// vanilla's snap range still win over the grid, and validity checks see the final position.
/// </summary>
[HarmonyPatch(typeof(Player), "PieceRayTest")]
internal static class Player_PieceRayTest_Patch;