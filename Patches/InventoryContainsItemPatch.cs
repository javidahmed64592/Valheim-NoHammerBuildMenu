using HarmonyLib;

namespace NoHammerBuildMenu.Patches;

/// <summary>
/// Makes EquipItem accept the phantom hammer despite it not being in the player's inventory.
/// </summary>
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
