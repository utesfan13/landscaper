using HarmonyLib;
using TMPro;
using UnityEngine;

namespace Landscaper;

/// <summary>
/// Adds "Scale", "Tint" and "Raise/lower" rows to the keyboard build hints (place, remove, rotate, ...) that show
/// while a Landscaper piece is selected. The rows are copies of Valheim's two-key "Snap" row and
/// mouse-wheel "rotate" row, so they match the other hints.
/// </summary>
internal static class BuildKeyHints
{
    private static readonly List<GameObject> Rows = new();
    private static TMP_Text? _indestructibleLabel;
    private static GameObject? _indestructibleRow;

    /// <summary>Updates the indestructible row after the mode is toggled.</summary>
    public static void RefreshIndestructibleLabel()
    {
        if (_indestructibleLabel != null)
        {
            _indestructibleLabel.text = IndestructibleController.HintLabel;
        }
    }

    /// <summary>
    /// Shows the scale, tint and height rows while a Landscaper piece is selected, and the
    /// indestructible row whenever building, since that mode applies to every piece. Call every frame.
    /// </summary>
    public static void SetVisible(bool landscaperPiece, bool building)
    {
        foreach (var row in Rows)
        {
            var visible = row == _indestructibleRow ? building : landscaperPiece;
            if (row != null && row.activeSelf != visible)
            {
                row.SetActive(visible);
            }
        }
    }

    [HarmonyPatch(typeof(KeyHints), "Awake")]
    private static class CreateRowsPatch
    {
        private static void Postfix(KeyHints __instance)
        {
            var keyboard = __instance.m_buildHints?.transform.Find("Keyboard");
            var snap = keyboard?.Find("Snap");
            if (keyboard is null || snap is null)
            {
                Plugin.Log.LogWarning("Could not find the build key hints; the scale and tint hints won't be shown.");
                return;
            }

            // KeyHints is recreated with the HUD; drop rows that belonged to the old one.
            Rows.RemoveAll(row => row == null);

            var after = keyboard.Find("rotate") ?? snap;
            var scale = CreateRow(keyboard, snap.gameObject, "LandscaperScale", after.GetSiblingIndex() + 1,
                $"Scale (hold {ScaleController.ModifierNames} for X/Y/Z)", ScaleController.DownKeyName, ScaleController.UpKeyName);
            var tint = CreateRow(keyboard, snap.gameObject, "LandscaperTint", scale.transform.GetSiblingIndex() + 1,
                $"Tint presets ({TintController.ModifierNames}: hue/strength/brightness)",
                TintController.BackKeyName, TintController.ForwardKeyName);

            // The rotate row shows the mouse wheel, which is what moving the piece uses.
            var last = tint;
            var rotate = keyboard.Find("rotate");
            if (rotate is not null)
            {
                last = CreateRow(keyboard, rotate.gameObject, "LandscaperOffset", tint.transform.GetSiblingIndex() + 1,
                    $"Move ({OffsetController.ModifierNames}: up/sideways/forward)", string.Empty, string.Empty);
            }

            // The place row has a single key box, like the toggle.
            var place = keyboard.Find("Place");
            if (place is not null)
            {
                var indestructible = CreateRow(keyboard, place.gameObject, "LandscaperIndestructible", last.transform.GetSiblingIndex() + 1,
                    IndestructibleController.HintLabel, IndestructibleController.ToggleKeyName, string.Empty);
                _indestructibleRow = indestructible;
                _indestructibleLabel = indestructible.transform.Find("Text")?.GetComponent<TMP_Text>();
            }
        }

        private static GameObject CreateRow(Transform parent, GameObject template, string name, int siblingIndex,
            string label, string firstKey, string secondKey)
        {
            var row = UnityEngine.Object.Instantiate(template, parent);
            row.name = name;
            row.transform.SetSiblingIndex(siblingIndex);

            // These would replace the text below with Valheim's own labels and key bindings.
            foreach (var behaviour in row.GetComponentsInChildren<MonoBehaviour>(includeInactive: true))
            {
                if (behaviour.GetType().Name is "Localize" or "UIInputHint")
                {
                    UnityEngine.Object.DestroyImmediate(behaviour);
                }
            }

            SetText(row.transform.Find("Text"), label);

            // These labels are longer than Valheim's one-word ones; shrink the text to fit rather
            // than cutting it off.
            var labelText = row.transform.Find("Text")?.GetComponent<TMP_Text>();
            if (labelText != null)
            {
                labelText.fontSizeMax = labelText.fontSize;
                labelText.fontSizeMin = labelText.fontSize * 0.6f;
                labelText.enableAutoSizing = true;
            }
            SetText(row.transform.Find("key_bkg/Key"), firstKey);
            SetText(row.transform.Find("key_bkg (1)/Key"), secondKey);

            row.SetActive(false);
            Rows.Add(row);
            return row;
        }

        private static void SetText(Transform? transform, string text)
        {
            var label = transform?.GetComponent<TMP_Text>();
            if (label != null)
            {
                label.text = text;
            }
        }
    }
}
