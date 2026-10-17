using HarmonyLib;

namespace NoHammerBuildMenu
{
    // Prevents Shift+RMB / Ctrl+RMB from also opening the build menu while we use them
    // for the repair and grid toggles, with the phantom hammer or any real hammer.
    [HarmonyPatch(typeof(Player), "UpdateBuildGuiInput")]
    internal static class Player_UpdateBuildGuiInput_Patch
    {
        [HarmonyPrefix]
        private static bool Prefix(Player __instance)
        {
            if (__instance == Player.m_localPlayer
                && (NoHammerBuildMenuPlugin.ShiftHeld() || NoHammerBuildMenuPlugin.CtrlHeld())
                && NoHammerBuildMenuPlugin.IsHammerEquipped(__instance))
                return false;
            return true;
        }
    }
}
