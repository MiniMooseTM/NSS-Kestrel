// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Tag;
using Content.Shared.Wall;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Kestrel.Wall;

/// <summary>
/// Turns posters and signs that face into a wall so they face an open floor tile instead.
/// </summary>
/// <remarks>
/// How it works:
/// 1. When the entity starts existing on a running map (MapInit: round start, or spawned mid-round),
///    work out which way it faces. That's its rotation plus the wall mount's own Direction:
///    0 = South, 90 = East, 180 = North, 270 = West. Facing south means "visible from the south".
/// 2. Look at the tile one step in that direction. If it's floor with no wall on it, the facing is
///    fine and we change nothing.
/// 3. Otherwise try South, North, East, West in that order, and turn to face the first open floor.
///    If none is open, leave it as it is.
/// Maps aren't changed: this runs on the live copy, not while mapping (unpaused maps only).
/// The sprite has noRot, so turning it doesn't turn the picture, only the side it's visible from.
/// </remarks>
public sealed class KestrelWallMountAutoFaceSystem : EntitySystem
{
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    // Many Goob walls don't have WallComponent but all carry the Wall tag (same as the visibility code).
    private static readonly ProtoId<TagPrototype> WallTag = "Wall";

    private static readonly Direction[] TryOrder = { Direction.South, Direction.North, Direction.East, Direction.West };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<KestrelWallMountAutoFaceComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<KestrelWallMountAutoFaceComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<WallMountComponent>(ent, out var mount))
            return;

        var xform = Transform(ent);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        var facing = (xform.LocalRotation + mount.Direction).GetCardinalDir();

        if (IsOpenFloor((gridUid, grid), tile + facing.ToIntVec()))
            return;

        foreach (var dir in TryOrder)
        {
            if (!IsOpenFloor((gridUid, grid), tile + dir.ToIntVec()))
                continue;

            // Rotation that makes (rotation + mount.Direction) point at dir.
            _transform.SetLocalRotation(ent, dir.ToAngle() - mount.Direction, xform);
            return;
        }
    }

    /// <summary>
    /// True if the tile has floor (not space) and no wall anchored on it.
    /// </summary>
    private bool IsOpenFloor(Entity<MapGridComponent> grid, Vector2i tile)
    {
        if (!_map.TryGetTileRef(grid, grid.Comp, tile, out var tileRef) || tileRef.Tile.IsEmpty)
            return false;

        var anchored = _map.GetAnchoredEntitiesEnumerator(grid, grid.Comp, tile);
        while (anchored.MoveNext(out var uid))
        {
            if (HasComp<WallComponent>(uid) || _tag.HasTag(uid.Value, WallTag))
                return false;
        }

        return true;
    }
}
