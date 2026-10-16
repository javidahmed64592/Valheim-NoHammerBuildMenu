using HarmonyLib;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NoHammerBuildMenu
{
    // Adds this mod's controls to the vanilla build key hints (bottom of the screen while placing
    // pieces). Each hint is built from clones of the vanilla hint pieces (label, boxed key, "+" and
    // the scroll icon) in a row right-aligned just above the vanilla hints, and is only shown while
    // a hammer the mod patches is equipped.
    internal static class KeyHintUI
    {
        private const string ContainerName = "NoHammerBuildMenu_KeyHints";
        private const string ScrollIcon = "@scroll";

        private enum HintVisibility { Always, GridOnly, BuildOnly }

        private class Hint
        {
            public string Label;
            public string[] Keys;   // localisation tokens or literal key names; ScrollIcon draws the scroll wheel icon
            public HintVisibility Visibility;
            public GameObject Item;
        }

        // Left to right; the last one sits above the vanilla "Rotate" hint.
        private static readonly List<Hint> Hints = new List<Hint>
        {
            new Hint { Label = "Repair mode", Keys = new[] { "Left Shift", "$button_mouse1" }, Visibility = HintVisibility.Always },
            new Hint { Label = "Build grid", Keys = new[] { "Left Ctrl", "$button_mouse1" }, Visibility = HintVisibility.BuildOnly },
            new Hint { Label = "Grid distance", Keys = new[] { "Left Ctrl", ScrollIcon }, Visibility = HintVisibility.GridOnly },
            new Hint { Label = "Zoom", Keys = new[] { "Left Alt", ScrollIcon }, Visibility = HintVisibility.Always },
        };

        private static GameObject _container;
        private static RectTransform _rotateAnchor;
        private static bool _createFailed;
        private static bool _layoutDirty;

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

            if (_container == null && (_createFailed || !Create(keyHints)))
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

                if (hint.Item.activeSelf != visible)
                {
                    hint.Item.SetActive(visible);
                    _layoutDirty = true;
                }
            }

            if (!_container.activeSelf)
            {
                _container.SetActive(true);
                _layoutDirty = true;
            }

            if (_layoutDirty)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)_container.transform);
                _layoutDirty = false;
            }

            AlignToRotateHint();
        }

        // Right edge and top of the container line up with the vanilla "Rotate" hint's.
        private static void AlignToRotateHint()
        {
            var corners = new Vector3[4];
            _rotateAnchor.GetWorldCorners(corners); // 0 = bottom left, 1 = top left, 2 = top right, 3 = bottom right
            _container.transform.position = new Vector3(corners[2].x, corners[2].y, corners[2].z);
        }

        private static bool Create(KeyHints keyHints)
        {
            Transform keyboard = keyHints.m_buildHints.transform.Find("Keyboard");
            Transform copy = keyboard != null ? keyboard.Find("Copy") : null;
            Transform rotate = keyboard != null ? keyboard.Find("rotate") : null;
            Transform labelTemplate = copy != null ? copy.Find("Text") : null;
            Transform plusTemplate = copy != null ? copy.Find("Text (1)") : null;
            Transform keyTemplate = copy != null ? copy.Find("key_bkg") : null;
            Transform iconTemplate = rotate != null ? rotate.Find("mousew_icon") : null;
            var keyboardLayout = keyboard != null ? keyboard.GetComponent<HorizontalLayoutGroup>() : null;

            if (labelTemplate == null || plusTemplate == null || keyTemplate == null || iconTemplate == null || keyboardLayout == null)
            {
                _createFailed = true;
                Debug.LogWarning("NoHammerBuildMenu: vanilla build key hints layout not as expected, not adding key hints.");
                return false;
            }

            var rect = new GameObject(ContainerName, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(keyHints.m_buildHints.transform, false);
            rect.pivot = new Vector2(1f, 0f);

            // Same row settings as the vanilla hints.
            var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = keyboardLayout.spacing;
            layout.childAlignment = TextAnchor.LowerRight;
            layout.childControlWidth = keyboardLayout.childControlWidth;
            layout.childControlHeight = keyboardLayout.childControlHeight;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var fitter = rect.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            foreach (Hint hint in Hints)
            {
                // Start from a vanilla multi-key hint (for its row settings), then rebuild its contents.
                GameObject item = Object.Instantiate(copy.gameObject, rect, false);
                item.name = "NoHammerBuildMenu_Hint_" + hint.Label;
                item.SetActive(true);
                for (int i = item.transform.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(item.transform.GetChild(i).gameObject);

                AddText(labelTemplate, item.transform, hint.Label);

                for (int i = 0; i < hint.Keys.Length; i++)
                {
                    if (i > 0)
                        AddText(plusTemplate, item.transform, "+");

                    if (hint.Keys[i] == ScrollIcon)
                    {
                        Object.Instantiate(iconTemplate.gameObject, item.transform, false).SetActive(true);
                    }
                    else
                    {
                        GameObject key = Object.Instantiate(keyTemplate.gameObject, item.transform, false);
                        key.SetActive(true);
                        key.GetComponentInChildren<TextMeshProUGUI>().text = Localization.instance.Localize(hint.Keys[i]);
                    }
                }

                hint.Item = item;
            }

            _rotateAnchor = (RectTransform)rotate;
            _container = rect.gameObject;
            _layoutDirty = true;
            return true;
        }

        private static void AddText(Transform template, Transform parent, string text)
        {
            GameObject clone = Object.Instantiate(template.gameObject, parent, false);
            clone.SetActive(true);

            // Vanilla labels can have a fixed width sized for their own text.
            LayoutElement fixedSize = clone.GetComponent<LayoutElement>();
            if (fixedSize != null)
                Object.DestroyImmediate(fixedSize);

            TextMeshProUGUI label = clone.GetComponent<TextMeshProUGUI>();
            label.enableAutoSizing = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.text = text;
        }

        // The hints live under the HUD, which is rebuilt with each world load.
        public static void Reset()
        {
            _container = null;
            _rotateAnchor = null;
            _createFailed = false;
            foreach (Hint hint in Hints)
                hint.Item = null;
        }

        [HarmonyPatch(typeof(KeyHints), "UpdateHints")]
        private static class KeyHints_UpdateHints_Patch
        {
            [HarmonyPostfix]
            private static void Postfix(KeyHints __instance)
            {
                // Drop stale references if the HUD (and our container with it) was destroyed.
                if (!ReferenceEquals(_container, null) && _container == null)
                    Reset();
                Update(__instance, Player.m_localPlayer);
            }
        }
    }
}
