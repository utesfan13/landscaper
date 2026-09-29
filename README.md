# Landscaper

Build with the world itself. Landscaper adds hundreds of Valheim's own trees, rocks, plants, ruins,
props and decorations as buildable pieces on the Cultivator, Hoe and Hammer, and gives you tools to
make each one fit: resize, tint, tilt and nudge pieces while you place them.

## Features

- **Hundreds of pieces**: trees, stumps, logs, bushes, flowers, fully grown crops, cattails
  and lily pads on the Cultivator; rocks, cliffs, ore deposits, ice and pond water on the Hoe; ruins, statues, dungeon
  decor, furniture, roofs, lights and props on the Hammer.
- **Vanilla build pieces too**: walls, floors, roofs, furniture and the rest can be resized, tinted,
  tilted and nudged, with nothing else about them changed. They stay ordinary vanilla pieces, so
  without the mod they're just normal size and colour again, not deleted.
- **Resize** any piece on each axis, **tint** it with presets or any colour, **tilt** it, and
  **nudge** it up, sideways or forward, all while placing.
- **Copy** an object in the world (Shift + middle click) to place more of it, with the same size,
  tint and rotation.
- **Functional furniture**: prop chests, chairs, benches and beds work like the vanilla pieces they
  look like.
- **Place anywhere**: pieces can be placed where Valheim normally refuses, and can be made
  **indestructible**.
- **Fair costs**: a little wood, stone or the matching material, with vanilla costs for vanilla
  build pieces. Removing a piece refunds it, and breaking one never gives back more valuable
  resources than it cost.
- **Multiplayer ready**: sizes, tints and rotations are saved on each piece, and server settings are
  synced to every player.

## Installation

Install with r2modman or Thunderstore Mod Manager, which also installs the requirements. To install by
hand: install [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/)
and [Jotunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/), then copy `Landscaper.dll`
into `BepInEx/plugins/Landscaper`.

**Every player and the server need the mod**, at the same minor version (for example 0.15.x);
players without it can't join. Console players on crossplay can't use mods, so they can't join
either.

## Before You Uninstall

Placed pieces only exist while the mod is installed. **If a world is loaded without Landscaper, by a
single player or a server, every Landscaper piece in it is permanently deleted.** Back up the world
before uninstalling or updating in a way that removes the mod. Turning the mod off in its settings
(`General.Enabled`) is safe: it only hides the pieces.

## Recommended Settings for Public Servers

The defaults suit single player and groups of friends. On a public server, consider:

- Turning off `Placement.IgnoreRules`, which lets pieces be placed in no-build areas, such as boss
  altars and dungeon entrances.
- Turning off `Indestructible.Allowed`: together with relaxed placement, anyone could block a path or
  a portal with pieces nobody can break.
- Turning on `General.DecorativeOnly`, so placed trees, rocks and plants can't be chopped, mined or
  picked.

These settings are synced from the server, so only the server's config matters.

Functional furniture is cheaper than its vanilla counterpart: a Dvergr crate gives chest storage for
a few wood, and the Ashwood bed prop gives an Ashwood bed's comfort without Ashlands materials.

## Known Conflicts

Landscaper changes how pieces are placed, so it can clash with other building mods, particularly
Gizmo (both use the scroll wheel and rotation while placing), InfinityHammer, PlanBuild and
Valheim Plus's building options. Every Landscaper key can be changed in the config if another mod
uses the same one.

## Settings

In `BepInEx/config/utesfan13.landscaper.cfg`. Settings marked **synced** come from the server in
multiplayer (see [Multiplayer](#multiplayer)) and take effect without a restart.

| Setting | Default | Effect |
|---|---|---|
| `General.Enabled` | `true` | Master toggle. Off hides the pieces from the menus and turns off the building controls; placed pieces stay in the world. |
| `Costs.Enabled` | `true` | **Synced.** Charge a small cost to place pieces; see [Build Costs](#build-costs). |
| `Costs.Multiplier` | `1` | **Synced.** Multiplies the automatic costs (0.25 to 4). |
| `Indestructible.Allowed` | `true` | **Synced.** Allow placing indestructible pieces; see [Indestructible Pieces](#indestructible-pieces). |
| `Indestructible.ToggleKey` | `Backslash` | Turn indestructible placement on or off while building. |
| `General.DecorativeOnly` | `false` | **Synced.** When `true`, placed trees, rocks and plants can't be chopped, mined or picked. Remove them with the remove button (middle click). |
| `Placement.IgnoreRules` | `true` | **Synced.** Place any piece, vanilla build pieces included, where Valheim normally refuses (clipping into other pieces, no support, wrong biome, inside dungeons, steep ground, ...), and Hoe and Cultivator pieces on floors, rocks and other objects as well as the ground. Overlapping a player or creature and other players' wards still block placing. |
| `Tools.CultivatorDecorEnabled` / `HoeDecorEnabled` / `HammerDecorEnabled` | `true` | **Synced.** List pieces in that tool's menu. Turning one off only hides its pieces; ones already placed stay in the world. |
| `CustomEntries.Entries` | empty | **Synced.** Extra pieces; see below. |
| `Scaling.ScaleUpKey` / `ScaleDownKey` | `]` / `[` | Resize the piece being placed. |
| `Scaling.XAxisModifier` / `YAxisModifier` | `LeftAlt` / `LeftShift` | Hold with the scale keys to change X or Y; hold both for Z. Either side of the keyboard works. |
| `Scaling.ScaleResetKey` | `End` | Reset the size, colour, position and tilt of the piece being placed. |
| `Scaling.ScaleStep` | `0.1` | How much each key press changes the scale, as a fraction of the current size (0.1 = 10%). |
| `Scaling.ScaleRepeatRate` | `15` | Steps per second while a scale key is held down. |
| `Scaling.MinScale` / `MaxScale` | `0.01` / `20` | Smallest and largest scale allowed on any axis (0.01 to 1, and 1 to 20). |
| `Tint.TintBackKey` / `TintForwardKey` | `,` / `.` | Step through the tint presets for the piece being placed. |
| `Tint.HueModifier` / `StrengthModifier` | `LeftAlt` / `LeftShift` | Hold with the tint keys to fine-tune hue or strength; hold both for brightness. |
| `Tint.HueStep` | `10` | Degrees round the colour wheel per key press. |
| `Tint.StrengthBrightnessStep` | `0.1` | How much each key press changes strength or brightness (0.1 = 10%). |
| `Tint.TintRepeatRate` | `15` | Steps per second while a tint key is held down. |
| `Tint.Presets` | 10 presets | `Name:hue,strength,brightness` entries separated by semicolons; see below. |
| `Offset.UpDownModifier` / `SideModifier` | `LeftAlt` / `LeftShift` | Hold and scroll to tilt the piece being placed forward/back or sideways, or spin it finely with both (rotate mode); or to move it up/down, sideways, or toward/away from you with both (move mode). |
| `Offset.ModeKey` | `Slash` (`/`) | Switch the modifiers' scrolling between rotate mode (the default) and move mode. |
| `Offset.Step` | `0.1` | Meters per scroll step in move mode. |
| `Offset.RotationStep` | `5` | Degrees per scroll step in rotate mode (0.5 to 45). |

## Build Costs

Placing a piece has a small cost, and removing it refunds the cost:

- **Vanilla build pieces** (chests, beds, banners, walls, floors, torches, ...) cost exactly what the
  vanilla piece costs and need the same crafting station nearby (workbench, forge, stonecutter, ...),
  so the scalable, tintable copy matches the original. `Costs.Multiplier` doesn't change these.
- **Pickables** that grow back (berry bushes, mushrooms, thistle, ...) cost only 5 of the item they
  give, e.g. a blueberry bush costs 5 blueberries. Ones that are used up when picked (fully grown
  crops, loose stones, fallen branches) cost exactly what picking them gives, e.g. a grown carrot
  costs 1 carrot, so placing and picking one breaks even.
- **Guck sacks** cost guck: 1 for the small one, 2 for the regular one.
- **Metal pieces** (lanterns, braziers, iron torches, sconces, chains, the iron gate, iron floors and
  walls, ...) cost 1 iron.
- **Everything else** costs wood or stone depending on its size:

| Largest dimension of the piece | Cost |
|---|---|
| under 2 m (stools, small rocks) | 2 |
| 2 to 6 m (benches, boulders) | 4 |
| 6 to 15 m (trees, large statues) | 6 |
| 15 m and over (cliffs, great pillars, frozen ships) | 8 |

  Trees, plants, stumps, furniture and wooden props cost wood; rocks, cliffs, statues and ruins
  cost stone, except for the wooden and cloth pieces among them (tree stumps, the wooden path, the
  trader wagon, Jotun benches and rugs, ...), which cost wood; clay pots cost stone and the prop
  cauldrons iron. Ice and snow pieces cost ice; bones, skulls and carcasses cost bone fragments. The
  stump hut and hole and the frozen ships cost wood and ice, split evenly. Building structures and
  copied objects go by their name (stone, crystal and so on cost stone).
- **Crafted light sources** (torches, lanterns, braziers, ...) cost 1 resin on top. Pickables and other
  natural pieces never do, even ones that glow.

Custom entries with their own requirements keep them. Resizing a piece while placing doesn't change
its cost.

Turn costs off with `Costs.Enabled`, or scale them with `Costs.Multiplier` (every cost stays at
least 1).

### Breaking placed pieces

Breaking a placed piece (chopping, mining or smashing it) never turns its cost into something better:

- If everything it naturally drops is what it cost, it drops as normal, bonus included. A rock bought
  with stone gives stone, and a beech bought with wood gives wood (plus resin and seeds, which are
  always fine).
- Otherwise it gives back exactly its build cost instead of its own drops once it's fully broken.
  A copper deposit bought with stone gives the stone back, not copper ore; a birch tree gives back
  wood, not fine wood, and disappears when felled rather than falling into logs; a crate or barrel
  gives back its wood rather than loot. Pieces that would drop nothing give back their cost too.
- Hammer building pieces give back their cost when they break, like vanilla ones.
- Picking isn't affected: planting a berry bush for 5 berries that keeps regrowing is intended.

With costs turned off, pieces cost nothing, so breaking them gives nothing. To stop placed trees,
rocks and plants from being chopped, mined or picked at all, turn on `DecorativeOnly`.

## Multiplayer

Every player, and a dedicated server, needs Landscaper (and Jotunn) installed at the same minor
version, e.g. any 0.15.x. Jotunn checks this when joining and refuses the connection with a message if
a player's version doesn't match. Players without the mod couldn't see Landscaper pieces anyway.

Scale and tint are saved on each placed piece, so every player sees them.

When joining a server, the synced settings (costs, `DecorativeOnly`, `Indestructible.Allowed`,
`Placement.IgnoreRules`, the three tool toggles and custom entries) are taken from the server, and only admins can change them in game. Keybinds, tint presets
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
remove your Landscaper pieces within that many meters of you (default 10, max 100). It never
removes anything that wasn't placed through this mod, or anything inside a ward you don't have
access to. Other players' pieces are only removed when an admin or the host runs it.

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
| End | Reset size, tint, position and tilt |

Each step changes the size by 10% of its current value, so steps are fine near normal size and
quick at large sizes. Hold a scale key to keep resizing; after a short pause it repeats until you let go. Each axis goes
from 0.01x to 20x by default; narrow it with `MinScale` and `MaxScale`. The current scale is shown mid-screen. It stays set while you place
more of the same piece, and resets to normal size whenever you select a different piece (including
switching back to one you scaled earlier). Each placed piece saves its size with the world.
X and Z follow the piece's own orientation, so they swap roles as you rotate it.

## Pond Water (experimental)

The Hoe has **Pond Water**, listed with its terrain tools right after paved road: Valheim's simulated liquid (the kind used for tar pits),
set to water. Placed on the ground it pours out a fixed amount of water that flows into and fills the
lowest ground around it, over an area of about 64 m. Dig a hollow with the Hoe or a pickaxe first,
then place the water in it. The placement preview is invisible, since the water only takes shape once
placed. Resizing doesn't change how much water it holds.

Pond water is free. Each placement pours 1 m³ of water; while Pond Water is selected, the scale keys
(`]` / `[`) change how much instead of resizing it, and the amount is shown mid-screen. The default is
set with `Water.PondVolume`. To fill a hollow, keep placing pond water inside the pond: the new water
is poured into that pond rather than starting a separate one, so the level rises each time. To remove a pond, aim at the water (or the ground under it) with the remove
button; the whole pond is removed.

## Functional Furniture

Some decorative copies work like the vanilla piece they resemble:

- **Chests:** Wardrobe (Prop), Barrel, Dvergr Barrel, Dvergr Crate, Dvergr Long Crate, Dvergr Ashlands
  Crate and Braided Box store items (no opening animation). They never contain loot.
- **Seats:** Darkwood Chair (Prop), Runed Bench (Prop), Dvergr Chair, Dvergr Stool, Mountain Chair,
  Jotun Stool and Jotun Bench can be sat on.
- **Beds:** Bed (Prop), Ashwood Bed (Prop), Dvergr Bed and Jotun Bedrolls can be slept in and set
  your spawn point.

They give the same comfort as the vanilla piece. Like vanilla chests, a chest with items in it can't
be removed, including with `landscaper_remove`.

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

## Tilting and Moving Pieces While Placing

While the placement preview of any Landscaper piece is showing, hold a modifier and scroll to tilt
or move it. Plain scrolling still spins the piece in Valheim's usual steps. Press `/` to switch the
modifiers between rotate mode (the default) and move mode; the build hints show the current mode.

| Keys | Rotate mode (5° per step) | Move mode (0.1 m per step) |
|---|---|---|
| Alt + scroll | Tilt forward / back | Up / down |
| Shift + scroll | Tilt left / right | Left / right |
| Alt + Shift + scroll | Fine spin | Away from / toward you |

The tilt is relative to the piece, so after tilting a tree to make it lean, plain scrolling turns
which way it leans. Moving left/right and away/toward follows the direction you're facing, so it
matches the screen. Moving is useful for lining pieces up exactly, such as stacking one staircase on
another without a gap. Valheim's own spin doesn't change while one of these modifiers is held. Shift
is also Valheim's "place without snapping" key, so snapping is off while you use it. The tilt and
offset are shown mid-screen, stay set while you place more of the same piece, and reset when you
select a different piece or press End; the mode stays as you left it. The game saves each piece's
rotation itself, so tilted pieces look the same for everyone. Copying an object (see
[Copying World Objects](#copying-world-objects)) takes its exact rotation, including any lean.

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
| End | Reset size, tint, position and tilt |

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

Add entries in `BepInEx/config/utesfan13.landscaper.cfg` under `[CustomEntries]`:

```text
Display Name|ExactPrefabName|Tool|Category|RotationX,RotationY,RotationZ|Item:Amount,Item:Amount|ScaleX,ScaleY,ScaleZ
```

BepInEx keeps each setting on a single line, so separate multiple entries with a literal `\n`:

```ini
Entries = Giant Oak|Oak1|Cultivator|Trees|||2\nFlat Boulder|Rock_destructible|Hoe|Rocks|||1.5,0.5,1.5
```

Valid tools are `Cultivator`, `Hoe`, and `Hammer`. Rotation, requirements and scale are optional;
leave a field empty to skip it, as in the example above. Rotation is the tilt the piece starts with
when selected, in degrees (forward, spin, sideways); you can still tilt it further while placing, and
End goes back to it. Requirements use item prefab names
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
`E("Giant Oak", "Oak1", scale: new Vector3(2f, 2f, 2f))`. Rebuild (see [Building](#building)).

Placed pieces are saved under `Landscaper_<prefab>_<tool>`, plus the display name for scaled
variants. Moving a piece to a different tool is safe: every piece is also registered under its names
for the other tools, so copies placed with the old tool load as the moved piece. Renaming a scaled
variant, or switching a piece to a renamed vanilla prefab, changes its saved name; list the old name
so placed copies keep loading:
`E("Huge Oak", "Oak1", scale: new Vector3(2f, 2f, 2f), formerNames: new[] { "Landscaper_Oak1_Cultivator_GiantOak" })`.

## Checking After a Valheim Update

Game updates occasionally rename or remove prefabs. A piece whose prefab is gone can't be placed,
and its placed copies are kept as invisible stand-ins (so a host doesn't delete them) until it's
fixed. After an update, load a world, open the console (F5) and run:

```
landscaper_check
```

It lists catalog pieces whose prefab no longer exists, missing furniture templates, cost rules that
refer to pieces or items that don't exist, and pieces currently kept as stand-ins. The same lines go
to the BepInEx log, and a warning is logged at startup whenever there are stand-ins. To fix a
missing prefab, find its new name with `landscaper_find` and update the entry, listing the old saved
name in `formerNames`.

## Building

Requires the .NET SDK. From the repository folder:

```
dotnet build -c Release
```

The DLL is written to `bin/Release/Landscaper.dll`; copy it to `BepInEx/plugins/Landscaper`. The
project looks for Valheim in the default Steam folder. If yours is elsewhere, or you use a mod manager
profile, create a `Local.props` file next to `Landscaper.csproj` (it's git-ignored):

```xml
<Project>
  <PropertyGroup>
    <ValheimDir>D:\Games\Valheim</ValheimDir>
    <!-- Optional: a mod manager profile's BepInEx folder instead of the one in ValheimDir. -->
    <BepInExDir>C:\Users\you\AppData\Roaming\r2modmanPlus-local\Valheim\profiles\Default\BepInEx</BepInExDir>
    <!-- Optional: copy the DLL into BepInEx/plugins/Landscaper after every build. -->
    <DeployToGame>true</DeployToGame>
  </PropertyGroup>
</Project>
```

The same properties work on the command line, e.g. `dotnet build -c Release -p:DeployToGame=true`.
The copy fails while Valheim is running, because the game locks the DLL.

### Packaging a release

```
dotnet build -c Release -t:Package
```

This writes `bin/Landscaper-<version>.zip`, ready to upload to Thunderstore or Nexus Mods. It contains
`manifest.json` (from `package/manifest.template.json`), `icon.png` (`package/icon.png`, a 256x256 PNG
you provide), `README.md` (`package/README.md`, a shorter player-facing page; keep it in step with
this one), `CHANGELOG.md` and `plugins/Landscaper.dll`. The version comes from
`ModVersion` in `Plugin.cs`, the only place it's set; update `CHANGELOG.md` for each release.
