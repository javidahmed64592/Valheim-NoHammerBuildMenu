using HarmonyLib;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NoHammerBuildMenu
{
    // Adds this mod's controls to the vanilla build key hints (bottom of the screen while placing
    // pieces). Each hint is a clone of a vanilla hint's text, laid out in a row just above the
    // vanilla hints, and is only shown while a hammer the mod patches is equipped.
    internal static class KeyHintUI
    {
        private const string ContainerName = "NoHammerBuildMenu_KeyHints";

        private enum HintVisibility { Always, GridOnly, BuildOnly }

        private class Hint
        {
            public string Label;
            public string Keys;
            public HintVisibility Visibility;
            public TextMeshProUGUI Text;
            public string AppliedText;
        }

        private static readonly List<Hint> Hints = new List<Hint>
        {
            new Hint { Label = "Repair mode", Keys = "$button_lshift + $button_mouse1", Visibility = HintVisibility.Always },
            new Hint { Label = "Build grid", Keys = "$button_lctrl + $button_mouse1", Visibility = HintVisibility.BuildOnly },
            new Hint { Label = "Grid distance", Keys = "$button_lctrl + Scroll", Visibility = HintVisibility.GridOnly },
            new Hint { Label = "Zoom", Keys = "$button_lalt + Scroll", Visibility = HintVisibility.Always },
        };

        private static GameObject _container;

        // Same text format as the vanilla hints, e.g. "Rotate <mspace=0.6em> $KEY_Block + ...</mspace>".
        private static string Format(Hint hint) =>
            Localization.instance.Localize($"{hint.Label} <mspace=0.6em> {hint.Keys}</mspace>");

        public static void Update(KeyHints keyHints, Player player)
        {
            bool show = keyHints.m_buildHints != null
                && keyHints.m_buildHints.activeInHierarchy
                && player != null
                && player.InPlaceMode()
                && !ZInput.IsGamepadActive()
                && NoHammerBuildMenuPlugin.IsHammerEquipped(player);

            if (!show)
            {
                if (_container != null && _container.activeSelf)
                    _container.SetActive(false);
                return;
            }

            if (_container == null && !Create(keyHints))
                return;

            bool repairing = player.InRepairMode();
            bool gridOn = NoHammerBuildMenuPlugin.GridEnabled;

            foreach (Hint hint in Hints)
            {
                bool visible;
                switch (hint.Visibility)
                {
                    case HintVisibility.BuildOnly: visible = !repairing; break;
                    case HintVisibility.GridOnly: visible = !repairing && gridOn; break;
                    default: visible = true; break;
                }

                hint.Text.gameObject.SetActive(visible);
                if (!visible)
                    continue;

                string text = Format(hint);
                if (text != hint.AppliedText)
                {
                    hint.Text.text = text;
                    hint.AppliedText = text;
                }
            }

            if (!_container.activeSelf)
                _container.SetActive(true);
        }

        private static bool Create(KeyHints keyHints)
        {
            // Any plain-text vanilla hint works as the template for font, size and colour.
            TextMeshProUGUI template = keyHints.m_cycleSnapKey != null ? keyHints.m_cycleSnapKey : keyHints.m_buildMenuKey;
            if (template == null)
                return false;

            var rect = new GameObject(ContainerName, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(keyHints.m_buildHints.transform, false);

            // Sit just above the vanilla build hints, centred.
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 10f);

            var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 40f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var fitter = rect.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            foreach (Hint hint in Hints)
            {
                GameObject clone = Object.Instantiate(template.gameObject, rect, false);
                clone.name = "NoHammerBuildMenu_Hint_" + hint.Label;
                clone.SetActive(true);

                hint.Text = clone.GetComponent<TextMeshProUGUI>();
                hint.Text.textWrappingMode = TextWrappingModes.NoWrap;
                hint.Text.alignment = TextAlignmentOptions.Center;
                hint.AppliedText = null;
            }

            _container = rect.gameObject;
            return true;
        }

        // The hints live under the HUD, which is rebuilt with each world load.
        public static void Reset()
        {
            _container = null;
            foreach (Hint hint in Hints)
            {
                hint.Text = null;
                hint.AppliedText = null;
            }
        }

        [HarmonyPatch(typeof(KeyHints), "UpdateHints")]
        private static class KeyHints_UpdateHints_Patch
        {
            [HarmonyPostfix]
            private static void Postfix(KeyHints __instance)
            {
                if (_container == null && __instance.m_buildHints != null)
                {
                    // Drop stale references if the HUD (and our container with it) was destroyed.
                    Transform existing = __instance.m_buildHints.transform.Find(ContainerName);
                    if (existing == null)
                        Reset();
                }
                Update(__instance, Player.m_localPlayer);
            }
        }
    }
}
