using UnityEngine;

namespace Landscaper;

/// <summary>
/// Keeps a placed water piece, such as a lily pad, resting on the water surface: on the sea it rises
/// and falls with the waves, and on a pond it follows the water level. Only the visible position
/// moves; the saved position stays where it was placed. The piece can sit a little above or below the
/// surface, as it was nudged when placed (saved as a height relative to the surface).
/// </summary>
internal sealed class WaterFloat : MonoBehaviour
{
    /// <summary>Saved on the piece: its height above the water surface, from nudging while placing.</summary>
    private static readonly int OffsetKey = "LandscaperWaterOffset".GetStableHashCode();

    /// <summary>Beyond this distance from the camera nobody sees the bobbing, so it isn't updated.</summary>
    private const float UpdateDistance = 80f;

    private const float UpdateInterval = 0.1f;

    private ZNetView? _view;
    private float _offset;
    private float _nextUpdate;

    /// <summary>
    /// The water surface height under <paramref name="position"/>: the sea with its waves, or a pond.
    /// Null if there's no water there.
    /// </summary>
    public static float? SurfaceAt(Vector3 position)
    {
        // Sampled a little below, so the point is inside the water even in a wave trough.
        var level = Floating.GetLiquidLevel(position + Vector3.down * 0.5f);
        return level > -1000f ? level : null;
    }

    /// <summary>Saves how far above the surface a just-placed piece was nudged.</summary>
    public static void SaveOffset(GameObject piece, float offset)
    {
        var view = piece.GetComponent<ZNetView>();
        if (view != null && view.IsValid())
        {
            view.GetZDO().Set(OffsetKey, offset);
        }
    }

    private void Start()
    {
        _view = GetComponent<ZNetView>();
        if (_view == null || !_view.IsValid())
        {
            // The placement ghost; ScaleController keeps that on the water.
            enabled = false;
            return;
        }

        _nextUpdate = Time.time + UnityEngine.Random.Range(0f, UpdateInterval);
    }

    private void Update()
    {
        if (Time.time < _nextUpdate || _view == null || !_view.IsValid())
        {
            return;
        }

        _nextUpdate = Time.time + UpdateInterval;
        var camera = GameCamera.instance;
        if (camera == null || (camera.transform.position - transform.position).sqrMagnitude > UpdateDistance * UpdateDistance)
        {
            return;
        }

        _offset = _view.GetZDO().GetFloat(OffsetKey, 0f);
        if (SurfaceAt(transform.position) is { } surface)
        {
            var position = transform.position;
            position.y = surface + _offset;
            transform.position = position;
        }
    }
}
