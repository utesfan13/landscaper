using UnityEngine;

namespace Landscaper;

/// <summary>
/// Which items a player can get in normal play: crafted, cooked, smelted or fermented, dropped by
/// creatures, picked, mined, chopped, bought, caught, or found in chests and lying around in
/// locations and dungeons. For landscaper_check, so no piece costs an item nobody can get.
/// </summary>
internal static class Obtainable
{
    /// <summary>
    /// The names of every obtainable item. Loads every location and dungeon room to look inside them,
    /// so it takes a moment; only for the console command.
    /// </summary>
    public static HashSet<string> Items()
    {
        var items = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<GameObject>();

        foreach (var recipe in ObjectDB.instance.m_recipes)
        {
            if (recipe != null && recipe.m_enabled && recipe.m_item != null)
            {
                items.Add(recipe.m_item.name);
            }
        }

        foreach (var prefab in ZNetScene.instance.m_prefabs)
        {
            Scan(prefab, items, seen, ownItem: false);
        }

        foreach (var location in ZoneSystem.instance.m_locations.Where(location => location.m_enable))
        {
            ScanReference(location.m_prefab, items, seen);
        }

        foreach (var room in DungeonDB.GetRooms().Where(room => room.m_enabled))
        {
            ScanReference(room.m_prefab, items, seen);
        }

        return items;
    }

    private static void ScanReference(SoftReferenceableAssets.SoftReference<GameObject> reference, HashSet<string> items, HashSet<GameObject> seen)
    {
        if (!reference.IsValid)
        {
            return;
        }

        reference.Load();
        try
        {
            if (reference.Asset is { } root)
            {
                // Items lying around in a location can be picked up as they are.
                foreach (var drop in root.GetComponentsInChildren<ItemDrop>(includeInactive: true))
                {
                    items.Add(Utils.GetPrefabName(drop.gameObject));
                }

                foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(includeInactive: true))
                {
                    ScanComponent(component, items, seen);
                }
            }
        }
        finally
        {
            reference.Release();
        }
    }

    /// <summary>What an object gives: through its components, and itself if it's an item that's dropped.</summary>
    private static void Scan(GameObject? prefab, HashSet<string> items, HashSet<GameObject> seen, bool ownItem)
    {
        if (prefab == null || !seen.Add(prefab) && !ownItem)
        {
            return;
        }

        if (ownItem && prefab.GetComponent<ItemDrop>() != null)
        {
            items.Add(prefab.name);
        }

        foreach (var component in prefab.GetComponents<MonoBehaviour>())
        {
            ScanComponent(component, items, seen);
        }
    }

    private static void Gives(GameObject? prefab, HashSet<string> items, HashSet<GameObject> seen) => Scan(prefab, items, seen, ownItem: true);

    private static void Gives(DropTable? table, HashSet<string> items, HashSet<GameObject> seen)
    {
        if (table?.m_drops is null)
        {
            return;
        }

        foreach (var drop in table.m_drops)
        {
            Gives(drop.m_item, items, seen);
        }
    }

    private static void ScanComponent(MonoBehaviour component, HashSet<string> items, HashSet<GameObject> seen)
    {
        switch (component)
        {
            case CharacterDrop characterDrop:
                foreach (var drop in characterDrop.m_drops)
                {
                    Gives(drop.m_prefab, items, seen);
                }

                break;
            case Pickable pickable:
                Gives(pickable.m_itemPrefab, items, seen);
                Gives(pickable.m_extraDrops, items, seen);
                break;
            case PickableItem pickableItem:
                Gives(pickableItem.m_itemPrefab != null ? pickableItem.m_itemPrefab.gameObject : null, items, seen);
                foreach (var random in pickableItem.m_randomItemPrefabs)
                {
                    Gives(random.m_itemPrefab != null ? random.m_itemPrefab.gameObject : null, items, seen);
                }

                break;
            case Container container:
                Gives(container.m_defaultItems, items, seen);
                break;
            case DropOnDestroyed dropOnDestroyed:
                Gives(dropOnDestroyed.m_dropWhenDestroyed, items, seen);
                break;
            case MineRock mineRock:
                Gives(mineRock.m_dropItems, items, seen);
                break;
            case MineRock5 mineRock5:
                Gives(mineRock5.m_dropItems, items, seen);
                break;
            case TreeBase tree:
                Gives(tree.m_dropWhenDestroyed, items, seen);
                Gives(tree.m_logPrefab, items, seen);
                Gives(tree.m_stubPrefab, items, seen);
                break;
            case TreeLog log:
                Gives(log.m_dropWhenDestroyed, items, seen);
                Gives(log.m_subLogPrefab, items, seen);
                break;
            case Destructible destructible:
                Gives(destructible.m_spawnWhenDestroyed, items, seen);
                break;
            case Trader trader:
                foreach (var tradeItem in trader.m_items)
                {
                    Gives(tradeItem.m_prefab != null ? tradeItem.m_prefab.gameObject : null, items, seen);
                }

                break;
            case CookingStation cooking:
                foreach (var conversion in cooking.m_conversion)
                {
                    Gives(conversion.m_to != null ? conversion.m_to.gameObject : null, items, seen);
                }

                Gives(cooking.m_overCookedItem != null ? cooking.m_overCookedItem.gameObject : null, items, seen);
                break;
            case Smelter smelter:
                foreach (var conversion in smelter.m_conversion)
                {
                    Gives(conversion.m_to != null ? conversion.m_to.gameObject : null, items, seen);
                }

                break;
            case Fermenter fermenter:
                foreach (var conversion in fermenter.m_conversion)
                {
                    Gives(conversion.m_to != null ? conversion.m_to.gameObject : null, items, seen);
                }

                break;
            case Incinerator incinerator:
                foreach (var conversion in incinerator.m_conversions ?? new List<Incinerator.IncineratorConversion>())
                {
                    Gives(conversion.m_result != null ? conversion.m_result.gameObject : null, items, seen);
                }

                Gives(incinerator.m_defaultResult != null ? incinerator.m_defaultResult.gameObject : null, items, seen);
                break;
            case Beehive beehive:
                Gives(beehive.m_honeyItem != null ? beehive.m_honeyItem.gameObject : null, items, seen);
                break;
            case SapCollector sapCollector:
                Gives(sapCollector.m_spawnItem != null ? sapCollector.m_spawnItem.gameObject : null, items, seen);
                break;
            case Procreation procreation:
                Gives(procreation.m_offspring, items, seen);
                break;
            case EggGrow eggGrow:
                Gives(eggGrow.m_grownPrefab, items, seen);
                break;
            case LootSpawner lootSpawner:
                Gives(lootSpawner.m_items, items, seen);
                break;
            case Fish fish:
                // A fish is caught as itself.
                items.Add(Utils.GetPrefabName(fish.gameObject));
                Gives(fish.m_pickupItem, items, seen);
                Gives(fish.m_extraDrops, items, seen);
                break;
        }
    }
}
