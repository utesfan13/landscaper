using UnityEngine;

namespace Landscaper;

/// <summary>
/// Closes doors whose animation only opens. A door tells its animator "state": 0 closed, otherwise
/// open. Dungeon doors such as the Dvergr secret door are made to open once: their animator goes
/// closed, then "opening" (the movement), then "open" (a still pose), with no way back. As a building
/// piece (Landscaper lets them close; see MakeStaticDecoration) they stayed open. This notes the
/// opening movement as the door opens, and when the door is closed but its animation is still on the
/// open pose a moment later, plays the opening movement backwards, then puts the animator back in its
/// starting (closed) state, which looks the same and lets the door be opened again as usual (doors
/// only take a use while their animation is on an "open" or "closed" state). Doors whose animation
/// closes by itself are left alone.
/// </summary>
internal sealed class DoorClosing : MonoBehaviour
{
    /// <summary>How long a door may take to start closing by itself before it's closed by hand.</summary>
    private const float Patience = 0.3f;

    private ZNetView? _view;
    private Animator? _animator;
    private float _stuckFor;
    private bool _reversing;

    /// <summary>The opening movement, noted as the door opened, and how long it takes.</summary>
    private int _openingState;
    private float _openingLength = 1f;

    /// <summary>Where the reversed movement is, from 1 (open) to 0 (closed).</summary>
    private float _time;

    private void Awake()
    {
        _view = GetComponent<ZNetView>();
        _animator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        if (_view == null || !_view.IsValid() || _animator == null)
        {
            return;
        }

        var current = _animator.GetCurrentAnimatorStateInfo(0);
        var closed = _view.GetZDO().GetInt(ZDOVars.s_state) == 0;
        if (!closed)
        {
            if (_reversing)
            {
                // Opened again while closing: the opening movement forwards from where it got to.
                _reversing = false;
                _animator.speed = 1f;
                _animator.Play(_openingState, 0, _time);
            }
            else if (current.IsTag("opening"))
            {
                _openingState = current.fullPathHash;
                _openingLength = Mathf.Max(current.length, 0.1f);
            }

            _stuckFor = 0f;
            return;
        }

        if (_reversing)
        {
            _time -= Time.deltaTime / _openingLength;
            if (_time > 0f)
            {
                _animator.Play(_openingState, 0, _time);
                return;
            }

            // Closed: back to the animator's starting state, the closed one.
            _reversing = false;
            _animator.speed = 1f;
            _animator.Rebind();
            _animator.SetInteger("state", 0);
            return;
        }

        if (!current.IsTag("open") || _animator.IsInTransition(0))
        {
            _stuckFor = 0f;
            return;
        }

        _stuckFor += Time.deltaTime;
        if (_stuckFor < Patience)
        {
            return;
        }

        // Still open: run the opening movement backwards from its end, holding the animator on it.
        // Without a noted movement (it opened before this was there), the open pose itself.
        if (_openingState == 0)
        {
            _openingState = current.fullPathHash;
            _openingLength = Mathf.Max(current.length, 0.1f);
        }

        _time = 1f;
        _animator.speed = 0f;
        _reversing = true;
    }
}
