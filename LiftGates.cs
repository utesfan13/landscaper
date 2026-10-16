using UnityEngine;

namespace Landscaper;

/// <summary>
/// Gates that open without a door of their own. The Jotun gate doors, the Ashlands fortress gate door,
/// the rusty sunken crypt gate and the infested mine door are barriers in the game that are smashed or
/// stay shut. As Landscaper pieces, using one opens it and using it again closes it: most lift by their
/// own height like a castle portcullis; the rusty crypt gate swings on its hinge, away from whoever
/// opens it, like a door; and the Ashlands fortress gate door opens as a double door, its two leaves
/// (already separate in the game's model) swinging apart on their outer edges, always toward the side
/// the spikes are on, so the spikes end up facing out, whichever side it's opened from. Whether it's open is saved on the piece, so everyone sees the same; without
/// the mod it's simply the closed gate. The infested mine door lifts only as its copy in the Gates and Doors tab;
/// the one in Dungeon Decor stays shut.
/// A lifting gate's model and colliders are moved under a child that slides, and a swinging gate turns
/// its own door part, so the piece itself (and its saved position) stays put and an open gate can be
/// walked through.
/// </summary>
internal static class LiftGates
{
    /// <summary>The gates that lift, by the prefab they're made from.</summary>
    private static readonly HashSet<string> LiftPrefabs = new(StringComparer.OrdinalIgnoreCase)
    {
        "Morkhalla_GateDoor", "Morkhalla_GateDoor02", "Morkhalla_GateDoor03"
    };

    /// <summary>The gates that swing on a hinge, and their part that swings (its origin is the hinge).</summary>
    private static readonly Dictionary<string, string> SwingPrefabs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["dungeon_sunkencrypt_irongate_rusty"] = "door"
    };

    /// <summary>
    /// Double doors: each leaf's parts, by name, and its hinge (the outer edge, where the leaf's own
    /// parts have their origin). The right leaf is a mirror image, so it swings the other way.
    /// </summary>
    private static readonly Dictionary<string, (Vector3 Hinge, float Mirror, string[] Parts)[]> DoublePrefabs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Ashlands_Fortress_Gate_Door"] = new[]
        {
            // With the gate's fixed direction (see Prepare), both leaves swing toward its spiked side.
            (new Vector3(-2f, 0f, 0f), 1f, new[] { "high", "low", "collider" }),
            (new Vector3(2f, 0f, 0f), -1f, new[] { "high (1)", "low (1)", "collider (1)" })
        }
    };

    public static bool Opens(string prefabName) =>
        LiftPrefabs.Contains(prefabName) || SwingPrefabs.ContainsKey(prefabName) || DoublePrefabs.ContainsKey(prefabName);

    /// <summary>
    /// Sets a gate up to open. Call while setting up the piece, before snap points and the aiming box
    /// are added (those stay with the piece).
    /// </summary>
    /// <param name="lift">Lift, whatever the prefab (a catalog entry's LiftGate).</param>
    public static void Prepare(GameObject clone, string prefabName, Bounds bounds, bool lift = false)
    {
        var height = bounds.size.y;
        // A double door: each leaf's parts under a hinge at its outer edge, swinging opposite ways.
        if (!lift && DoublePrefabs.TryGetValue(prefabName, out var leaves))
        {
            var first = true;
            foreach (var (hingePosition, mirror, parts) in leaves)
            {
                var hinge = new GameObject("LandscaperHinge").transform;
                hinge.SetParent(clone.transform, worldPositionStays: false);
                hinge.localPosition = hingePosition;
                foreach (var part in parts.Select(name => FindDeep(clone.transform, name)).OfType<Transform>().ToList())
                {
                    part.SetParent(hinge, worldPositionStays: true);
                }

                var gate = hinge.gameObject.AddComponent<Gate>();
                gate.Swing = true;
                gate.Mirror = mirror;
                gate.FixedDirection = 1;
                gate.Secondary = !first;
                gate.ClearsDoorway = first;
                first = false;
            }

            StopCollapsing(clone);
            return;
        }

        // A swinging gate turns its own door part, around that part's origin.
        if (!lift && SwingPrefabs.TryGetValue(prefabName, out var hingeName))
        {
            if (clone.transform.Find(hingeName) is { } hinge)
            {
                hinge.gameObject.AddComponent<Gate>().Swing = true;
                StopCollapsing(clone);
            }

            return;
        }

        // A lifting gate: everything under a child that slides up.
        var root = clone.transform;
        var mover = new GameObject("LandscaperGateMover").transform;
        mover.SetParent(root, worldPositionStays: false);
        foreach (var child in root.Cast<Transform>().Where(child => child != mover).ToList())
        {
            child.SetParent(mover, worldPositionStays: true);
        }

        // On the mover, nearer the colliders than the piece's own scripts, so aiming at the gate finds it.
        var lifting = mover.gameObject.AddComponent<Gate>();
        lifting.Height = Mathf.Max(height, 0.5f);

        // A raised gate is out of reach, so the gate can be used from where it is closed too: a box
        // there that only catches aiming. A trigger doesn't block anything, and on the item layer the
        // building ray doesn't hit it either, so pieces can still be placed through an open gate.
        var handle = new GameObject("LandscaperGateHandle") { layer = LayerMask.NameToLayer("item") };
        handle.transform.SetParent(root, worldPositionStays: false);
        var box = handle.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = bounds.center;
        box.size = bounds.size;
        handle.AddComponent<Handle>().Gate = lifting;
        StopCollapsing(clone);
    }

    private static Transform? FindDeep(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name)
            {
                return child;
            }

            if (FindDeep(child, name) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>An open gate isn't held up by anything; don't let it collapse.</summary>
    private static void StopCollapsing(GameObject clone)
    {
        foreach (var wear in clone.GetComponentsInChildren<WearNTear>(includeInactive: true))
        {
            wear.m_noSupportWear = false;
        }
    }

    /// <summary>The box where a lifting gate is closed: using it uses the gate.</summary>
    internal sealed class Handle : MonoBehaviour, Hoverable, Interactable
    {
        public Gate? Gate;

        public bool Interact(Humanoid user, bool hold, bool alt) => Gate != null && Gate.Interact(user, hold, alt);

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) => false;

        public string GetHoverText() => Gate != null ? Gate.GetHoverText() : string.Empty;

        public string GetHoverName() => Gate != null ? Gate.GetHoverName() : string.Empty;

        public float GetHoverOffset() => 0f;
    }

    /// <summary>The gate control: use to open or close it.</summary>
    internal sealed class Gate : MonoBehaviour, Hoverable, Interactable
    {
        private const string ToggleRpc = "LandscaperToggleGate";
        private const string LiftSound = "sfx_stonedoor_sliding_open";
        private const string SwingSound = "sfx_door_open";
        private const float SwingAngle = 90f;

        /// <summary>Saved on the piece: 0 closed, otherwise open (a swinging gate: 1 or -1, the way it swung).</summary>
        private static readonly int StateKey = "LandscaperGateState".GetStableHashCode();

        /// <summary>How far a lifting gate lifts, in the piece's own units (so it follows the piece's size).</summary>
        public float Height = 1f;

        /// <summary>Swings on a hinge (this part's origin) instead of lifting.</summary>
        public bool Swing;

        /// <summary>-1 for a mirrored leaf of a double door, which swings the other way.</summary>
        public float Mirror = 1f;

        /// <summary>The second leaf of a double door: it follows the first's state, which handles using it.</summary>
        public bool Secondary;

        /// <summary>A swinging gate that always opens one way (1 or -1) rather than away from whoever opens it.</summary>
        public int FixedDirection;

        /// <summary>
        /// While open, switches off the piece's colliders that don't swing with a leaf (a double door's
        /// hit areas fill the doorway), so the way through is clear; they're back once it's closed.
        /// </summary>
        public bool ClearsDoorway;

        private List<Collider>? _doorwayColliders;

        private ZNetView? _view;
        private float _open;
        private int _wasState;
        private int _direction = 1;
        private Quaternion _closedRotation;

        private float Duration => Swing ? 1.2f : Mathf.Clamp(Height / 2f, 1.5f, 5f);

        private void Awake()
        {
            _closedRotation = transform.localRotation;
            _view = GetComponentInParent<ZNetView>();
            if (_view != null && _view.GetZDO() != null)
            {
                if (!Secondary)
                {
                    _view.Register<int>(ToggleRpc, RPC_Toggle);
                }

                _wasState = State();
                if (_wasState != 0)
                {
                    _direction = _wasState;
                }

                _open = _wasState != 0 ? 1f : 0f;
                Apply();
            }

            if (ClearsDoorway && _view != null)
            {
                var leaves = _view.GetComponentsInChildren<Gate>(includeInactive: true).Select(gate => gate.transform).ToList();
                _doorwayColliders = _view.GetComponentsInChildren<Collider>(includeInactive: true)
                    .Where(collider => !leaves.Any(leaf => collider.transform.IsChildOf(leaf)) && collider.GetComponent<Handle>() == null)
                    .ToList();
                ClearDoorway();
            }
        }

        /// <summary>The doorway's other colliders are on only while the gate is fully closed.</summary>
        private void ClearDoorway()
        {
            if (_doorwayColliders == null)
            {
                return;
            }

            var closed = _open <= 0.001f;
            foreach (var collider in _doorwayColliders)
            {
                if (collider != null && collider.enabled != closed)
                {
                    collider.enabled = closed;
                }
            }
        }

        private int State() => _view != null && _view.IsValid() ? _view.GetZDO().GetInt(StateKey) : 0;

        private bool IsOpen() => State() != 0;

        private void Update()
        {
            if (_view == null || !_view.IsValid())
            {
                return;
            }

            var state = State();
            if (state != _wasState)
            {
                _wasState = state;
                if (state != 0)
                {
                    _direction = state;
                }

                if (!Secondary && ZNetScene.instance?.GetPrefab(Swing ? SwingSound : LiftSound) is { } sound)
                {
                    Instantiate(sound, transform.position, Quaternion.identity);
                }
            }

            var target = state != 0 ? 1f : 0f;
            if (!Mathf.Approximately(_open, target))
            {
                _open = Mathf.MoveTowards(_open, target, Time.deltaTime / Duration);
                Apply();
            }

            ClearDoorway();
        }

        private void Apply()
        {
            var amount = Mathf.SmoothStep(0f, 1f, _open);
            if (Swing)
            {
                transform.localRotation = _closedRotation * Quaternion.Euler(0f, SwingAngle * _direction * Mirror * amount, 0f);
            }
            else
            {
                transform.localPosition = new Vector3(0f, Height * amount, 0f);
            }
        }

        /// <summary>The owner opens the gate the way asked, or closes it.</summary>
        private void RPC_Toggle(long sender, int direction)
        {
            if (_view != null && _view.IsOwner())
            {
                _view.GetZDO().Set(StateKey, State() != 0 ? 0 : (direction < 0 ? -1 : 1));
            }
        }

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold || _view == null || !_view.IsValid())
            {
                return false;
            }

            if (!PrivateArea.CheckAccess(transform.position))
            {
                return true;
            }

            // A swinging gate opens away from whoever opens it, like a vanilla door, unless it has a set way.
            var side = Vector3.Dot(transform.forward, user.transform.position - transform.position);
            _view.InvokeRPC(ToggleRpc, FixedDirection != 0 ? FixedDirection : side > 0f ? -1 : 1);
            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) => false;

        public string GetHoverName() => GetComponentInParent<Piece>() is { } piece ? piece.m_name : "Gate";

        public float GetHoverOffset() => 0f;

        public string GetHoverText()
        {
            var action = IsOpen() ? (Swing ? "Close" : "Lower") : (Swing ? "Open" : "Raise");
            return Localization.instance.Localize($"{GetHoverName()}\n[<color=yellow><b>$KEY_Use</b></color>] {action}");
        }
    }
}
