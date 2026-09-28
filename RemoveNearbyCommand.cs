using Jotunn.Entities;

namespace Landscaper;

/// <summary>
/// Console command that removes Landscaper pieces near the player without aiming at them, for
/// pieces that are too large or awkward to target with the remove button. It only touches pieces
/// placed through this mod, never inside a ward the player has no access to, and, unless the player
/// is an admin or the host, only pieces they placed themselves. Console commands are available to
/// every player, so without these checks anyone could clear other players' landscaping.
/// </summary>
public sealed class RemoveNearbyCommand : ConsoleCommand
{
    private const float DefaultRadius = 10f;
    private const float MaxRadius = 100f;

    public override string Name => "landscaper_remove";

    public override string Help => $"[radius] - Remove your Landscaper pieces within this many meters of you (default {DefaultRadius:0}, max {MaxRadius:0}); admins remove anyone's";

    public override void Run(string[] args, Terminal context)
    {
        var player = Player.m_localPlayer;
        if (player is null || ZNetScene.instance is null)
        {
            context.AddString("Load into a world first.");
            return;
        }

        var radius = DefaultRadius;
        if (args.Length > 0 && (!float.TryParse(args[0], out radius) || radius <= 0f))
        {
            context.AddString("Usage: landscaper_remove [radius]");
            return;
        }

        radius = Math.Min(radius, MaxRadius);
        var isAdmin = ZNet.instance != null && ZNet.instance.LocalPlayerIsAdminOrHost();
        var pieces = new List<Piece>();
        Piece.GetAllPiecesInRadius(player.transform.position, radius, pieces);

        var removed = 0;
        var skipped = 0;
        var notAllowed = 0;
        foreach (var piece in pieces)
        {
            if (piece == null || !PlacementInput.IsLandscaperPiece(piece.gameObject))
            {
                continue;
            }

            var view = piece.GetComponent<ZNetView>();
            if (view == null || !view.IsValid())
            {
                continue;
            }

            // Other players' pieces, and anything inside a ward this player can't use, stay.
            if ((!isAdmin && !piece.IsCreator()) || !PrivateArea.CheckAccess(piece.transform.position, flash: false))
            {
                notAllowed++;
                continue;
            }

            // Like the remove button: not while a chest still has items in it, or someone is using it.
            if (!piece.CanBeRemoved())
            {
                skipped++;
                continue;
            }

            // Same as the hammer: building pieces break normally, everything else is destroyed.
            var wearNTear = piece.GetComponent<WearNTear>();
            if (wearNTear != null)
            {
                wearNTear.Remove();
            }
            else
            {
                view.ClaimOwnership();
                ZNetScene.instance.Destroy(piece.gameObject);
            }

            removed++;
        }

        context.AddString(removed == 0
            ? $"No Landscaper pieces within {radius:0.#} m."
            : $"Removed {removed} Landscaper piece(s) within {radius:0.#} m.");
        if (notAllowed > 0)
        {
            context.AddString(isAdmin
                ? $"Left {notAllowed} inside wards you don't have access to."
                : $"Left {notAllowed} placed by other players or inside wards you don't have access to.");
        }

        if (skipped > 0)
        {
            context.AddString($"Skipped {skipped} that can't be removed yet (a chest with items in it, or in use).");
        }
    }
}
