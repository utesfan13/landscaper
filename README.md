# landscaper
Valheim Mod - Prefab placement

## Finding Prefab Names

Load into a world, open the console (F5) and run:

```text
landscaper_find <keyword>
```

For example, `landscaper_find crypt` lists every prefab whose name contains "crypt". Only spawnable
prefabs (ones with a `ZNetView`) are listed. Pieces built from them are saved with the world. Many
dungeon and location parts, such as individual walls and floors, are not spawnable on their own and
won't appear. Results tagged `(item)` or `(creature)` are not good choices for placeable pieces.

## Adding Prefabs

### From the config file (no rebuild)

Add entries in `BepInEx/config/landscaper.zackc.cfg` under `[CustomEntries]`:

```text
Display Name|ExactPrefabName|Tool|Category|RotationX,RotationY,RotationZ
```

BepInEx keeps each setting on a single line, so separate multiple entries with a literal `\n`:

```ini
Entries = Rock Thumb|RockThumb|Hoe|Rocks|0,0,0\nDolmen|RockDolmen_1|Hoe|Rocks|0,0,0
```

Valid tools are `Cultivator`, `Hoe`, and `Hammer`. The rotation part is optional. Restart Valheim
after changing the config.

### In code

Add a `("Display Name", "PrefabName")` pair to one of the arrays in `OtherCatalog.cs`:
`ResourceNodes` (trees go on the Cultivator, everything else on the Hoe), `Furniture` or
`BuildingStructures` (both on the Hammer). Rebuild and copy `Landscaper.dll` into
`BepInEx/plugins/Landscaper`.
