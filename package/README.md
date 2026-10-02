# Landscaper

I built this mod for my wife and our love of creating cozy spaces in this game. The mod adds hundreds of Valheim's own trees, rocks, plants, ruins, props and decorations as buildable pieces, and each piece can be resized, tinted, tilted and nudged during placement.

![A cabin furnished and decorated with Landscaper pieces](https://raw.githubusercontent.com/utesfan13/landscaper/main/docs/images/cabin-interior.jpg)

## Features

- **Tons of new pieces**: Adds 150+ buildable prefabs from objects you see out in the world: rocks, bushes, stone walls, spider webs, and many more. 
- **Adjust each piece to fit your vibe**: recolor (tint), resize, rotate (on 3 axes), and nudge items to get each piece to fit perfectly. 
- **Place pieces without restrictions**: Place pieces where you normally wouldn't be able to, such as planting seeds without cultivated ground or outside of their normal biomes.
- **Add pond water**: Make a pond for your base! Note: Uses tar pit mechanics which can be a bit finicky. Water can be removed with middle click.
- **Indestructible**: place indestructible pieces; protect your wood from the rain or build a floating castle.
- **Somewhat survival friendly**: all pieces are given a reasonable cost, and unlocked just like vanilla pieces. May be game breaking in some ways (especially indestructible) so use with caution. 

![A pond with lily pads, cattails and a campfire](https://raw.githubusercontent.com/utesfan13/landscaper/main/docs/images/pond-scene.jpg)

![Crates resized, tinted and tilted](https://raw.githubusercontent.com/utesfan13/landscaper/main/docs/images/crates-resized-tinted.jpg)

### Some of the new pieces

![Cultivator pieces: trees, plants and crops](https://raw.githubusercontent.com/utesfan13/landscaper/main/docs/images/cultivator-pieces.jpg)

![Hoe pieces: rocks, ice and pond water](https://raw.githubusercontent.com/utesfan13/landscaper/main/docs/images/hoe-pieces.jpg)

![Hammer pieces: props, Dvergr pieces and furniture](https://raw.githubusercontent.com/utesfan13/landscaper/main/docs/images/hammer-pieces.jpg)

## Installation

First, please note that the mod is still in beta, so use at your own risk. 

Install with r2modman or Thunderstore Mod Manager, which also installs BepInExPack Valheim and Jotunn.
By hand: install [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/)
and [Jotunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/), then copy `Landscaper.dll` into
`BepInEx/plugins/Landscaper`. Every player needs the mod, at the same minor version. A dedicated
server doesn't need it (see Dedicated Servers), but if it has it, it needs the same minor version too.

## Controls

While placing a piece:

| Keys | Effect |
|---|---|
| `[` / `]` | Smaller / bigger. Hold Alt, Shift or both to change only the width, height or depth. |
| `,` / `.` | Previous / next tint preset. Fine tune hue/strength/brightness with alt/shift/alt+shift |
| Scroll | Rotate, as usual |
| Scroll + <br> Alt / Shift / Alt + Shift | Tilt forward/back, tilt sideways, fine tune rotation when in **rotation** mode <br> **OR** up and down/side to side/toward and away in **move** mode. |
| `/` | Switch between rotation and move mode |
| End | Reset size, tint, tilt and position |
| `\` | Indestructible placement on / off |
| Middle click | Remove a piece, including pond water |
| Shift + middle click | Copy the object you're aiming at, with its size, tint, rotation, and height |

Keys can be changed in the config.

## Console Commands

Open the console with F5:

- `landscaper_remove [radius=10]`: remove your Landscaper pieces around you (if admin: everyone's)
- `landscaper_costs [on|off]`: turn build costs off or on for every piece (host or admin)

## Server Settings

Settings are in `BepInEx/config/utesfan13.landscaper.cfg`, each with a description. Costs,
indestructible pieces, relaxed placement (`Placement.IgnoreRules`) and the tool menus are synced from
the server, so only the server's config matters for those.

On a public server, consider turning off `Placement.IgnoreRules` (placing pieces in no-build areas)
and `Indestructible.Allowed`, and turning on `General.DecorativeOnly` so placed trees and rocks can't
be chopped or mined.

## Dedicated Servers

A dedicated server can run without Landscaper; only the players need it. Without the mod on the
server, nothing is synced, so each player's own settings are used: agree on the same `Costs`,
`Indestructible` and `General.DecorativeOnly` settings.

> **Warning: without the mod, the server can't check that players have it.** A player without
> Landscaper can join and sees your pieces as the plain game objects they're made from, at normal size
> and colour. **While they're the only one near your pieces, their game can make them fall, wear them
> down or break them (indestructible pieces too), and grow saplings.** Nothing is lost when only
> players with the mod visit. If you can't be sure every player has it, install the mod on the server too.

## Uninstalling

Placed pieces are saved as the game objects they're made from, so a world loaded without Landscaper
keeps them as ordinary objects (normal size and colour); reinstall and they're Landscaper pieces again.
Cattails and lily pads are the exception: they're deleted without the mod. Back up your world first.
