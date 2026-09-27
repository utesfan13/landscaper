# landscaper
Valheim Mod - Prefab placement

## Choosing Prefabs

Add custom entries in `BepInEx/config/landscaper.zackc.cfg` under `CustomEntries`.
Use one entry per line:

```text
Display Name|ExactPrefabName|Tool|Category|RotationX,RotationY,RotationZ
```

Example:

```text
Birch Tree|Birch1|Hammer|Trees|0,0,0
Large Rock|Rock_4|Hoe|Rocks|0,0,0
```

Valid tools are `Cultivator`, `Hoe`, and `Hammer`. The exact prefab name must match a name in Valheim's runtime prefab list. Use the existing prefab dump command from the plugin to search names, then restart Valheim after changing the config.
