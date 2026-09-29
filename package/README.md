# Landscaper

Build with the world itself. Landscaper adds hundreds of Valheim's own trees, rocks, plants, ruins,
props and decorations as buildable pieces on the Cultivator, Hoe and Hammer, and gives you tools to
make each one fit: resize, tint, tilt and nudge pieces while you place them.

## Features

- **Hundreds of pieces**: trees, stumps, logs, bushes, flowers, fully grown crops, cattails
  and lily pads on the Cultivator; rocks, cliffs, ore deposits, ice and pond water on the Hoe; ruins, statues, dungeon
  decor, Dvergr and Fuling pieces, shipwrecks and props on the Hammer.
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

Install with r2modman or Thunderstore Mod Manager, which also installs BepInExPack Valheim and Jotunn.

**Every player and the server need the mod**, at the same minor version (for example 0.15.x);
players without it can't join. Console players on crossplay can't use mods, so they can't join
either.

## Before You Uninstall

Landscaper's own pieces (the trees, rocks, ruins, props and so on from its tabs) only exist while the
mod is installed. **If a world is loaded without Landscaper, by a single player or a server, every one
of them is permanently deleted.** Vanilla build pieces are safe: they just go back to normal size and
colour. Back up the world before uninstalling. Turning the mod off in its settings (`General.Enabled`) is safe: it only hides
the pieces.

## Controls

While placing a Landscaper piece (every key can be changed in the config):

| Keys | Effect |
|---|---|
| `]` / `[` | Bigger / smaller; hold Alt, Shift or both for width, height or depth only |
| `,` / `.` | Previous / next tint preset; hold Alt for hue, Shift for strength, both for brightness |
| Scroll | Rotate, as usual |
| Alt / Shift / both + scroll | Tilt forward, tilt sideways, fine spin |
| `/` | Switch Alt/Shift + scroll between tilting and moving (up, sideways, forward) |
| End | Reset size, tint, position and tilt |
| `\` | Indestructible placement on / off (any piece) |
| Middle click | Remove a Landscaper piece, also with the Cultivator and Hoe |
| Shift + middle click | Copy the object you're aiming at |

The build hints show these while a piece that can be adjusted is selected: any Landscaper piece and
any vanilla build piece apart from ships and carts.

Console commands (F5):

- `landscaper_find <keyword>` lists prefab names, for adding your own pieces in the config.
- `landscaper_remove [radius]` removes your Landscaper pieces around you (admins: anyone's), never
  inside a ward you can't access.
- `landscaper_check` checks the pieces against your version of Valheim, useful after a game update.

## Settings

The config file is `BepInEx/config/utesfan13.landscaper.cfg`. Settings synced from the server (only
admins can change them in game):

| Setting | Default | Effect |
|---|---|---|
| `Costs.Enabled` / `Costs.Multiplier` | on / 1 | Build costs, and how expensive they are |
| `Placement.IgnoreRules` | on | Place any piece where Valheim normally refuses |
| `Indestructible.Allowed` | on | Allow indestructible placement |
| `General.DecorativeOnly` | off | Placed trees, rocks and plants can't be chopped, mined or picked |
| `Tools.*DecorEnabled` | on | List pieces in the Cultivator, Hoe and Hammer menus |
| `CustomEntries.Entries` | empty | Add your own pieces from any prefab |

Keys, steps, tint presets and scale limits are personal settings.

## Recommended Settings for Public Servers

The defaults suit single player and groups of friends. On a public server, consider:

- Turning off `Placement.IgnoreRules`, which lets pieces be placed in no-build areas, such as boss
  altars and dungeon entrances.
- Turning off `Indestructible.Allowed`: together with relaxed placement, anyone could block a path or
  a portal with pieces nobody can break.
- Turning on `General.DecorativeOnly`, so placed trees, rocks and plants can't be chopped, mined or
  picked.

Functional furniture is cheaper than its vanilla counterpart: a Dvergr crate gives chest storage for
a few wood, and the Ashwood bed prop gives an Ashwood bed's comfort without Ashlands materials.

## Known Conflicts

Landscaper changes how pieces are placed, so it can clash with other building mods, particularly
Gizmo (both use the scroll wheel and rotation while placing), InfinityHammer, PlanBuild and
Valheim Plus's building options. Every Landscaper key can be changed in the config if another mod
uses the same one.

Controllers aren't supported yet; Steam Input can map controller buttons to the keys above.

## More

Full documentation, including costs, pond water, custom pieces and building from source:
https://github.com/utesfan13/landscaper

Bug reports and suggestions are welcome as GitHub issues. Please include your Landscaper and Valheim
versions and `BepInEx/LogOutput.log`.
