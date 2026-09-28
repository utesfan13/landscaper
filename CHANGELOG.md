# Changelog

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
