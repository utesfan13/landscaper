using HarmonyLib;
using TMPro;
using UnityEngine;

namespace Landscaper;

/// <summary>
/// Adds a "Scale [ ]" row to the keyboard build hints (place, remove, rotate, ...) that shows while
/// a Landscaper piece is selected. The row is a copy of Valheim's two-key "Snap" row, so it matches
/// the other hints.
/// </summary>
internal static class ScaleKeyHint
{
    private static GameObject? _row;

    /// <summary>Shows the row only while scaling applies. Call every frame.</summary>
    public static void SetVisible(bool visible)
    {
        if (_row != null && _row.activeSelf != visible)
        {
            _row.SetActive(visible);
        }
    }

    [HarmonyPatch(typeof(KeyHints), "Awake")]
    private static class CreateRowPatch
    {
        private static void Postfix(KeyHints __instance)
        {
            var keyboard = __instance.m_buildHints?.transform.Find("Keyboard");
            var snap = keyboard?.Find("Snap");
            if (keyboard is null || snap is null)
            {
                Plugin.Log.LogWarning("Could not find the build key hints; the scale hint won't be shown.");
                return;
            }

            var row = UnityEngine.Object.Instantiate(snap.gameObject, keyboard);
            row.name = "LandscaperScale";
            var rotate = keyboard.Find("rotate");
            row.transform.SetSiblingIndex((rotate ?? snap).GetSiblingIndex() + 1);

            // These would replace the text below with Valheim's own labels and key bindings.
            foreach (var behaviour in row.GetComponentsInChildren<MonoBehaviour>(includeInactive: true))
            {
                if (behaviour.GetType().Name is "Localize" or "UIInputHint")
                {
                    UnityEngine.Object.DestroyImmediate(behaviour);
                }
            }

            SetText(row.transform.Find("Text"), $"Scale (hold {ScaleController.ModifierNames} for X/Y/Z)");
            SetText(row.transform.Find("key_bkg/Key"), ScaleController.DownKeyName);
            SetText(row.transform.Find("key_bkg (1)/Key"), ScaleController.UpKeyName);

            row.SetActive(false);
            _row = row;
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
