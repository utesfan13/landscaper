# Changelog

## 0.18.0
- Removed the Sunken Crypt Tower Wall, which was invisible when placed. Ones already placed can still
  be removed.
- Fixed a "new piece" message repeating on every item pickup when another mod has a piece with the
  same name as a Landscaper piece (such as a "Dvergr Banner").

## 0.17.3
- Pond water no longer turns white like snow in the mountains and in snowy weather.
- Webs are placed with the bottom of the web itself exactly at the crosshair (measured from the web's
  strands, since the horizontal webs are sheets turned 45 degrees inside a much taller box), and their
  icon is the web texture, since the strands don't show in a rendered icon.

## 0.17.2
- The web pieces are placed around the crosshair instead of starting at the far edge of their model
  (often off screen), and their icons are zoomed in so the strands show. Webs placed earlier appear
  moved by the same amount; re-place any that end up in the wrong spot.
- Removed the Dragon Egg Cup: the game keeps its model hidden (it only holds the egg in eagle nests),
  so a placed one was invisible. Ones already placed can still be removed.

## 0.17.1
- Icons of pieces whose model sits away from its origin, such as the big Jotun statue horns, are
  framed to fill the icon instead of coming out tiny.

## 0.17.0
- Pieces cost the wood, stone and bone of their biome: core wood for the Black Forest, ancient bark
  for the Swamp, obsidian for the Mountains, fine wood for the Plains, Yggdrasil wood and black marble
  for the Mistlands, ashwood, grausten and charred bone for the Ashlands, frostwood for the Deep North.
- Pieces need a crafting station like vanilla ones, the one that fits their material: stonecutter
  (stone, obsidian, black marble, grausten, ore rocks; 2 m or larger, the workbench below that), forge (bronze, iron; black forge for Mistlands
  and later metal) or workbench (everything else). Cultivator pieces (trees, plants, crops) need none.
- Ore rocks cost their ore and scrap piles scrap iron, and never mine for more than they cost; Jotun
  stonework, statues and the Morkborg gate cost grausten; cloth hangings, the Fuling banner and Jotun
  rugs red jute (and 2 more on Jotun and charred banners); Fuling walls, stairs and ladder 2 linen thread on top; the Fuling straw pile 2 flax and 2 barley; the webs 2 resin and 2 linen thread; the infested growths resin plus 1 black marble; the Jotun training dummies plain wood; no resin for the Mead Cauldron (Prop); Fuling roofs lox pelts; Fuling roofs lox pelts by size on top; the Jotun bridge fine wood; the Jotun rubble pile
  frostwood; Bog Witch rugs their fur; trophy stands their trophy.
- Lily pads rest on the water: the preview snaps to the surface (ponds included), and placed ones
  rise and fall with the waves and follow a pond's level. Nudging up or down sets their height above
  the surface.
- `landscaper_costs [on|off]` console command (host or admin): build costs off or on for every piece,
  vanilla ones included.
- Pieces unlock like vanilla ones: they show up once you've found all their materials, with one
  message saying how many unlocked. Pieces earlier versions unlocked straight away are locked again
  until then.

## 0.16.0
- Uninstalling no longer deletes placed Landscaper pieces: each is saved as the game object it's made
  from, with a tag, so without the mod it loads as that ordinary object and with it (reinstalled) as
  the Landscaper piece again, size and tint included. Pieces placed earlier are converted when they
  next load. Cattails and lily pads, built from ground clutter, still need the mod.
- Vanilla build pieces can be resized, tinted, tilted and nudged too (all but ships and carts), and
  copying one takes its size, tint and tilt. They stay ordinary vanilla pieces: same menu, cost and
  unlocks, and without the mod they're just normal size and colour again, not deleted.
- Landscaper no longer lists its own copies of vanilla build pieces (extra furniture, lights, roofs,
  stacks); the adjustable vanilla pieces replace them, and ones already placed are saved as the vanilla
  piece. Scaled variants, functional props, the turf roofs and festive pieces stay.
- `Placement.IgnoreRules` covers every piece, not just Landscaper's.
- New Crops tab on the Cultivator: fully grown carrots, turnips, onions, barley, flax and kale, and
  the seed stage of carrots, turnips, onions and kale.
- Pickables that are used up when picked (crops, loose stones, branches) cost exactly what picking
  them gives; ones that grow back still cost 5.
- Placed trees, rocks and plants stay where you put them. About 20 seconds after loading, the game
  snapped them to the terrain: sunken pieces popped up, and raised ones or ones placed on floors
  dropped down.
- The turf (grass) roof pieces had the thatch roof's icons, so they looked like thatch in the menu;
  they now show their own.

## 0.15.1
- Fixed the Old Pine Log, which failed to load properly once placed and couldn't be chopped. Logs
  already placed work again.
- Quieter log: only problems and a one-line summary are logged at startup.

## 0.15.0
- The mod's ID is now `utesfan13.landscaper`, so its config file is `BepInEx/config/utesfan13.landscaper.cfg`.
  To keep your settings from an earlier version, copy `landscaper.valheim.cfg` to that name. Every player
  needs 0.15.x to play together.

## 0.14.3
- `landscaper_remove` only removes your own pieces (admins and the host can remove anyone's), and never
  inside a ward you don't have access to.
- Removed the one-time migration from the mod's old config file name.

## 0.14.2
- Pieces moved to another tool keep their placed copies, and catalog entries can list former names.
- New `landscaper_check` console command to check the catalog after a Valheim update.
- Build settings for other install folders and mod manager profiles.

## 0.14.1
- Turning the mod off (`General.Enabled`) no longer deletes placed pieces from a hosted world.
- Pieces that can't be registered are kept as invisible stand-ins instead of being deleted.
- A custom entry's rotation sets the piece's starting tilt.
- Pouring pond water into an existing pond adds the right amount.
- Monster spawners and boss altars can't be copied.

## 0.14.0
- Breaking a placed piece gives back its build cost instead of better resources (ore, fine wood,
  loot); rocks and trees bought with stone or wood still drop stone or wood.
- Lower per-frame work while building, and lighter tint checks on placed pieces.

## 0.13.0
- `Placement.IgnoreRules`: place Landscaper pieces where Valheim normally refuses, except over players,
  inside other players' wards, or with nothing aimed at. Hoe and Cultivator pieces can be placed on
  floors and objects.
- Scroll modes: `/` switches Alt/Shift + scroll between tilting (the default) and moving.
- Copying an object takes its exact rotation.

## 0.12.0
- Functional furniture: prop chests, chairs, benches and beds work like the vanilla pieces they look like.
- Cost fixes for wooden pieces in stone tabs, pots, cauldrons and guck sacks.

## 0.11.0
- 123 more decor pieces: roofs, lights, rugs, festive pieces, stacks and piles, and more furniture.

## 0.10.0
- Pond water for the Hoe, tree sway that scales with a tree's size, and cheaper loose stones and branches.

## Earlier versions
- Hundreds of trees, rocks, plants, ruins and props on the Cultivator, Hoe and Hammer.
- In-game scaling, tinting, nudging, removal, copying of world objects, build costs and indestructible
  placement, with a multiplayer version check and settings synced from the server.
