using HarmonyLib;

namespace NoHammerBuildMenu
{
    // Vanilla only zooms the camera while placing if the piece can't rotate (it uses scroll to
    // rotate otherwise). Report "can't rotate" while Alt is held so the camera zooms instead.
    [HarmonyPatch(typeof(Player), nameof(Player.CanRotatePiece))]
    internal static class Player_CanRotatePiece_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(Player __instance, ref bool __result)
        {
            if (__result && NoHammerBuildMenuPlugin.AltZoomActive(__instance))
                __result = false;
        }
    }
}
