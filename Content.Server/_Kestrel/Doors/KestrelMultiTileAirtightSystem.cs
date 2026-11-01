// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Robust.Shared.Map.Components;

namespace Content.Server._Kestrel.Doors;

/// <summary>
/// Keeps a multi-tile door's extra tile airtight whenever the door itself is.
/// </summary>
/// <remarks>
/// How it works:
/// 1. When the door starts existing on the map (MapInit), it finds or places a filler
///    on its extra tile. The filler is just an anchored, invisible entity with an
///    <see cref="AirtightComponent"/>.
/// 2. Whenever the door's own airtightness changes (it opens, closes, gets welded...),
///    the airtight system raises <see cref="AirtightChanged"/> on the door, and we copy
///    the new value onto the filler.
/// 3. When the door is deleted, the filler goes with it.
/// We copy the door's airtight state instead of watching open/close events, so anything
/// that makes the door airtight or not is covered without us having to know about it.
/// </remarks>
public sealed class KestrelMultiTileAirtightSystem : EntitySystem
{
    [Dependency] private readonly AirtightSystem _airtight = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<KestrelMultiTileAirtightComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<KestrelMultiTileAirtightComponent, AirtightChanged>(OnAirtightChanged);
        SubscribeLocalEvent<KestrelMultiTileAirtightComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnMapInit(Entity<KestrelMultiTileAirtightComponent> ent, ref MapInitEvent args)
    {
        var xform = Transform(ent);
        if (!xform.Anchored || xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        // Work out the extra tile. The offset is turned by the door's rotation, so it
        // still points the right way if the door is ever placed rotated.
        var doorTile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        var turned = xform.LocalRotation.RotateVec(new Vector2(ent.Comp.Offset.X, ent.Comp.Offset.Y));
        var extraTile = doorTile + new Vector2i((int) MathF.Round(turned.X), (int) MathF.Round(turned.Y));

        // A saved map might already have a filler on that tile; reuse it instead of
        // stacking a second one.
        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, extraTile);
        while (anchored.MoveNext(out var existing))
        {
            if (!HasComp<KestrelAirtightFillerComponent>(existing))
                continue;

            ent.Comp.Filler = existing;
            break;
        }

        if (ent.Comp.Filler == null)
        {
            var filler = Spawn(ent.Comp.FillerPrototype, _map.GridTileToLocal(gridUid, grid, extraTile));
            _transform.AnchorEntity(filler);
            ent.Comp.Filler = filler;
        }

        SyncFiller(ent);
    }

    private void OnAirtightChanged(Entity<KestrelMultiTileAirtightComponent> ent, ref AirtightChanged args)
    {
        SyncFiller(ent);
    }

    private void OnShutdown(Entity<KestrelMultiTileAirtightComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Filler is { } filler && !TerminatingOrDeleted(filler))
            QueueDel(filler);

        ent.Comp.Filler = null;
    }

    /// <summary>
    /// Makes the filler block air exactly when the door does.
    /// </summary>
    private void SyncFiller(Entity<KestrelMultiTileAirtightComponent> ent)
    {
        if (ent.Comp.Filler is not { } filler
            || !TryComp<AirtightComponent>(ent, out var doorAirtight)
            || !TryComp<AirtightComponent>(filler, out var fillerAirtight))
        {
            return;
        }

        _airtight.SetAirblocked((filler, fillerAirtight), doorAirtight.AirBlocked);
    }
}
