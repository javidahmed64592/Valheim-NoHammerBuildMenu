using HarmonyLib;
using System.Reflection;

namespace NoHammerBuildMenu
{
    // Ctrl+scroll adjusts the grid, so stop vanilla from also using it to rotate the piece.
    [HarmonyPatch]
    internal static class ZInput_GetMouseScrollWheel_Patch
    {
        private static MethodBase TargetMethod() =>
            AccessTools.Method(typeof(ZInput), "GetMouseScrollWheel");

        private static bool Prepare() => TargetMethod() != null;

        [HarmonyPostfix]
        private static void Postfix(ref float __result)
        {
            Player player = Player.m_localPlayer;
            if (player != null && NoHammerBuildMenuPlugin.CtrlHeld() && NoHammerBuildMenuPlugin.GridActive(player))
                __result = 0f;
        }
    }
}
