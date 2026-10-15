// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Kestrel mapping copy/paste tool (server side).
// Copies tiles, entities (exactly, using the same serializer that saves maps) and decals from a box on a grid,
// pastes them elsewhere with rotate/flip, and keeps the last 10 pastes per admin so they can be undone.
// Only admins with the Mapping flag can use it. No engine code is modified.

using System.Linq;
using System.Numerics;
using Content.Server.Administration.Managers;
using Content.Server.Decals;
using Content.Server.NodeContainer.Nodes;
using Content.Shared._Kestrel.Mapping;
using Content.Shared.Administration;
using Content.Shared.Atmos;
using Content.Shared.Decals;
using Content.Shared.NodeContainer;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Map.Events;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Serialization.Markdown.Mapping;

namespace Content.Server._Kestrel.Mapping;

public sealed class KestrelCopyPasteSystem : EntitySystem
{
    [Dependency] private readonly IAdminManager _admin = default!;
    [Dependency] private readonly IDependencyCollection _deps = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefs = default!;
    [Dependency] private readonly DecalSystem _decals = default!;
    [Dependency] private readonly MapLoaderSystem _loader = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    /// <summary>Biggest box side, in tiles. Stops someone freezing the server with a huge copy.</summary>
    private const int MaxSide = 200;

    private const int MaxUndo = 10;

    private sealed class Clipboard
    {
        public EntityUid SourceGrid;
        public Vector2i Min;
        public Vector2i Size;
        public readonly List<(Vector2i Rel, Tile Tile)> Tiles = new();
        public readonly List<(Vector2 RelCentre, Decal Decal)> Decals = new();
        public readonly HashSet<Vector2i> Footprint = new();
        public MappingDataNode? Entities;
        public int EntityCount;
    }

    private sealed class UndoRecord
    {
        public EntityUid Grid;
        public readonly List<(Vector2i Index, Tile Tile)> OldTiles = new();
        public MappingDataNode? OldEntities;
        public readonly List<Decal> OldDecals = new();
        public readonly HashSet<EntityUid> Spawned = new();
        public readonly List<uint> SpawnedDecals = new();
    }

    private readonly Dictionary<NetUserId, Clipboard> _clipboards = new();
    private readonly Dictionary<NetUserId, List<UndoRecord>> _undo = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<KestrelCopyAreaRequestEvent>(OnCopy);
        SubscribeNetworkEvent<KestrelPasteRequestEvent>(OnPaste);
        SubscribeNetworkEvent<KestrelUndoPasteRequestEvent>(OnUndo);
    }

    #region Copy

    private void OnCopy(KestrelCopyAreaRequestEvent ev, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        if (!_admin.HasAdminFlag(session, AdminFlags.Mapping))
            return;

        var gridUid = GetEntity(ev.Grid);
        if (!TryComp<MapGridComponent>(gridUid, out var grid))
        {
            Status(session, Loc.GetString("kestrel-copypaste-no-grid"));
            return;
        }

        var min = new Vector2i(Math.Min(ev.Min.X, ev.Max.X), Math.Min(ev.Min.Y, ev.Max.Y));
        var max = new Vector2i(Math.Max(ev.Min.X, ev.Max.X), Math.Max(ev.Min.Y, ev.Max.Y));
        var size = max - min + new Vector2i(1, 1);

        if (size.X > MaxSide || size.Y > MaxSide)
        {
            Status(session, Loc.GetString("kestrel-copypaste-too-big", ("max", MaxSide)));
            return;
        }

        var clip = new Clipboard
        {
            SourceGrid = gridUid,
            Min = min,
            Size = size,
        };

        // Tiles. Space is skipped so pasting never turns existing floor into space.
        for (var x = min.X; x <= max.X; x++)
        {
            for (var y = min.Y; y <= max.Y; y++)
            {
                var index = new Vector2i(x, y);
                if (!_map.TryGetTileRef(gridUid, grid, index, out var tileRef) || tileRef.Tile.IsEmpty)
                    continue;

                clip.Tiles.Add((index - min, tileRef.Tile));
                clip.Footprint.Add(index - min);
            }
        }

        // Entities sitting directly on the grid inside the box (their contents come along automatically).
        var roots = GetEntitiesInArea(gridUid, grid, index => index.X >= min.X && index.X <= max.X && index.Y >= min.Y && index.Y <= max.Y);
        foreach (var uid in roots)
        {
            clip.Footprint.Add(_map.LocalToTile(gridUid, grid, Transform(uid).Coordinates) - min);
        }

        if (roots.Count > 0)
        {
            try
            {
                clip.Entities = Serialize(roots);
                clip.EntityCount = roots.Count;
            }
            catch (Exception e)
            {
                Log.Error($"Copy/paste: failed to copy entities: {e}");
                Status(session, Loc.GetString("kestrel-copypaste-copy-failed"));
                return;
            }
        }

        // Decals. Stored by their centre so they rotate around the right point.
        var box = new Box2((Vector2) min, (Vector2) (max + new Vector2i(1, 1)));
        foreach (var (_, decal) in _decals.GetDecalsIntersecting(gridUid, box))
        {
            var centre = decal.Coordinates + new Vector2(0.5f, 0.5f);
            clip.Decals.Add((centre - (Vector2) min, decal));
        }

        _clipboards[session.UserId] = clip;

        RaiseNetworkEvent(new KestrelCopyAreaResultEvent(
                GetNetEntity(gridUid),
                size,
                clip.Footprint.ToList(),
                clip.Tiles.Count,
                clip.EntityCount,
                clip.Decals.Count),
            session);

        Status(session, Loc.GetString("kestrel-copypaste-copied",
            ("width", size.X),
            ("height", size.Y),
            ("tiles", clip.Tiles.Count),
            ("entities", clip.EntityCount),
            ("decals", clip.Decals.Count)));
    }

    #endregion

    #region Paste

    private void OnPaste(KestrelPasteRequestEvent ev, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        if (!_admin.HasAdminFlag(session, AdminFlags.Mapping))
            return;

        if (!_clipboards.TryGetValue(session.UserId, out var clip))
        {
            Status(session, Loc.GetString("kestrel-copypaste-nothing-copied"));
            return;
        }

        var gridUid = GetEntity(ev.Grid);
        if (!TryComp<MapGridComponent>(gridUid, out var grid))
        {
            Status(session, Loc.GetString("kestrel-copypaste-no-grid"));
            return;
        }

        if (ev.LockToSource && gridUid != clip.SourceGrid)
        {
            Status(session, Loc.GetString("kestrel-copypaste-wrong-grid"));
            return;
        }

        var rot = ev.Rotation & 3;
        var flip = ev.Flip;
        var origin = ev.Origin;

        Vector2i ToTarget(Vector2i rel) => origin + KestrelCopyTransform.Tile(rel, clip.Size, rot, flip);
        Vector2 ToTargetPoint(Vector2 rel) => (Vector2) origin + KestrelCopyTransform.Point(rel, clip.Size, rot, flip);

        var footprint = clip.Footprint.Select(ToTarget).ToHashSet();
        var record = new UndoRecord { Grid = gridUid };

        // 1. Clear what's already there (Replace mode), remembering it for undo.
        var existing = GetEntitiesInArea(gridUid, grid, footprint.Contains);
        if (existing.Count > 0)
        {
            try
            {
                record.OldEntities = Serialize(existing);
            }
            catch (Exception e)
            {
                Log.Error($"Copy/paste: failed to back up entities before pasting: {e}");
                Status(session, Loc.GetString("kestrel-copypaste-paste-failed"));
                return;
            }

            foreach (var uid in existing)
            {
                Del(uid);
            }
        }

        if (footprint.Count > 0)
        {
            var bounds = BoundsOf(footprint);
            foreach (var (id, decal) in _decals.GetDecalsIntersecting(gridUid, bounds))
            {
                var centre = decal.Coordinates + new Vector2(0.5f, 0.5f);
                var tile = new Vector2i((int) MathF.Floor(centre.X), (int) MathF.Floor(centre.Y));
                if (!footprint.Contains(tile))
                    continue;

                record.OldDecals.Add(decal);
                _decals.RemoveDecal(gridUid, id);
            }
        }

        // 2. Tiles.
        var newTiles = new List<(Vector2i, Tile)>(clip.Tiles.Count);
        foreach (var (rel, tile) in clip.Tiles)
        {
            var index = ToTarget(rel);
            var old = _map.TryGetTileRef(gridUid, grid, index, out var oldRef) ? oldRef.Tile : Tile.Empty;
            record.OldTiles.Add((index, old));

            var rotMirror = tile.RotationMirroring;
            if (_tileDefs.TryGetDefinition(tile.TypeId, out var def) && def.AllowRotationMirror)
                rotMirror = KestrelCopyTransform.TileRotationMirroring(rotMirror, rot, flip);

            newTiles.Add((index, new Tile(tile.TypeId, tile.Flags, tile.Variant, rotMirror)));
        }

        if (newTiles.Count > 0)
            _map.SetTiles(gridUid, grid, newTiles);

        // 3. Entities: exact copies, moved/turned into place.
        if (clip.Entities != null)
        {
            var spawned = LoadOnto(gridUid, clip.Entities, (uid, xform) =>
            {
                var pos = ToTargetPoint(xform.LocalPosition - (Vector2) clip.Min);
                var angle = KestrelCopyTransform.Rotation(xform.LocalRotation, rot, flip);
                if (flip)
                    angle = FixPipeFlip(uid, xform.LocalRotation, angle, rot);
                return (pos, angle);
            });

            if (spawned == null)
                Status(session, Loc.GetString("kestrel-copypaste-paste-entities-failed"));
            else
                record.Spawned.UnionWith(spawned);
        }

        // 4. Decals.
        foreach (var (relCentre, decal) in clip.Decals)
        {
            var bottomLeft = ToTargetPoint(relCentre) - new Vector2(0.5f, 0.5f);
            var placed = decal
                .WithCoordinates(bottomLeft)
                .WithRotation(KestrelCopyTransform.Rotation(decal.Angle, rot, flip));

            if (_decals.TryAddDecal(placed, new EntityCoordinates(gridUid, bottomLeft), out var id))
                record.SpawnedDecals.Add(id);
        }

        // 5. Remember for undo.
        var stack = GetUndoStack(session.UserId);
        stack.Add(record);
        if (stack.Count > MaxUndo)
            stack.RemoveAt(0);

        Status(session, Loc.GetString("kestrel-copypaste-pasted"));
    }

    #endregion

    #region Undo

    private void OnUndo(KestrelUndoPasteRequestEvent ev, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        if (!_admin.HasAdminFlag(session, AdminFlags.Mapping))
            return;

        var stack = GetUndoStack(session.UserId);
        if (stack.Count == 0)
        {
            Status(session, Loc.GetString("kestrel-copypaste-nothing-to-undo"));
            return;
        }

        var record = stack[^1];
        stack.RemoveAt(stack.Count - 1);

        if (!TryComp<MapGridComponent>(record.Grid, out var grid))
        {
            Status(session, Loc.GetString("kestrel-copypaste-undo-grid-gone"));
            return;
        }

        // Remove what the paste added.
        foreach (var uid in record.Spawned)
        {
            if (!TerminatingOrDeleted(uid))
                Del(uid);
        }

        foreach (var id in record.SpawnedDecals)
        {
            _decals.RemoveDecal(record.Grid, id);
        }

        // Put back what was there before.
        if (record.OldTiles.Count > 0)
            _map.SetTiles(record.Grid, grid, record.OldTiles);

        if (record.OldEntities != null)
        {
            if (LoadOnto(record.Grid, record.OldEntities, (_, xform) => (xform.LocalPosition, xform.LocalRotation)) == null)
                Status(session, Loc.GetString("kestrel-copypaste-undo-entities-failed"));
        }

        foreach (var decal in record.OldDecals)
        {
            _decals.TryAddDecal(decal, new EntityCoordinates(record.Grid, decal.Coordinates), out _);
        }

        Status(session, Loc.GetString("kestrel-copypaste-undone"));
    }

    #endregion

    #region Helpers

    private List<UndoRecord> GetUndoStack(NetUserId user)
    {
        if (!_undo.TryGetValue(user, out var stack))
        {
            stack = new List<UndoRecord>();
            _undo[user] = stack;
        }

        return stack;
    }

    private void Status(ICommonSession session, string message)
    {
        var undoCount = _undo.TryGetValue(session.UserId, out var stack) ? stack.Count : 0;
        RaiseNetworkEvent(new KestrelCopyPasteStatusEvent(message, undoCount), session);
    }

    private static Box2 BoundsOf(HashSet<Vector2i> tiles)
    {
        var minX = int.MaxValue;
        var minY = int.MaxValue;
        var maxX = int.MinValue;
        var maxY = int.MinValue;

        foreach (var t in tiles)
        {
            minX = Math.Min(minX, t.X);
            minY = Math.Min(minY, t.Y);
            maxX = Math.Max(maxX, t.X);
            maxY = Math.Max(maxY, t.Y);
        }

        // Decals are stored by their bottom-left corner, so pad by one tile to catch ones hanging over the edge.
        return new Box2(minX - 1, minY - 1, maxX + 1, maxY + 1);
    }

    /// <summary>
    /// Entities parented directly to the grid whose tile passes <paramref name="inArea"/>.
    /// Skips players and ghosts (and anything carrying one, like a locker with someone inside),
    /// and anything the map saver refuses to save.
    /// </summary>
    private HashSet<EntityUid> GetEntitiesInArea(EntityUid gridUid, MapGridComponent grid, Func<Vector2i, bool> inArea)
    {
        var result = new HashSet<EntityUid>();
        var children = Transform(gridUid).ChildEnumerator;

        while (children.MoveNext(out var child))
        {
            if (TerminatingOrDeleted(child))
                continue;

            if (MetaData(child).EntityPrototype?.MapSavable == false)
                continue;

            if (HasPlayer(child))
                continue;

            var index = _map.LocalToTile(gridUid, grid, Transform(child).Coordinates);
            if (inArea(index))
                result.Add(child);
        }

        return result;
    }

    private bool HasPlayer(EntityUid uid)
    {
        if (HasComp<ActorComponent>(uid))
            return true;

        var children = Transform(uid).ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            if (HasPlayer(child))
                return true;
        }

        return false;
    }

    private MappingDataNode Serialize(HashSet<EntityUid> roots)
    {
        var opts = SerializationOptions.Default with
        {
            // References to things outside the copy (e.g. a door linked to a button elsewhere) are dropped.
            MissingEntityBehaviour = MissingEntityBehaviour.Ignore,
            ErrorOnOrphan = false,
            LogAutoInclude = null,
            EntityExceptionBehaviour = EntityExceptionBehaviour.IgnoreEntityAndChildren,
            ExpectPreInit = roots.All(x => LifeStage(x) < EntityLifeStage.MapInitialized),
        };

        return _loader.SerializeEntitiesRecursive(roots, opts).Node;
    }

    /// <summary>
    /// Loads serialized entities and places each top-level one on <paramref name="gridUid"/>.
    /// This is the engine's own map-merge procedure, but targeting a grid instead of a map.
    /// Returns the placed top-level entities, or null if loading failed.
    /// </summary>
    private HashSet<EntityUid>? LoadOnto(
        EntityUid gridUid,
        MappingDataNode data,
        Func<EntityUid, TransformComponent, (Vector2 Pos, Angle Rot)> place)
    {
        var readEv = new BeforeEntityReadEvent();
        RaiseLocalEvent(readEv);

        // Copy the data: the deserializer may modify what it reads, and we want to paste the same clipboard again.
        var deserializer = new EntityDeserializer(
            _deps,
            data.Copy(),
            DeserializationOptions.Default,
            readEv.RenamedPrototypes,
            readEv.DeletedPrototypes);

        if (!deserializer.TryProcessData())
        {
            Log.Error("Copy/paste: could not read the copied entity data.");
            return null;
        }

        try
        {
            deserializer.CreateEntities();
        }
        catch (Exception e)
        {
            Log.Error($"Copy/paste: error while creating entities: {e}");
            _loader.Delete(deserializer.Result);
            return null;
        }

        var gridXform = Transform(gridUid);
        var roots = deserializer.Result.Orphans.ToHashSet();

        foreach (var uid in roots)
        {
            var xform = Transform(uid);
            var (pos, angle) = place(uid, xform);

            // unanchor: false keeps walls, pipes etc. anchored; they anchor to the new tile when they start up.
            _xform.SetCoordinates((uid, xform, MetaData(uid)),
                new EntityCoordinates(gridUid, pos),
                rotation: angle,
                unanchor: false,
                newParent: gridXform);
        }

        deserializer.Result.Orphans.Clear();

        try
        {
            deserializer.StartEntities();
        }
        catch (Exception e)
        {
            Log.Error($"Copy/paste: error while starting entities: {e}");
            _loader.Delete(deserializer.Result);
            return null;
        }

        // Match the target map: initialized maps run MapInit on new things, paused maps keep them paused.
        var mapId = gridXform.MapID;
        var initialized = _map.IsInitialized(mapId);
        var paused = _map.IsPaused(mapId);

        foreach (var uid in roots)
        {
            if (initialized)
                RecursiveMapInit(uid);

            RecursiveSetPaused(uid, paused);
        }

        return roots;
    }

    private void RecursiveMapInit(EntityUid uid)
    {
        var meta = MetaData(uid);
        if (meta.EntityLifeStage < EntityLifeStage.MapInitialized)
            EntityManager.RunMapInit(uid, meta);

        // Copy the child list first: MapInit can add children (e.g. filling a container).
        foreach (var child in ChildrenOf(uid))
        {
            RecursiveMapInit(child);
        }
    }

    private void RecursiveSetPaused(EntityUid uid, bool paused)
    {
        _meta.SetEntityPaused(uid, paused);

        foreach (var child in ChildrenOf(uid))
        {
            RecursiveSetPaused(child, paused);
        }
    }

    private List<EntityUid> ChildrenOf(EntityUid uid)
    {
        var list = new List<EntityUid>();
        var children = Transform(uid).ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            list.Add(child);
        }

        return list;
    }

    /// <summary>
    /// A pipe bend or T can't be truly mirrored, but a mirrored bend is just a bend turned a different way.
    /// Works out which turn gives the mirrored connections. Anything else keeps the plain flipped angle.
    /// </summary>
    private Angle FixPipeFlip(EntityUid uid, Angle original, Angle fallback, int rot)
    {
        if (!TryComp<NodeContainerComponent>(uid, out var container))
            return fallback;

        var pipes = container.Nodes.Values.OfType<PipeNode>().ToList();
        if (pipes.Count == 0)
            return fallback;

        var wanted = new List<PipeDirection>(pipes.Count);
        foreach (var pipe in pipes)
        {
            var current = pipe.OriginalPipeDirection.RotatePipeDirection(original.Theta);
            var mirrored = MirrorLeftRight(current);
            wanted.Add(mirrored.RotatePipeDirection(rot * Math.PI / 2));
        }

        for (var turn = 0; turn < 4; turn++)
        {
            var theta = turn * Math.PI / 2;
            var matches = true;

            for (var i = 0; i < pipes.Count; i++)
            {
                if (pipes[i].OriginalPipeDirection.RotatePipeDirection(theta) != wanted[i])
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
                return new Angle(theta);
        }

        return fallback;
    }

    private static PipeDirection MirrorLeftRight(PipeDirection dir)
    {
        var result = dir & ~(PipeDirection.East | PipeDirection.West);
        if ((dir & PipeDirection.East) != 0)
            result |= PipeDirection.West;
        if ((dir & PipeDirection.West) != 0)
            result |= PipeDirection.East;
        return result;
    }

    #endregion
}
