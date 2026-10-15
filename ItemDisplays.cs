using HarmonyLib;
using Jotunn.Managers;
using UnityEngine;

namespace Landscaper;

/// <summary>
/// Display pieces: food, drinks, trophies and other items set down on their own, on a table or a shelf. Each
/// is a vanilla horizontal item stand holding the item, with the stand itself hidden, so it's saved
/// as exactly that: without Landscaper it's an ordinary item stand with the item on it. Every item
/// a vanilla item stand shows (it has an "attach" model to draw) gets one, except gear, on the
/// Hammer's display tabs: food and drink, trophies, and other items.
/// A display costs the item it shows and gives it back when removed, as the piece's refund (the
/// stand's own drop is turned off, so the item comes back once, and never with costs off). The item
/// can't be taken off with the use key; remove the display with the hammer instead.
/// </summary>
internal static class ItemDisplays
{
    /// <summary>The Hammer tabs displays are listed on, by kind of item.</summary>
    public const string FoodCategory = "Food Displays";
    public const string TrophyCategory = "Trophy Displays";
    public const string ItemCategory = "Item Displays";
    public const string StandPrefab = "itemstandh";

    /// <summary>On a display piece: the item it shows.</summary>
    public sealed class Marker : MonoBehaviour
    {
        public string Item = "";
    }

    /// <summary>The display definitions, one per item that a vanilla item stand can hold, sorted by name.</summary>
    public static List<DecorativePieceDefinition> Definitions()
    {
        // Read from ZNetScene, which lists every item too: pieces are registered as it wakes, before
        // ObjectDB has.
        var definitions = new List<DecorativePieceDefinition>();
        if (ZNetScene.instance == null)
        {
            return definitions;
        }

        foreach (var prefab in ZNetScene.instance.m_prefabs)
        {
            // Only the game's own items, so every player with the same game version gets the same set.
            if (prefab == null || !prefab.TryGetComponent<ItemDrop>(out var item) || ItemManager.Instance.GetItem(prefab.name) != null ||
                !HasStandModel(prefab))
            {
                continue;
            }

            // Not gear (anything with durability or upgrade levels): placing takes one by name from the
            // inventory, which could be an upgraded one, and the refund would be a new one.
            var shared = item.m_itemData.m_shared;
            if (shared.m_useDurability || shared.m_maxQuality > 1 || shared.m_itemType == ItemDrop.ItemData.ItemType.Customization)
            {
                continue;
            }

            // Finished items are named with a translation key ("$item_..."); a plain name, such as
            // "Yagluth thing", is a leftover the game never gives out.
            if (!shared.m_name.StartsWith("$", StringComparison.Ordinal))
            {
                continue;
            }

            var name = Localization.instance.Localize(shared.m_name);
            if (string.IsNullOrWhiteSpace(name) || name.StartsWith("[", StringComparison.Ordinal))
            {
                continue;
            }

            var definition = new DecorativePieceDefinition
            {
                PrefabName = StandPrefab,
                DisplayItem = prefab.name,
                DisplayName = name,
                Description = $"{name}, set down for display.",
                Tool = BuildTool.Hammer,
                Category = shared.m_itemType switch
                {
                    ItemDrop.ItemData.ItemType.Consumable => FoodCategory,
                    ItemDrop.ItemData.ItemType.Trophy => TrophyCategory,
                    _ => ItemCategory
                }
            };
            definition.Requirements.Add(new PieceRequirement { ItemName = prefab.name, Amount = 1 });
            definitions.Add(definition);
        }

        return definitions.OrderBy(definition => definition.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>
    /// Whether an item stand shows something for the item: it has a stand model ("attach") that's
    /// switched on and has a mesh to draw. Feasts have one that's switched off (the game sets feasts
    /// down as a whole spread instead), and one of the troll trophies has one without a mesh.
    /// </summary>
    private static bool HasStandModel(GameObject item)
    {
        var attachPrefab = ItemStand.GetAttachPrefab(item);
        if (attachPrefab == null)
        {
            return false;
        }

        // Checked by hand: a prefab isn't in a scene, so Unity's own "active in hierarchy" doesn't apply.
        var model = ItemStand.GetAttachGameObject(attachPrefab);
        return model.activeSelf &&
            (model.GetComponentsInChildren<MeshFilter>(includeInactive: true)
                 .Any(filter => filter.sharedMesh != null && filter.GetComponent<MeshRenderer>() != null && IsOn(filter.transform, model.transform)) ||
             model.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true)
                 .Any(skinned => skinned.sharedMesh != null && IsOn(skinned.transform, model.transform)));
    }

    /// <summary>Whether an object and every parent up to the model are switched on.</summary>
    private static bool IsOn(Transform transform, Transform model)
    {
        for (var current = transform; current != null; current = current.parent)
        {
            if (!current.gameObject.activeSelf)
            {
                return false;
            }

            if (current == model)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Turns a clone of the item stand into a display of the item: hides the stand, adds the item's
    /// model for the placement ghost, and a collider the size of the item, so it can be aimed at.
    /// Returns the item's icon for the menu, or null if the item can't be displayed.
    /// </summary>
    public static Sprite? Prepare(GameObject clone, string itemName)
    {
        var item = ZNetScene.instance?.GetPrefab(itemName);
        var attachPrefab = item != null ? ItemStand.GetAttachPrefab(item) : null;
        if (item == null || attachPrefab == null || !clone.TryGetComponent<ItemStand>(out var stand) || stand.m_attachOther == null)
        {
            return null;
        }

        var itemData = item.GetComponent<ItemDrop>().m_itemData;
        var root = clone.transform;

        // The stand is hidden; placed, the stand shows the item it holds.
        foreach (var renderer in clone.GetComponentsInChildren<Renderer>(includeInactive: true))
        {
            UnityEngine.Object.DestroyImmediate(renderer);
        }

        foreach (var lod in clone.GetComponentsInChildren<LODGroup>(includeInactive: true))
        {
            UnityEngine.Object.DestroyImmediate(lod);
        }

        // The ghost shows the item where the stand will put it ("_GhostOnly" is only shown on ghosts).
        var ghostOnly = new GameObject("_GhostOnly");
        ghostOnly.SetActive(false);
        ghostOnly.transform.SetParent(root, worldPositionStays: false);
        var attach = new GameObject("attach").transform;
        attach.SetParent(ghostOnly.transform, worldPositionStays: false);
        attach.localPosition = root.InverseTransformPoint(stand.m_attachOther.position);
        attach.localRotation = Quaternion.Inverse(root.rotation) * stand.m_attachOther.rotation;

        // The same placement as the stand's own (ItemStand.SetVisualItem), for the first orientation.
        var visual = UnityEngine.Object.Instantiate(ItemStand.GetAttachGameObject(attachPrefab), attach, worldPositionStays: false);
        visual.transform.localPosition = attachPrefab.transform.localPosition;
        visual.transform.localRotation = attachPrefab.transform.localRotation;
        visual.transform.localScale = Vector3.Scale(attachPrefab.transform.localScale, itemData.GetScale(1f));
        if (itemData.m_shared.m_itemStandOffsets.FirstOrDefault(offset => (stand.m_orientationType & offset.m_orientations) != 0) is { } orientation)
        {
            visual.transform.localPosition += orientation.m_position;
            visual.transform.localRotation *= Quaternion.Euler(orientation.m_rotation);
            visual.transform.localScale = Vector3.Scale(visual.transform.localScale, orientation.m_scale);
        }

        // Only the model: no scripts or colliders from the item.
        foreach (var behaviour in visual.GetComponentsInChildren<MonoBehaviour>(includeInactive: true))
        {
            UnityEngine.Object.DestroyImmediate(behaviour);
        }

        foreach (var collider in visual.GetComponentsInChildren<Collider>(includeInactive: true))
        {
            UnityEngine.Object.DestroyImmediate(collider);
        }

        AddItemCollider(clone, visual.transform);
        clone.AddComponent<Marker>().Item = itemName;
        return itemData.GetIcon();
    }

    /// <summary>A solid box around the item's model, so the display can be aimed at and built on.</summary>
    private static void AddItemCollider(GameObject clone, Transform visual)
    {
        var root = clone.transform;
        var hasBounds = false;
        var bounds = new Bounds();
        foreach (var filter in visual.GetComponentsInChildren<MeshFilter>(includeInactive: true))
        {
            if (filter.sharedMesh != null)
            {
                Encapsulate(filter.sharedMesh.bounds, filter.transform);
            }
        }

        foreach (var skinned in visual.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true))
        {
            if (skinned.sharedMesh != null)
            {
                Encapsulate(skinned.sharedMesh.bounds, skinned.transform);
            }
        }

        if (!hasBounds)
        {
            return;
        }

        var box = new GameObject("LandscaperDisplayCollider") { layer = LayerMask.NameToLayer("piece") };
        box.transform.SetParent(root, worldPositionStays: false);
        var collider = box.AddComponent<BoxCollider>();
        collider.center = bounds.center;
        collider.size = Vector3.Max(bounds.size, new Vector3(0.05f, 0.05f, 0.05f));

        void Encapsulate(Bounds mesh, Transform from)
        {
            for (var corner = 0; corner < 8; corner++)
            {
                var point = new Vector3(
                    (corner & 1) == 0 ? mesh.min.x : mesh.max.x,
                    (corner & 2) == 0 ? mesh.min.y : mesh.max.y,
                    (corner & 4) == 0 ? mesh.min.z : mesh.max.z);
                var local = root.InverseTransformPoint(from.TransformPoint(point));
                if (hasBounds)
                {
                    bounds.Encapsulate(local);
                }
                else
                {
                    bounds = new Bounds(local, Vector3.zero);
                    hasBounds = true;
                }
            }
        }
    }

    private static bool IsDisplay(Component component) => component.TryGetComponent<Marker>(out _);

    /// <summary>Puts the item on a display's stand as it's placed.</summary>
    [HarmonyPatch(typeof(Piece), nameof(Piece.SetCreator))]
    private static class AttachItemWhenPlacedPatch
    {
        private static void Postfix(Piece __instance)
        {
            if (!__instance.TryGetComponent<Marker>(out var marker) || !__instance.TryGetComponent<ZNetView>(out var view) ||
                !view.IsValid() || !view.IsOwner() || ObjectDB.instance?.GetItemPrefab(marker.Item) is not { } prefab)
            {
                return;
            }

            var item = prefab.GetComponent<ItemDrop>().m_itemData.Clone();
            item.m_stack = 1;
            item.m_dropPrefab = prefab;
            var hash = prefab.name.GetStableHashCode();
            var zdo = view.GetZDO();
            zdo.Set(ZDOVars.s_item, hash);
            ItemDrop.SaveToZDO(item, zdo);
            view.InvokeRPC(ZNetView.Everybody, "SetVisualItem", hash, item.m_variant, item.m_quality, 0);
        }
    }

    /// <summary>The item can't be taken off a display (the alternate use key still turns it).</summary>
    [HarmonyPatch(typeof(ItemStand), nameof(ItemStand.Interact))]
    private static class InteractPatch
    {
        private static bool Prefix(ItemStand __instance, bool alt, ref bool __result)
        {
            if (!IsDisplay(__instance) || alt)
            {
                return true;
            }

            __result = false;
            return false;
        }
    }

    /// <summary>A display is named after its item, without the item stand's actions.</summary>
    [HarmonyPatch(typeof(ItemStand), nameof(ItemStand.GetHoverText))]
    private static class HoverTextPatch
    {
        private static bool Prefix(ItemStand __instance, ref string __result)
        {
            if (!IsDisplay(__instance))
            {
                return true;
            }

            __result = Localization.instance.Localize(__instance.m_currentItemName);
            return false;
        }
    }

    /// <summary>
    /// A display's stand never drops its item, whether removed, broken or asked to: the item comes
    /// back as the piece's refund instead, so it's returned once.
    /// </summary>
    [HarmonyPatch(typeof(ItemStand), "DropItem")]
    private static class NoDropPatch
    {
        private static bool Prefix(ItemStand __instance) => !IsDisplay(__instance);
    }
}
