using HarmonyLib;

namespace NoHammerBuildMenu
{
    // SetupVisEquipment in Valheim l-1.0.x crashes with a NullReferenceException when
    // equipping items that didn't come through the normal inventory flow. Work around it
    // by hiding the phantom from the method and setting the visual directly afterward.
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.SetupVisEquipment))]
    internal static class Humanoid_SetupVisEquipment_Patch
    {
        [HarmonyPrefix]
        private static void Prefix(Humanoid __instance, out ItemDrop.ItemData __state)
        {
            ItemDrop.ItemData phantom = NoHammerBuildMenuPlugin._phantomHammer;
            __state = null;
            if (phantom != null && __instance.GetRightItem() == phantom)
            {
                __state = phantom;
                __instance.m_rightItem = null;
            }
        }

        [HarmonyPostfix]
        private static void Postfix(Humanoid __instance, ItemDrop.ItemData __state)
        {
            if (__state == null) return;
            __instance.m_rightItem = __state;
            if (__instance is Player player)
                player.m_visEquipment?.SetRightItem(
                    __state.m_dropPrefab.name.GetStableHashCode(),
                    __state.m_quality
                );
        }
    }
}
