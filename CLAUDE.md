# Landscaper

A Valheim mod (BepInEx + Jotunn, C# net472): hundreds of world objects as buildable pieces on the
Cultivator, Hoe and Hammer, and size/tint/tilt/nudge for almost every piece while placing.

## Build and install

- Build after every change: `dotnet build -c Release`
- Install only when the game is closed (it locks the DLL):
  `tasklist | grep -i "^valheim.exe" || dotnet build -c Release -p:DeployToGame=true`
  A running `valheim_server.exe` doesn't count; only `valheim.exe` blocks the copy.
- Game paths come from `Landscaper.csproj` (`ValheimDir`, `BepInExDir`); override in `Local.props`.
- The game's code is easiest checked by decompiling with `ilspycmd` (assembly_valheim.dll in
  `valheim_Data\Managed`). The dedicated server ships its own assembly_valheim.dll.

## Releasing

- Version lives only in `Plugin.ModVersion`. Bump minor for new prefabs or anything that affects
  multiplayer (NetworkCompatibility is ClientMustHaveMod with VersionStrictness.Minor), patch otherwise.
- Add a `CHANGELOG.md` entry for every user-visible change.
- `dotnet build -c Release -t:Package` writes `bin\Landscaper-<v>.zip` and copies it to
  `release\` (replacing the old one). Commit that zip with the version: publishing a GitHub release
  tagged `v<version>` makes `.github/workflows/thunderstore.yml` upload it to Thunderstore.
- `dotnet build -c Release -t:PackageNexus` writes `bin\Landscaper-<v>-nexus.zip` (uploaded by hand).

## Working with the user

- Commit only when asked. Push to `publish-prep`; the user merges PRs into `main` themselves.
  Commit messages end with the Co-Authored-By line.
- `README.md` is the user's own wording, especially Features: don't reword it unless asked.
  `package/README.md` must stay an exact copy, and `README-nexus.txt` is the same content in BBCode;
  update all three together. Check `git status`/`git diff` first: the user may have unsaved or
  uncommitted README edits.

## Design notes

- Placed Landscaper pieces are saved under the game object they're made from, tagged with the
  Landscaper piece (`SavedPieces`), so uninstalling (or a server without the mod) keeps them as
  ordinary objects. Size, tint and other data live in the ZDO.
- Anything a game without the mod should respect has to be in vanilla saved data: protection uses a
  huge saved health (`SavedHealth`), forever saplings a far-future planting time (`SavedPieces`).
- Vanilla build pieces are made adjustable in place (`DecorativePieceManager.VanillaPieces.cs`);
  duplicate vanilla entries aren't registered.
- Costs: `BuildCosts` (biome materials, overrides, stations); refunds on breaking: `BreakRefund`.
- Ground tools (TerrainOp) are sent to the terrain's owner by prefab name only, so resized ones go
  through their own RPC (`TerrainBrushes`).
- Only the owner of a ZDO may change it; patches that write saved data check `IsOwner()`.
- In-game check: console command `landscaper_check` reports missing prefabs, templates and items.
