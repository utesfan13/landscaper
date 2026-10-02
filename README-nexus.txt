[size=6][b]Landscaper[/b][/size]

I built this mod for my wife and our love of creating cozy spaces in this game. The mod adds hundreds of Valheim's own trees, rocks, plants, ruins, props and decorations as buildable pieces, and each piece can be resized, tinted, tilted and nudged during placement.

[img]https://raw.githubusercontent.com/utesfan13/landscaper/main/docs/images/cabin-interior.jpg[/img]

[size=5][b]Features[/b][/size]

[list]
[*][b]Tons of new pieces[/b]: Adds 150+ buildable prefabs from objects you see out in the world: rocks, bushes, stone walls, spider webs, and many more.
[*][b]Adjust each piece to fit your vibe[/b]: recolor (tint), resize, rotate (on 3 axes), and nudge items to get each piece to fit perfectly.
[*][b]Place pieces without restrictions[/b]: Place pieces where you normally wouldn't be able to, such as planting seeds without cultivated ground or outside of their normal biomes.
[*][b]Add pond water[/b]: Make a pond for your base! Note: Uses tar pit mechanics which can be a bit finicky. Water can be removed with middle click.
[*][b]Indestructible[/b]: place indestructible pieces; protect your wood from the rain or build a floating castle.
[*][b]Somewhat survival friendly[/b]: all pieces are given a reasonable cost, and unlocked just like vanilla pieces. May be game breaking in some ways (especially indestructible) so use with caution.
[/list]

[img]https://raw.githubusercontent.com/utesfan13/landscaper/main/docs/images/pond-scene.jpg[/img]

[img]https://raw.githubusercontent.com/utesfan13/landscaper/main/docs/images/crates-resized-tinted.jpg[/img]

[size=4][b]Some of the new pieces[/b][/size]

[img]https://raw.githubusercontent.com/utesfan13/landscaper/main/docs/images/cultivator-pieces.jpg[/img]

[img]https://raw.githubusercontent.com/utesfan13/landscaper/main/docs/images/hoe-pieces.jpg[/img]

[img]https://raw.githubusercontent.com/utesfan13/landscaper/main/docs/images/hammer-pieces.jpg[/img]

[size=5][b]Installation[/b][/size]

First, please note that the mod is still in beta, so use at your own risk.

Requires [b]BepInExPack Valheim[/b] and [b]Jotunn[/b] (see Requirements). Install with Vortex, or by hand: extract the zip into your Valheim folder, so that Landscaper.dll ends up in BepInEx/plugins/Landscaper. Every player needs the mod, at the same minor version. A dedicated server doesn't need it (see Dedicated Servers), but if it has it, it needs the same minor version too.

[size=5][b]Controls[/b][/size]

While placing a piece:

[list]
[*][b][ / ][/b]: Smaller / bigger. Hold Alt, Shift or both to change only the width, height or depth.
[*][b], / .[/b]: Previous / next tint preset. Fine tune hue/strength/brightness with Alt/Shift/Alt+Shift.
[*][b]Scroll[/b]: Rotate, as usual.
[*][b]Alt / Shift / Alt + Shift + Scroll[/b]: Tilt forward/back, tilt sideways, fine tune rotation when in [b]rotation[/b] mode, OR up and down/side to side/toward and away in [b]move[/b] mode.
[*][b]/[/b]: Switch between rotation and move mode.
[*][b]End[/b]: Reset size, tint, tilt and position.
[*][b]\[/b]: Indestructible placement on / off.
[*][b]Middle click[/b]: Remove a piece, including pond water.
[*][b]Shift + middle click[/b]: Copy the object you're aiming at, with its size, tint, rotation, and height.
[/list]

Keys can be changed in the config.

[size=5][b]Console Commands[/b][/size]

Open the console with F5:

[list]
[*][b]landscaper_remove [radius=10][/b]: remove your Landscaper pieces around you (if admin: everyone's)
[*][b]landscaper_costs [on|off][/b]: turn build costs off or on for every piece (host or admin)
[/list]

[size=5][b]Server Settings[/b][/size]

Settings are in BepInEx/config/utesfan13.landscaper.cfg, each with a description. Costs, indestructible pieces, relaxed placement (Placement.IgnoreRules) and the tool menus are synced from the server, so only the server's config matters for those.

On a public server, consider turning off Placement.IgnoreRules (placing pieces in no-build areas) and Indestructible.Allowed, and turning on General.DecorativeOnly so placed trees and rocks can't be chopped or mined.

[size=5][b]Dedicated Servers[/b][/size]

A dedicated server can run without Landscaper; only the players need it. Without the mod on the server, nothing is synced, so each player's own settings are used: agree on the same Costs, Indestructible and General.DecorativeOnly settings.

[quote][b][color=#ff6060]Warning: without the mod, the server can't check that players have it.[/color][/b] A player without Landscaper can join and sees your pieces as the plain game objects they're made from, at normal size and colour. [b]While they're the only one near your pieces, their game can make them fall, wear them down or break them (indestructible pieces too), and grow saplings.[/b] Nothing is lost when only players with the mod visit. If you can't be sure every player has it, install the mod on the server too.[/quote]

[size=5][b]Uninstalling[/b][/size]

Placed pieces are saved as the game objects they're made from, so a world loaded without Landscaper keeps them as ordinary objects (normal size and colour); reinstall and they're Landscaper pieces again. Cattails and lily pads are the exception: they're deleted without the mod. Back up your world first.

[size=5][b]Links[/b][/size]

Source code and bug reports: [url=https://github.com/utesfan13/landscaper]github.com/utesfan13/landscaper[/url]
