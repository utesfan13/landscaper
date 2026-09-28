using UnityEngine;

namespace Landscaper;

/// <summary>
/// Makes decorative copies work like the vanilla piece they look like: a chest to store things in, a
/// chair or bench to sit on, or a bed to sleep and set your spawn point in. Valheim's function lives
/// in Container, Chair and Bed components, independent of the model, so the matching component is
/// copied from a vanilla piece (the "template") onto the clone, along with the template's comfort.
/// Chairs and beds need a point to sit or lie at; it's recreated at the same spot relative to the
/// model, which is exact for props that are copies of the template and close for similar furniture.
/// </summary>
internal static class FunctionalPieces
{
    public static void Apply(GameObject clone, string templateName)
    {
        var template = ZNetScene.instance?.GetPrefab(templateName);
        if (template == null)
        {
            Plugin.Log.LogWarning($"Function template '{templateName}' for '{clone.name}' was not found.");
            return;
        }

        if (template.GetComponentInChildren<Container>() is { } container && clone.GetComponentInChildren<Container>() == null)
        {
            CopyContainer(container, clone);
        }

        if (template.GetComponentInChildren<Chair>() is { } chair && clone.GetComponentInChildren<Chair>() == null)
        {
            CopyChair(chair, template, clone);
        }

        if (template.GetComponentInChildren<Bed>() is { } bed && clone.GetComponentInChildren<Bed>() == null)
        {
            CopyBed(bed, template, clone);
        }

        // Comfort counts towards the rested buff, like the vanilla piece.
        if (template.GetComponent<Piece>() is { } templatePiece && clone.GetComponent<Piece>() is { } piece)
        {
            piece.m_comfort = templatePiece.m_comfort;
            piece.m_comfortGroup = templatePiece.m_comfortGroup;
        }
    }

    private static void CopyContainer(Container from, GameObject clone)
    {
        var to = clone.AddComponent<Container>();
        to.m_name = from.m_name;
        to.m_bkg = from.m_bkg;
        to.m_width = from.m_width;
        to.m_height = from.m_height;
        to.m_privacy = from.m_privacy;
        to.m_checkGuardStone = from.m_checkGuardStone;
        to.m_openEffects = from.m_openEffects;
        to.m_closeEffects = from.m_closeEffects;
        to.m_hoverOffset = from.m_hoverOffset;
        // Storage only: no loot, and it stays when empty. There is no open/closed model to switch.
        to.m_autoDestroyEmpty = false;
        to.m_defaultItems = new DropTable();
    }

    private static void CopyChair(Chair from, GameObject template, GameObject clone)
    {
        var to = clone.AddComponent<Chair>();
        to.m_name = from.m_name;
        to.m_useDistance = from.m_useDistance;
        to.m_detachOffset = from.m_detachOffset;
        to.m_attachAnimation = from.m_attachAnimation;
        to.m_hoverOffset = from.m_hoverOffset;
        to.m_attachPoint = CopyPoint(from.m_attachPoint, template, clone, "LandscaperSeat");
    }

    private static void CopyBed(Bed from, GameObject template, GameObject clone)
    {
        var to = clone.AddComponent<Bed>();
        to.m_monsterCheckRadius = from.m_monsterCheckRadius;
        to.m_hoverOffset = from.m_hoverOffset;
        to.m_spawnPoint = CopyPoint(from.m_spawnPoint, template, clone, "LandscaperSleep");
    }

    /// <summary>A child of the clone at the same position and rotation, relative to the root, as
    /// <paramref name="point"/> is in the template. Scaling the piece scales it with the model.</summary>
    private static Transform CopyPoint(Transform? point, GameObject template, GameObject clone, string name)
    {
        var child = new GameObject(name).transform;
        child.SetParent(clone.transform, worldPositionStays: false);
        if (point != null)
        {
            var root = template.transform;
            child.localPosition = root.InverseTransformPoint(point.position);
            child.localRotation = Quaternion.Inverse(root.rotation) * point.rotation;
        }

        return child;
    }
}
