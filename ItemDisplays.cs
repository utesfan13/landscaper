using HarmonyLib;
using Jotunn.Managers;
using UnityEngine;

namespace Landscaper;

/// <summary>
/// Display pieces: food, drinks, trophies and other items set down on their own, on a table or a shelf. Each
/// is a vanilla horizontal item stand holding the item, with the stand itself hidden, so it's saved
/// as exactly that: without Landscaper it's an ordinary item stand with the item on it. Every item
/// a vanilla item stand shows (it has an "attach" model to draw) gets one, on the Hammer's display
/// tabs: food and drink, trophies, gear, and other items.
/// A display costs the item it shows (or something else, see OtherCosts), except gear, which costs
/// what it's made of (see GearCost), and gives that back when removed, as the piece's refund (the
/// stand's own drop is turned off, so the item comes back once, and never with costs off). The item
/// can't be taken off with the use key; remove the display with the hammer instead.
/// </summary>
internal static class ItemDisplays
{
    /// <summary>The Hammer tabs displays are listed on, by kind of item.</summary>
    public const string FoodCategory = "Food Displays";
    public const string TrophyCategory = "Trophy Displays";
    public const string ItemCategory = "Item Displays";
    public const string GearCategory = "Gear Displays";

    /// <summary>Metals, for a gear display's cost: the metal a piece of gear is made of, if any.</summary>
    private static readonly HashSet<string> Metals = new(StringComparer.OrdinalIgnoreCase)
    {
        "Copper", "Tin", "Bronze", "Iron", "Silver", "BlackMetal", "FlametalNew", "Flametal"
    };
    public const string StandPrefab = "itemstandh";

    /// <summary>
    /// Displays that cost something other than their item, with a name of their own if needed: items
    /// the game never gives out (leftover copies, event-only items), so every display can be built in
    /// normal play (landscaper_check reports any that can't), and items that are hard to come by.
    /// </summary>
    private static readonly Dictionary<string, (string? Name, (string Item, int Amount)[] Cost)> OtherCosts = new(StringComparer.OrdinalIgnoreCase)
    {
        // The Bog Witch's broom, standing still (the animated one is in Props); not her broom's trophy.
        ["TrophyKvastur"] = ("Kvastur (Still)", new[] { ("YggdrasilWood", 4) }),
        ["TrophyDraugrFem"] = ("Draugr Trophy (Female)", new[] { ("TrophyDraugr", 1) }),
        ["Voidplasm"] = ("Ectoplasm (Void)", new[] { ("Ectoplasm", 1) }),
        ["FishAnglerRaw"] = ("Raw Fish (Anglerfish)", new[] { ("FishRaw", 1) }),
        ["HelmetMidsummerCrown"] = (null, new[] { ("Dandelion", 5) }),
        // Shown on a stand as just its grapes.
        ["FeastAshlands"] = ("Vineberry Bunch", new[] { ("Vineberry", 3) })
    };

    /// <summary>The items OtherCosts asks for, which should all exist in the game.</summary>
    public static IEnumerable<string> ReferencedItems() => OtherCosts.Values.SelectMany(value => value.Cost.Select(cost => cost.Item));

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

        // The recipes are set on ObjectDB in the scene, whether or not it's awake yet.
        var objectDB = ObjectDB.instance != null ? ObjectDB.instance : Resources.FindObjectsOfTypeAll<ObjectDB>().FirstOrDefault();
        var traders = ZNetScene.instance.m_prefabs.Where(prefab => prefab != null).Select(prefab => prefab.GetComponent<Trader>())
            .Where(trader => trader != null).ToList();
        foreach (var prefab in ZNetScene.instance.m_prefabs)
        {
            // Only the game's own items, so every player with the same game version gets the same set.
            if (prefab == null || !prefab.TryGetComponent<ItemDrop>(out var item) || ItemManager.Instance.GetItem(prefab.name) != null ||
                !HasStandModel(prefab))
            {
                continue;
            }

            // Gear (anything with durability or upgrade levels) costs what it's made of instead of the
            // item: placing takes one by name from the inventory, which could be an upgraded one, and
            // the refund would be a new one. Hair and beards aren't items to show.
            var shared = item.m_itemData.m_shared;
            if (shared.m_itemType == ItemDrop.ItemData.ItemType.Customization)
            {
                continue;
            }

            var isGear = shared.m_useDurability || shared.m_maxQuality > 1;
            var gearCost = isGear ? GearCost(prefab.name, objectDB, traders) : null;
            if (isGear && gearCost == null)
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

            OtherCosts.TryGetValue(prefab.name, out var otherCost);
            name = otherCost.Name ?? name;
            var definition = new DecorativePieceDefinition
            {
                PrefabName = StandPrefab,
                DisplayItem = prefab.name,
                DisplayName = name,
                Description = $"{name}, set down for display.",
                Tool = BuildTool.Hammer,
                Category = isGear ? GearCategory : shared.m_itemType switch
                {
                    ItemDrop.ItemData.ItemType.Consumable => FoodCategory,
                    ItemDrop.ItemData.ItemType.Trophy => TrophyCategory,
                    _ => ItemCategory
                }
            };
            foreach (var (costItem, amount) in gearCost ?? otherCost.Cost ?? new[] { (prefab.name, 1) })
            {
                definition.Requirements.Add(new PieceRequirement { ItemName = costItem, Amount = amount });
            }
            definitions.Add(definition);
        }

        return definitions.OrderBy(definition => definition.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>
    /// What a gear display costs: the main material of the gear's own recipe, the metal if it has one
    /// (else its largest ingredient), as much as crafting it takes; for gear that's bought instead,
    /// its price in coins. Null for gear that's neither (it isn't listed).
    /// </summary>
    private static (string Item, int Amount)[]? GearCost(string itemName, ObjectDB? objectDB, List<Trader> traders)
    {
        var recipe = objectDB?.m_recipes.FirstOrDefault(recipe => recipe != null && recipe.m_enabled && recipe.m_item != null &&
                                                                  recipe.m_item.name == itemName);
        var resources = recipe?.m_resources?.Where(resource => resource.m_resItem != null && resource.m_amount > 0).ToList();
        if (resources is { Count: > 0 })
        {
            var main = resources.Where(resource => Metals.Contains(resource.m_resItem.name)).OrderByDescending(resource => resource.m_amount)
                .FirstOrDefault() ?? resources.OrderByDescending(resource => resource.m_amount).First();
            return new[] { (main.m_resItem.name, main.m_amount) };
        }

        var sold = traders.SelectMany(trader => trader.m_items)
            .FirstOrDefault(tradeItem => tradeItem.m_prefab != null && tradeItem.m_prefab.name == itemName);
        return sold != null ? new[] { ("Coins", Mathf.Max(1, sold.m_price)) } : null;
    }

    /// <summary>The variant of a display on the game's upright item stand, for walls (see WallDisplays).</summary>
    public const string WallVariant = "Wall";
    public const string WallStandPrefab = "itemstand";

    /// <summary>
    /// The wall version of each display: the same item, name and cost on the upright item stand. It's
    /// kept out of the menus; WallDisplays swaps it in while a display is aimed at a wall.
    /// </summary>
    public static List<DecorativePieceDefinition> WallDefinitions(IEnumerable<DecorativePieceDefinition> displays) =>
        displays.Select(display =>
        {
            var wall = new DecorativePieceDefinition
            {
                PrefabName = WallStandPrefab,
                DisplayItem = display.DisplayItem,
                DisplayName = display.DisplayName,
                Description = display.Description,
                Tool = display.Tool,
                Category = display.Category,
                Variant = WallVariant
            };
            foreach (var requirement in display.Requirements)
            {
                wall.Requirements.Add(new PieceRequirement { ItemName = requirement.ItemName, Amount = requirement.Amount });
            }

            return wall;
        }).ToList();

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
