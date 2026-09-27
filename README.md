# landscaper
Valheim Mod - Prefab placement

Adds trees, rocks, plants, furniture and building pieces to the Cultivator, Hoe and Hammer menus.
Cattails and lily pads, which Valheim only scatters as ground clutter, are available as placeable
pieces too; lily pads are placed on the water surface.
Pieces cost a little wood or stone (see [Build Costs](#build-costs)) and need no workbench.

## Settings

In `BepInEx/config/landscaper.zackc.cfg`. Settings marked **synced** come from the server in
multiplayer (see [Multiplayer](#multiplayer)) and take effect without a restart.

| Setting | Default | Effect |
|---|---|---|
| `General.Enabled` | `true` | Master toggle. Requires a restart. |
| `Costs.Enabled` | `true` | **Synced.** Charge wood or stone to place pieces; see [Build Costs](#build-costs). |
| `Costs.Multiplier` | `1` | **Synced.** Multiplies the automatic costs (0.25 to 4). |
| `Indestructible.Allowed` | `true` | **Synced.** Allow placing indestructible pieces; see [Indestructible Pieces](#indestructible-pieces). |
| `Indestructible.ToggleKey` | `Backslash` | Turn indestructible placement on or off while building. |
| `General.DecorativeOnly` | `false` | **Synced.** When `true`, placed trees, rocks and plants can't be chopped, mined or picked. Remove them with the remove button (middle click). |
| `Tools.CultivatorDecorEnabled` / `HoeDecorEnabled` / `HammerDecorEnabled` | `true` | **Synced.** List pieces in that tool's menu. Turning one off only hides its pieces; ones already placed stay in the world. |
| `CustomEntries.Entries` | empty | **Synced.** Extra pieces; see below. |
| `Scaling.ScaleUpKey` / `ScaleDownKey` | `]` / `[` | Resize the piece being placed. |
| `Scaling.XAxisModifier` / `YAxisModifier` | `LeftAlt` / `LeftShift` | Hold with the scale keys to change X or Y; hold both for Z. Either side of the keyboard works. |
| `Scaling.ScaleResetKey` | `End` | Reset the size, colour and height of the piece being placed. |
| `Scaling.ScaleStep` | `0.1` | How much each key press changes the scale, as a fraction of the current size (0.1 = 10%). |
| `Scaling.ScaleRepeatRate` | `15` | Steps per second while a scale key is held down. |
| `Scaling.MinScale` / `MaxScale` | `0.01` / `20` | Smallest and largest scale allowed on any axis (0.01 to 1, and 1 to 20). |
| `Tint.TintBackKey` / `TintForwardKey` | `,` / `.` | Step through the tint presets for the piece being placed. |
| `Tint.HueModifier` / `StrengthModifier` | `LeftAlt` / `LeftShift` | Hold with the tint keys to fine-tune hue or strength; hold both for brightness. |
| `Tint.HueStep` | `10` | Degrees round the colour wheel per key press. |
| `Tint.StrengthBrightnessStep` | `0.1` | How much each key press changes strength or brightness (0.1 = 10%). |
| `Tint.TintRepeatRate` | `15` | Steps per second while a tint key is held down. |
| `Tint.Presets` | 10 presets | `Name:hue,strength,brightness` entries separated by semicolons; see below. |
| `Offset.UpDownModifier` / `SideModifier` | `LeftAlt` / `LeftShift` | Hold and scroll to move the piece being placed up/down or sideways; hold both for toward/away from you. |
| `Offset.Step` | `0.1` | Meters per scroll step. |

## Build Costs

Placing a piece costs wood or stone, and removing it refunds the cost:

| Largest dimension of the piece | Cost |
|---|---|
| under 2 m (mushrooms, stools, small rocks) | 2 |
| 2 to 6 m (bushes, benches, boulders) | 4 |
| 6 to 15 m (trees, large statues) | 6 |
| 15 m and over (cliffs, great pillars, frozen ships) | 8 |

Trees, plants, stumps, furniture and wooden props cost wood; rocks, cliffs, ice, bones, statues and
ruins cost stone. Building structures and copied objects go by their name (stone, iron, crystal and
so on cost stone). Custom entries with their own requirements keep them. Resizing a piece while
placing doesn't change its cost.

Turn costs off with `Costs.Enabled`, or scale them with `Costs.Multiplier`. Chopping or mining a
placed tree or rock gives back more than it cost; turn on `DecorativeOnly` to prevent that.

## Multiplayer

Every player, and a dedicated server, needs Landscaper (and Jotunn) installed at the same minor
version, e.g. any 0.8.x. Jotunn checks this when joining and refuses the connection with a message if
a player's version doesn't match. Players without the mod couldn't see Landscaper pieces anyway.

Scale and tint are saved on each placed piece, so every player sees them.

When joining a server, the synced settings (`DecorativeOnly`, the three tool toggles and custom
entries) are taken from the server, and only admins can change them in game. Keybinds, tint presets
and scale limits stay personal.

When you host a world, or play alone, Valheim permanently deletes saved objects whose prefab it can't
find. Removing a custom entry hides it from the menu but keeps it loading until you restart; after a
restart, copies of it already placed in the world are deleted when the world loads. Keep an entry
until you have removed its placed copies, e.g. with `landscaper_remove`.

## Removing Pieces

Aim at a placed Landscaper piece and press the remove button (middle click by default), the same as
removing a building piece with the Hammer. This works with the Hammer, Cultivator and Hoe. The
Cultivator and Hoe only remove Landscaper pieces; crops, buildings and anything else are left alone.
The piece that will be removed is highlighted in light blue while you aim at it.

For pieces that are hard to aim at, open the console (F5) and run `landscaper_remove [radius]` to
remove every Landscaper piece within that many meters of you (default 10, max 100). It never
removes anything that wasn't placed through this mod.

## Modifier Keys

Resizing, tinting and moving each use Alt and Shift as modifiers, with both held together for the
third option; Ctrl isn't used, because it makes your character crouch. On Windows with more than one
keyboard layout installed, Left Alt + Shift also switches the input language; that shortcut can be
turned off in Windows under *Settings > Time & language > Typing > Advanced keyboard settings >
Input language hot keys*, or use Right Alt instead.

## Resizing Pieces While Placing

While the placement preview of any Landscaper piece is showing:

| Keys | Effect |
|---|---|
| `]` / `[` | Bigger / smaller on all axes |
| Alt + `]` / `[` | Wider / narrower (X) |
| Shift + `]` / `[` | Taller / shorter (Y) |
| Alt + Shift + `]` / `[` | Deeper / shallower (Z) |
| End | Reset size, tint and position |

Each step changes the size by 10% of its current value, so steps are fine near normal size and
quick at large sizes. Hold a scale key to keep resizing; after a short pause it repeats until you let go. Each axis goes
from 0.01x to 20x by default; narrow it with `MinScale` and `MaxScale`. The current scale is shown mid-screen. It stays set while you place
more of the same piece, and resets to normal size whenever you select a different piece (including
switching back to one you scaled earlier). Each placed piece saves its size with the world.
X and Z follow the piece's own orientation, so they swap roles as you rotate it.

## Copying World Objects

With the Hammer, Cultivator or Hoe out, aim at a rock, tree, bush, statue or other object and use
Valheim's copy shortcut (Shift + middle click by default). Landscaper selects the matching piece, facing
the same way and at the same size as the object, ready to place. Copying a tinted Landscaper piece
copies its tint too.

- It works whichever of the three tools you have out. If the object belongs to another tool, that
  tool is equipped from your inventory and the piece selected. Without that tool, the host (or a
  single player) gets the object added to the current tool's **Copied** tab instead; other players
  are told which tool they need.
- If it isn't in the catalog at all, it's added to the current tool's **Copied** tab and saved as a
  custom entry, so it's still there after a restart. Only the host (or a single player) can do this: a
  host deletes saved objects it doesn't have a piece for, so other players can only copy objects that
  are already in the catalog. As the host, new entries are sent to connected players straight away.
- Creatures, items, ships, carts and gravestones can't be copied. Some small wild plants such as
  mushrooms can't be aimed at to copy; once placed through Landscaper they can.

## Moving Pieces While Placing

While the placement preview of any Landscaper piece is showing, hold a modifier and scroll to nudge
the piece 0.1 m per scroll step:

| Keys | Effect |
|---|---|
| Alt + scroll | Up / down |
| Shift + scroll | Left / right |
| Alt + Shift + scroll | Away from / toward you |

Left/right and away/toward follow the direction you're facing, so they match the screen. This is
useful for lining pieces up exactly, such as stacking one staircase on another without a gap. The
piece doesn't rotate while one of these modifiers is held. Shift is also Valheim's "place without
snapping" key, so snapping is off while you nudge sideways or forward and back. The offset is shown mid-screen, stays set while you place
more of the same piece, and resets when you select a different piece or press End.

Building pieces that need support (for example the Ashlands ruin walls) can still break if moved off
the ground with nothing under them, unless they're placed as indestructible.

## Indestructible Pieces

Press `\` while building, with any tool and any piece selected, to turn indestructible placement on
or off; the key hints show whether it's on. It stays on until you turn it off. Every piece placed
while it's on never breaks, vanilla building pieces included: they ignore lack of support, weather,
raids and attacks, so they can hang in the air or stand on nothing. Trees, rocks and saplings placed
this way can't be chopped, mined or broken. Pieces can still be removed with the remove button (or
`landscaper_remove` for Landscaper pieces), and berries and crops can still be picked.

Each piece remembers whether it was placed as indestructible, so it survives reloads and applies for
every player. A host can turn the feature off for everyone with `Indestructible.Allowed`; pieces
already placed as indestructible stay that way.

## Tinting Pieces While Placing

While the placement preview of any Landscaper piece is showing:

| Keys | Effect |
|---|---|
| `,` / `.` | Previous / next preset |
| Alt + `,` / `.` | Hue backward / forward round the colour wheel (red, yellow, green, cyan, blue, magenta) |
| Shift + `,` / `.` | Weaker / stronger tint |
| Alt + Shift + `,` / `.` | Darker / lighter |
| End | Reset size, tint and position |

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
(`Wood`, `Stone`, `Resin`, ...), e.g. `Stone:10`; entries without requirements get the automatic
cost. Scale is
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
