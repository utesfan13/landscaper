# landscaper
Valheim Mod - Prefab placement

Adds trees, rocks, plants, furniture and building pieces to the Cultivator, Hoe and Hammer menus.
Pieces are free by default and need no workbench.

## Settings

In `BepInEx/config/landscaper.zackc.cfg` (restart Valheim after changing any of them):

| Setting | Default | Effect |
|---|---|---|
| `General.Enabled` | `true` | Master toggle. |
| `General.DecorativeOnly` | `false` | When `true`, placed trees, rocks and plants can't be chopped, mined or picked. Remove them with the remove button (middle click). |
| `Tools.CultivatorDecorEnabled` / `HoeDecorEnabled` / `HammerDecorEnabled` | `true` | Add pieces to that tool's menu. |
| `CustomEntries.Entries` | empty | Extra pieces; see below. |
| `Scaling.ScaleUpKey` / `ScaleDownKey` | `]` / `[` | Resize the piece being placed. |
| `Scaling.XAxisModifier` / `YAxisModifier` / `ZAxisModifier` | `LeftAlt` / `LeftShift` / `LeftControl` | Hold with the scale keys to change one axis. Either side of the keyboard works. |
| `Scaling.ScaleResetKey` | `End` | Reset to normal size and colour. |
| `Scaling.ScaleStep` | `0.1` | How much each key press changes the scale, as a fraction of the current size (0.1 = 10%). |
| `Scaling.ScaleRepeatRate` | `15` | Steps per second while a scale key is held down. |
| `Scaling.MinScale` / `MaxScale` | `0.01` / `20` | Smallest and largest scale allowed on any axis (0.01 to 1, and 1 to 20). |
| `Tint.TintBackKey` / `TintForwardKey` | `,` / `.` | Tint the piece being placed. |
| `Tint.StrengthModifier` / `BrightnessModifier` / `PresetModifier` | `LeftShift` / `LeftControl` / `LeftAlt` | Hold with the tint keys to change strength, brightness, or step through presets. |
| `Tint.HueStep` | `10` | Degrees round the colour wheel per key press. |
| `Tint.StrengthBrightnessStep` | `0.1` | How much each key press changes strength or brightness (0.1 = 10%). |
| `Tint.TintRepeatRate` | `15` | Steps per second while a tint key is held down. |
| `Tint.Presets` | 10 presets | `Name:hue,strength,brightness` entries separated by semicolons; see below. |

## Removing Pieces

Aim at a placed Landscaper piece and press the remove button (middle click by default), the same as
removing a building piece with the Hammer. This works with the Hammer, Cultivator and Hoe. The
Cultivator and Hoe only remove Landscaper pieces; crops, buildings and anything else are left alone.
The piece that will be removed is highlighted in light blue while you aim at it.

For pieces that are hard to aim at, open the console (F5) and run `landscaper_remove [radius]` to
remove every Landscaper piece within that many meters of you (default 10, max 100). It never
removes anything that wasn't placed through this mod.

## Resizing Pieces While Placing

While the placement preview of any Landscaper piece is showing:

| Keys | Effect |
|---|---|
| `]` / `[` | Bigger / smaller on all axes |
| Alt + `]` / `[` | Wider / narrower (X) |
| Shift + `]` / `[` | Taller / shorter (Y) |
| Ctrl + `]` / `[` | Deeper / shallower (Z) |
| End | Reset to normal size |

Each step changes the size by 10% of its current value, so steps are fine near normal size and
quick at large sizes. Hold a scale key to keep resizing; after a short pause it repeats until you let go. Each axis goes
from 0.01x to 20x by default; narrow it with `MinScale` and `MaxScale`. The current scale is shown mid-screen. It stays set while you place
more of the same piece, and resets to normal size whenever you select a different piece (including
switching back to one you scaled earlier). Each placed piece saves its size with the world.
X and Z follow the piece's own orientation, so they swap roles as you rotate it.

## Tinting Pieces While Placing

While the placement preview of any Landscaper piece is showing:

| Keys | Effect |
|---|---|
| `,` / `.` | Hue backward / forward round the colour wheel (red, yellow, green, cyan, blue, magenta) |
| Shift + `,` / `.` | Weaker / stronger tint |
| Ctrl + `,` / `.` | Darker / lighter |
| Alt + `,` / `.` | Previous / next preset |
| End | Reset scale and tint |

The preview shows the tint as you change it, and the current values are shown mid-screen. Like
scaling, the tint stays set while you place more of the same piece, resets when you select a different
piece, and holding a key repeats it. Each placed piece saves its tint with the world, so it survives
reloads and other players see it.

The tint is multiplied with the piece's textures, so it can colour and darken a piece but not make it
brighter than normal. Colours show most on light textures such as stone, bone, ice and birch bark.
While placement is invalid the preview turns red as usual, hiding the tint.

`,` and `.` are also Valheim's minimap zoom keys, so the minimap doesn't zoom while you're in build
mode. The large map and the minimap outside build mode zoom as normal.

Presets are set in `Tint.Presets` as `Name:hue,strength,brightness` entries separated by semicolons.
Hue is 0-360 degrees round the colour wheel (0 red, 120 green, 240 blue); strength and brightness are
percentages. The defaults are Red, Orange, Gold, Green, Teal, Blue, Purple, Pink, Weathered and
Charred, for example `Weathered:35,25,75; Charred:0,0,35`.

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
Display Name|ExactPrefabName|Tool|Category|RotationX,RotationY,RotationZ|Item:Amount,Item:Amount|ScaleX,ScaleY,ScaleZ
```

BepInEx keeps each setting on a single line, so separate multiple entries with a literal `\n`:

```ini
Entries = Giant Oak|Oak1|Cultivator|Trees|||2\nFlat Boulder|Rock_destructible|Hoe|Rocks|||1.5,0.5,1.5
```

Valid tools are `Cultivator`, `Hoe`, and `Hammer`. Rotation, requirements and scale are optional;
leave a field empty to skip it, as in the example above. Requirements use item prefab names
(`Wood`, `Stone`, `Resin`, ...), e.g. `Stone:10`; entries without requirements are free. Scale is
either one number for all axes (`2`) or separate X,Y,Z values (`1.5,0.5,1.5`). Restart Valheim after
changing the config.

A scaled entry is a separate variant, so you can have a normal Oak Tree and a Giant Oak at the same
time. Variants are saved under their display name: you can change a variant's scale freely, but
renaming it makes copies already placed in a world disappear. Each placed copy saves its own size,
so a new scale only applies to copies placed afterwards.

### In code

Add an `E("Display Name", "PrefabName")` line to the matching group in `PieceCatalog.cs`. Each
group sets the tool and menu category. For a scaled variant, pass a scale:
`E("Giant Oak", "Oak1", scale: new Vector3(2f, 2f, 2f))`. Rebuild and copy `Landscaper.dll` into
`BepInEx/plugins/Landscaper`.

Don't move an existing prefab to a different tool. Placed pieces are saved under
`Landscaper_<prefab>_<tool>` (plus the display name for scaled variants), so changing the tool makes copies already placed in a world disappear.
