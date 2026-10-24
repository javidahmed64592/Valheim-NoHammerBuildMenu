using System;

using BepInEx;
using BepInEx.Configuration;

using UnityEngine;

namespace NoHammerBuildMenu;

[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
[BepInDependency(Jotunn.Main.ModGuid)]
public class NoHammerBuildMenuPlugin : BaseUnityPlugin
{
    public const string PluginGUID = "javidahmed64592.nohammerbuildmenu";
    public const string PluginName = "No Hammer Build Menu";
    public const string PluginVersion = "0.10.1";

    internal static ItemDrop.ItemData _phantomHammer;

    private static readonly ConfigEntry<bool> _gridEnabled;
    internal static bool GridEnabled => _gridEnabled.Value;
    internal static ConfigEntry<float> _gridSize;

    /// <summary>
    /// Latest grid-snapped aim point from Player.PieceRayTest, used to place the grid overlay.
    /// </summary>
    internal static int _aimFrame = -10;
    internal static Vector3 _aimPoint;
    internal static GridPlane _aimPlane;
    internal static float _aimSign;

    internal static bool GridActive(Player player)
    {
        return _gridEnabled.Value
            && player.InPlaceMode()
            && !player.InRepairMode()
            && player.m_placementGhost != null
            && IsHammerEquipped(player);
    }

    /// <summary>
    /// The grid is for hammers only (not the hoe, cultivator, etc.). Besides the phantom
    /// hammer, any build tool with a piece table whose item, prefab or table name mentions
    /// "hammer" counts, which covers vanilla and modded hammers.
    /// </summary>
    /// <param name="player"></param>
    internal static bool IsHammerEquipped(Player player)
    {
        ItemDrop.ItemData item = player.GetRightItem();
        if (item == null)
        {
            return false;
        }

        if (item == _phantomHammer)
        {
            return true;
        }

        PieceTable table = item.m_shared?.m_buildPieces;
        return table != null
            && (ContainsHammer(item.m_shared.m_name)
            || ContainsHammer(item.m_dropPrefab?.name)
            || ContainsHammer(table.name));
    }

    private static bool ContainsHammer(string name)
    {
        return name?.IndexOf("hammer", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    internal static bool ShiftHeld()
    {
        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
    }

    internal static bool CtrlHeld()
    {
        return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
    }

    private static bool AltHeld()
    {
        return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
    }

    /// <summary>
    /// Alt+scroll zooms the camera while placing with a hammer.
    /// </summary>
    /// <param name="player"></param>
    internal static bool AltZoomActive(Player player)
    {
        return player == Player.m_localPlayer && AltHeld() && player.InPlaceMode() && IsHammerEquipped(player);
    }
}