using HarmonyLib;

namespace NoHammerBuildMenu
{
    // Makes EquipItem accept the phantom hammer despite it not being in the player's inventory.
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.ContainsItem))]
    internal static class Inventory_ContainsItem_Patch
    {
        [HarmonyPrefix]
        private static bool Prefix(ItemDrop.ItemData item, ref bool __result)
        {
            ItemDrop.ItemData phantom = NoHammerBuildMenuPlugin._phantomHammer;
            if (phantom != null && item == phantom)
            {
                __result = true;
                return false;
            }
            return true;
        }
    }
}
