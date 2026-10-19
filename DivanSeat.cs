using UnityEngine;

namespace Landscaper;

/// <summary>
/// A divan's seat, in place of a chair's: use it to sit on the middle of the cushion facing out, use
/// it again to lie down along it, and again to get up (moving gets up too, as from any seat). The
/// points are measured from the divan's own shape the first time it's used, so they fit whichever
/// divan it is: rays probe the cushion's height across it, the higher part along one side is the
/// backrest, and the seat faces away from it.
/// </summary>
public sealed class DivanSeat : MonoBehaviour, Hoverable, Interactable
{
    private const float UseDistance = 2.5f;
    private const int Samples = 9;

    /// <summary>
    /// How far below the cushion's top the seat is: the sitting animation holds the hips above the
    /// point it's attached to.
    /// </summary>
    private const float SitDepth = 0.52f;

    /// <summary>Lies the other way along it: set for the divan whose model runs the other way.</summary>
    public bool FlipLie;

    private Transform? _sit;
    private Transform? _lie;

    public string GetHoverName() => Localization.instance.Localize("$piece_chair");

    public string GetHoverText()
    {
        if (Player.m_localPlayer is not { } player)
        {
            return string.Empty;
        }

        var name = Piece() is { } piece ? piece.m_name : string.Empty;
        var action = IsAt(player, _sit) ? "Lie down" : IsAt(player, _lie) ? "Get up" : "Sit";
        return Localization.instance.Localize($"{name}\n[<color=yellow><b>$KEY_Use</b></color>] {action}");
    }

    public float GetHoverOffset() => 0f;

    public bool UseItem(Humanoid user, ItemDrop.ItemData item) => false;

    public bool Interact(Humanoid human, bool hold, bool alt)
    {
        if (hold || human is not Player player || player.IsEncumbered())
        {
            return false;
        }

        Measure();
        if (_sit == null || _lie == null || Vector3.Distance(player.transform.position, _sit.position) > UseDistance)
        {
            return false;
        }

        if (IsAt(player, _lie))
        {
            player.AttachStop();
        }
        else if (IsAt(player, _sit))
        {
            player.AttachStop();
            player.AttachStart(_lie, gameObject, hideWeapons: true, isBed: false, onShip: false, "attach_bed", new Vector3(0f, 0.5f, 0f));
        }
        else if (!InUse())
        {
            player.AttachStart(_sit, gameObject, hideWeapons: false, isBed: false, onShip: false, "attach_chair", new Vector3(0f, 0.5f, 0f));
        }

        return false;
    }

    private Piece? Piece() => GetComponent<Piece>();

    private static bool IsAt(Player player, Transform? point) => point != null && player.IsAttached() && player.GetAttachPoint() == point;

    /// <summary>Whether someone else is sitting or lying on it.</summary>
    private bool InUse() =>
        (_sit != null && Player.GetClosestPlayer(_sit.position, 0.05f) != null) ||
        (_lie != null && Player.GetClosestPlayer(_lie.position, 0.05f) != null);

    /// <summary>Places the sit and lie points from the divan's shape, once.</summary>
    private void Measure()
    {
        if (_sit != null)
        {
            return;
        }

        var root = transform;
        var colliders = GetComponentsInChildren<Collider>()
            .Where(collider => collider.enabled && !collider.isTrigger && collider.name != DecorativePieceManager.TargetBoxName)
            .ToList();
        var meshes = GetComponentsInChildren<MeshFilter>().Where(filter => filter.sharedMesh != null).ToList();
        if (colliders.Count == 0 || meshes.Count == 0)
        {
            return;
        }

        // The model's extent in the divan's own space, from its meshes' own bounds: the same however
        // it's turned (world-space bounds grow with the turn, and could swap its length and width).
        var min = Vector3.positiveInfinity;
        var max = Vector3.negativeInfinity;
        foreach (var filter in meshes)
        {
            var bounds = filter.sharedMesh.bounds;
            var toRoot = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            for (var corner = 0; corner < 8; corner++)
            {
                var point = toRoot.MultiplyPoint3x4(new Vector3(
                    (corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (corner & 2) == 0 ? bounds.min.y : bounds.max.y,
                    (corner & 4) == 0 ? bounds.min.z : bounds.max.z));
                min = Vector3.Min(min, point);
                max = Vector3.Max(max, point);
            }
        }

        // Along its length (a) and across it (b).
        var alongX = max.x - min.x >= max.z - min.z;
        Vector3 Local(float a, float b, float y) => alongX ? new Vector3(a, y, b) : new Vector3(b, y, a);
        float A(Vector3 local) => alongX ? local.x : local.z;
        float B(Vector3 local) => alongX ? local.z : local.x;

        // The height of the top surface across it, by casting down onto its colliders.
        var heights = new List<(float A, float B, float Y)>();
        for (var i = 0; i < Samples; i++)
        {
            for (var j = 0; j < Samples; j++)
            {
                var a = Mathf.Lerp(A(min), A(max), (i + 0.5f) / Samples);
                var b = Mathf.Lerp(B(min), B(max), (j + 0.5f) / Samples);
                var origin = root.TransformPoint(Local(a, b, max.y + 1f));
                var ray = new Ray(origin, -root.up);
                var best = float.NegativeInfinity;
                foreach (var collider in colliders)
                {
                    if (collider.Raycast(ray, out var hit, (max.y - min.y + 2f) * Mathf.Max(1f, root.lossyScale.y)))
                    {
                        best = Mathf.Max(best, root.InverseTransformPoint(hit.point).y);
                    }
                }

                if (!float.IsNegativeInfinity(best))
                {
                    heights.Add((a, b, best));
                }
            }
        }

        var middleA = (A(min) + A(max)) / 2f;
        var middleB = (B(min) + B(max)) / 2f;
        var seatY = min.y + (max.y - min.y) * 0.45f;
        var seatB = middleB;
        var front = 1f;
        if (heights.Count > 0)
        {
            // The cushion is the low part of the middle, away from the arms at the ends; the backrest
            // the high part, along one side.
            var halfLength = (A(max) - A(min)) / 2f;
            var middle = heights.Where(h => Mathf.Abs(h.A - middleA) < halfLength * 0.6f).ToList();
            if (middle.Count == 0)
            {
                middle = heights;
            }

            var sorted = middle.Select(h => h.Y).OrderBy(y => y).ToList();
            seatY = sorted[sorted.Count / 3];
            var seat = middle.Where(h => h.Y <= seatY + 0.12f).ToList();
            var back = middle.Where(h => h.Y > seatY + 0.25f).ToList();
            if (seat.Count > 0)
            {
                seatB = seat.Average(h => h.B);
            }

            if (back.Count > 0)
            {
                front = back.Average(h => h.B) > seatB ? -1f : 1f;
            }
        }

        _sit = Point("LandscaperDivanSit", Local(middleA, seatB, seatY - SitDepth), Local(0f, front, 0f));
        _lie = Point("LandscaperDivanLie", Local(middleA, seatB, seatY + 0.05f), Local(FlipLie ? -1f : 1f, 0f, 0f));
    }

    private Transform Point(string name, Vector3 position, Vector3 forward)
    {
        var point = new GameObject(name).transform;
        point.SetParent(transform, worldPositionStays: false);
        point.localPosition = position;
        point.localRotation = Quaternion.LookRotation(forward, Vector3.up);
        return point;
    }
}
