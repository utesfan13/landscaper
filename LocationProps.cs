using BepInEx.Logging;
using Jotunn.Managers;
using UnityEngine;

namespace Landscaper;

/// <summary>
/// Builds placeable prefabs from the parts of a location that aren't saved objects of their own,
/// such as the Bog Witch's hut, cauldron and table: the location draws them as part of itself, so
/// they can't be cloned like other pieces. Each becomes a networked prefab named
/// "LandscaperLocation_{name}" with the part's models, colliders and animation; the catalog then
/// refers to it like any vanilla prefab. A piece can also be a group, such as the table with the
/// herbs and bones on it, kept as the location arranges them. Every player's game builds the same
/// prefabs, so placed copies load for everyone, but like ground cover they're deleted from a world
/// loaded without Landscaper.
/// </summary>
internal static class LocationProps
{
    public const string Prefix = "LandscaperLocation_";

    /// <summary>
    /// A piece made from a location: the part named Anchor (the first of that name, ignoring the
    /// " (1)" Unity adds to copies), and with it every part named one of Members within Radius
    /// metres of it, where the location put them.
    /// </summary>
    private sealed class Part
    {
        public Part(string name, string anchor, float radius = 0f, params string[] members)
        {
            Name = name;
            Anchor = anchor;
            Radius = radius;
            Members = members;
        }

        public string Name { get; }
        public string Anchor { get; }
        public float Radius { get; }
        public string[] Members { get; }

        /// <summary>
        /// Keeps a single part at its size in the location rather than its own, for a model the
        /// location stretches into something else (a bucket into a trough).
        /// </summary>
        public bool KeepScale { get; set; }

        /// <summary>Red fabric made white, so a tint can make it any colour (see FabricWhitening).</summary>
        public bool WhiteFabric { get; set; }

        /// <summary>Sat and lain on like a divan (see DivanSeat).</summary>
        public bool Divan { get; set; }
    }

    private static Part Single(string name) => new(name, name);

    /// <summary>The pieces to make, by location.</summary>
    private static readonly (string Location, Part[] Parts)[] Sources =
    {
        ("BogWitch_Camp", new[]
        {
            Single("BogWitch_Hut"), Single("BogWitch_Cauldron"), Single("BogWitch_Table"), Single("BogWitch_Ladder"),
            Single("BogWitch_Stump1"), Single("BogWitch_Stump2"), Single("BogWitch_Talisman1"), Single("BogWitch_Talisman2"),
            Single("BogWitch_Talisman3"), Single("BogWitch_Thistles"), Single("BogWitch_Amulet"), Single("BogWitch_Leech"),
            Single("bogwitch_barrel"), Single("Menu_greydwarf_george"), Single("bzerkermushroom"),
            Single("garlic"), Single("piripiri"), Single("lovage"), Single("fi_vil_combs_props_bone_skull"),
            Single("fi_vil_combs_props_bonepile_01a"), Single("fi_vil_combs_props_bonepile_01b"),
            Single("fi_vil_combs_props_bonepile_01e"), Single("fi_vil_combs_props_bonepile_02b"),
            Single("fi_vil_combs_props_bonepile_04b"), Single("fi_vil_combs_props_bonepile_04d"),
            // Groups, as the camp has them.
            new("BogWitch_TableSet", "BogWitch_Table", 1.5f,
                "garlic", "lovage", "bzerkermushroom", "fi_vil_combs_props_bonepile_01a", "SpiceMountains", "fi_vil_combs_props_bone_skull"),
            new("BogWitch_MushroomBarrels", "bogwitch_barrel (1)", 2f,
                "bogwitch_barrel", "bzerkermushroom", "BogWitch_Stump1", "BogWitch_Stump2"),
            new("BogWitch_MushroomStump", "BogWitch_Stump1", 1.2f, "bzerkermushroom"),
            // The Bog Witch herself, without her trading.
            new("BogWitch_Figure", "BogWitch"),
            new("BogWitch_BonePiles", "fi_vil_combs_props_bonepile_04b", 2f, "fi_vil_combs_props_bonepile_01e",
                "fi_vil_combs_props_bonepile_02b", "fi_vil_combs_props_bonepile_04d")
        }),
        // The Bog Witch's broom: the creature's model and animation, without the creature.
        ("BogWitchKvastur", new[] { new Part("Kvastur", "Visual") }),
        // Haldor's camp: his tent, lamp and wares, and Haldor himself, without his trading.
        ("Vendor_BlackForest", new[]
        {
            new Part("Haldor_Figure", "Haldor"), Single("TraderTent"), Single("TraderChest_static"), Single("TraderRune"),
            new Part("TraderLamp", "fi_vil_light_lamp01_03", 1f, "Point light", "insects"), Single("rug_Bjorn"),
            Single("cpR_tablesmall01"), new Part("cpR_tablesmall01_plain", "cpR_tablesmall01 (1)"),
            Single("fi_vil_container_barrel_small"), Single("fi_vil_container_barrel_small_sweetpot"),
            Single("fi_vil_container_barrel_small_fish"), Single("fi_vil_container_barrel_big_fruit"),
            Single("fi_vil_container_barrel_big_empty"), Single("fi_vil_container_crate_small"),
            Single("fi_vil_container_crate_big_x4_01"), Single("fi_vil_container_basket01_coffee_lid"),
            Single("fi_vil_container_basket01_grain_lid"), Single("fi_vil_container_basket02_closed"),
            Single("fi_vil_container_keg"), Single("fi_vil_shield05_a"), Single("fi_vil_shield10_a"), Single("bandedshield"),
            // A bucket stretched into a trough, planted with carrots; and empty.
            new("CarrotPlanter", "fi_vil_container_bucket01", 1.5f, "carrot"),
            new("Trough", "fi_vil_container_bucket01") { KeepScale = true }
        }),
        // Hildir's camp: her tents, wagon, wares and the chests she pays for, her lox, and Hildir
        // herself, without her trading.
        ("Hildir_camp", new[]
        {
            new Part("Hildir_Figure", "Hildir"), Single("HildirsLox"), Single("hildir_tent"), Single("hildir_tent2"),
            Single("hildir_wagon"), Single("hildir_chest1"), Single("hildir_chest2"), Single("hildir_chest3"),
            Single("hildir_clothesrack1"), Single("hildir_clothesrack2"), Single("hildir_clothesrack3"),
            Single("hildir_fabricsroll1"), Single("hildir_fabricsroll2"), Single("hildir_table"), Single("hildir_table1"),
            Single("hildir_table2"), Single("hildir_hatstand"), Single("hildir_hatstand1"), Single("hildir_hatstand2"),
            Single("hildir_maptable"), new("hildir_divan", "hildir_divan") { Divan = true }, new("hildir_divan1", "hildir_divan1") { Divan = true },
            // White copies of the fabric pieces, to tint any colour.
            new("hildir_divan_white", "hildir_divan") { WhiteFabric = true, Divan = true },
            new("hildir_divan1_white", "hildir_divan1") { WhiteFabric = true, Divan = true },
            new("hildir_carpet_white", "hildir_carpet") { WhiteFabric = true },
            new("hildir_fabricsroll1_white", "hildir_fabricsroll1") { WhiteFabric = true },
            new("hildir_fabricsroll2_white", "hildir_fabricsroll2") { WhiteFabric = true },
            new("hildir_clothesrack1_white", "hildir_clothesrack1") { WhiteFabric = true },
            new("hildir_clothesrack2_white", "hildir_clothesrack2") { WhiteFabric = true },
            new("hildir_clothesrack3_white", "hildir_clothesrack3") { WhiteFabric = true },
            new("hildir_table1_white", "hildir_table1") { WhiteFabric = true },
            new("hildir_table2_white", "hildir_table2") { WhiteFabric = true }, Single("hildir_carpet"),
            Single("hildir_lantern"), Single("hildir_barrel"), Single("hildir_flowergirland"),
            // Hildir's trough, planted with turnips.
            new("TurnipPlanter", "fi_vil_container_bucket01", 1.5f, "turnip", "blast")
        })
    };

    private static bool _built;

    /// <summary>Builds the prefabs once. Call before registering pieces, from ZNetScene.Awake.</summary>
    public static void Build(ManualLogSource log)
    {
        if (_built)
        {
            return;
        }

        if (!AssetManager.Instance.IsReady())
        {
            log.LogWarning("The game's assets aren't ready yet; location pieces such as the Bog Witch's hut are left out.");
            return;
        }

        _built = true;
        foreach (var (locationName, parts) in Sources)
        {
            try
            {
                // A networked prefab (a creature) as it is; a location loaded and kept loaded (never
                // released), since the pieces use its meshes and materials.
                var location = PrefabManager.Instance.GetPrefab(locationName);
                if (location == null)
                {
                    var reference = AssetManager.Instance.GetSoftReference<GameObject>(locationName);
                    if (!reference.IsValid)
                    {
                        log.LogWarning($"Location '{locationName}' wasn't found; its pieces are left out.");
                        continue;
                    }

                    reference.Load();
                    location = reference.Asset;
                }
                if (location == null)
                {
                    log.LogWarning($"Location '{locationName}' couldn't be loaded; its pieces are left out.");
                    continue;
                }

                var all = location.GetComponentsInChildren<Transform>(includeInactive: true).Where(part => part != location.transform).ToList();
                foreach (var part in parts)
                {
                    // An exact name first (a group's anchor can be a particular copy), then the first copy.
                    var anchor = all.FirstOrDefault(candidate => candidate.name == part.Anchor) ??
                                 all.FirstOrDefault(candidate => BaseName(candidate.name) == part.Anchor);
                    if (anchor == null)
                    {
                        log.LogWarning($"'{part.Anchor}' wasn't found in location '{locationName}'.");
                        continue;
                    }

                    BuildPart(part, anchor, all);
                }
            }
            catch (Exception exception)
            {
                log.LogError($"Failed to make pieces from location '{locationName}': {exception}");
            }
        }
    }

    /// <summary>A name without the " (1)" Unity adds to copies.</summary>
    private static string BaseName(string name)
    {
        var bracket = name.IndexOf(" (", StringComparison.Ordinal);
        return bracket > 0 ? name.Substring(0, bracket) : name;
    }

    private static void BuildPart(Part part, Transform anchor, List<Transform> all)
    {
        var prefab = PrefabManager.Instance.CreateEmptyPrefab(Prefix + part.Name);

        // CreateEmptyPrefab makes a cube; keep only the transform and ZNetView.
        UnityEngine.Object.DestroyImmediate(prefab.GetComponent<Collider>());
        UnityEngine.Object.DestroyImmediate(prefab.GetComponent<MeshRenderer>());
        UnityEngine.Object.DestroyImmediate(prefab.GetComponent<MeshFilter>());

        if (part.Members.Length == 0 && part.KeepScale)
        {
            Copy(anchor, prefab.transform, Vector3.zero, Quaternion.identity, anchor.lossyScale);
        }
        else if (part.Members.Length == 0)
        {
            // At its own size and upright, wherever and however the location placed it.
            Copy(anchor, prefab.transform, Vector3.zero, Quaternion.identity, Vector3.one);
        }
        else
        {
            // The anchor stands upright at the middle, at its size in the location, with the others
            // where they are around it (on it, or next to it).
            var members = new HashSet<string>(part.Members);
            var origin = anchor.position;
            var turn = Quaternion.Inverse(anchor.rotation);
            var chosen = all.Where(candidate => candidate == anchor || members.Contains(BaseName(candidate.name)) &&
                    Horizontal(candidate.position - origin) <= part.Radius)
                .ToList();

            // Not a part inside another chosen one (a carrot's own "carrot" model): it's copied with it.
            foreach (var member in chosen.Where(candidate => !chosen.Any(other => other != candidate && IsWithin(candidate, other))))
            {
                Copy(member, prefab.transform, turn * (member.position - origin), turn * member.rotation, member.lossyScale);
            }
        }

        if (part.WhiteFabric)
        {
            FabricWhitening.Apply(prefab);
        }

        if (part.Divan)
        {
            // The two divans' models run opposite ways along their length, so the first is lain on the other way.
            prefab.AddComponent<DivanSeat>().FlipLie = part.Anchor == "hildir_divan";
        }

        PrefabManager.Instance.AddPrefab(prefab);
        PrefabManager.Instance.RegisterToZNetScene(prefab);
    }

    /// <summary>Whether a part is inside another (already copied with it).</summary>
    private static bool IsWithin(Transform part, Transform other)
    {
        for (var current = part.parent; current != null; current = current.parent)
        {
            if (current == other)
            {
                return true;
            }
        }

        return false;
    }

    private static float Horizontal(Vector3 offset) => new Vector2(offset.x, offset.z).magnitude;

    /// <summary>A copy of a part's models, colliders and animation, without its scripts.</summary>
    private static void Copy(Transform part, Transform parent, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        var model = UnityEngine.Object.Instantiate(part.gameObject, parent, worldPositionStays: false);
        model.name = part.name;
        model.transform.localPosition = position;
        model.transform.localRotation = rotation;
        model.transform.localScale = scale;
        foreach (var behaviour in model.GetComponentsInChildren<MonoBehaviour>(includeInactive: true))
        {
            UnityEngine.Object.DestroyImmediate(behaviour);
        }
    }
}
