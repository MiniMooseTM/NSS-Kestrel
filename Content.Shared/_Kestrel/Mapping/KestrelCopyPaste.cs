// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Kestrel mapping copy/paste tool: network messages and the shared rotate/flip maths.
// The server does the actual copying; the client only draws a preview and sends requests.

using System.Numerics;
using Robust.Shared.Serialization;

namespace Content.Shared._Kestrel.Mapping;

/// <summary>
/// Client -> server: copy every tile, entity and decal in the box from <see cref="Min"/> to <see cref="Max"/>
/// (tile indices on <see cref="Grid"/>, both corners included).
/// </summary>
[Serializable, NetSerializable]
public sealed class KestrelCopyAreaRequestEvent : EntityEventArgs
{
    public NetEntity Grid;
    public Vector2i Min;
    public Vector2i Max;

    public KestrelCopyAreaRequestEvent(NetEntity grid, Vector2i min, Vector2i max)
    {
        Grid = grid;
        Min = min;
        Max = max;
    }
}

/// <summary>
/// Server -> client: what was copied, so the client can draw the paste preview.
/// </summary>
[Serializable, NetSerializable]
public sealed class KestrelCopyAreaResultEvent : EntityEventArgs
{
    public NetEntity SourceGrid;
    public Vector2i Size;

    /// <summary>Tiles (relative to the copy's bottom-left corner) the paste will cover.</summary>
    public List<Vector2i> Footprint;

    public int TileCount;
    public int EntityCount;
    public int DecalCount;

    public KestrelCopyAreaResultEvent(NetEntity sourceGrid, Vector2i size, List<Vector2i> footprint, int tileCount, int entityCount, int decalCount)
    {
        SourceGrid = sourceGrid;
        Size = size;
        Footprint = footprint;
        TileCount = tileCount;
        EntityCount = entityCount;
        DecalCount = decalCount;
    }
}

/// <summary>
/// Client -> server: paste the clipboard with its bottom-left corner at <see cref="Origin"/> on <see cref="Grid"/>.
/// </summary>
[Serializable, NetSerializable]
public sealed class KestrelPasteRequestEvent : EntityEventArgs
{
    public NetEntity Grid;
    public Vector2i Origin;

    /// <summary>Number of 90 degree counter-clockwise turns (0-3), applied after the flip.</summary>
    public int Rotation;

    /// <summary>Mirror left-right before rotating.</summary>
    public bool Flip;

    /// <summary>Refuse to paste onto any grid other than the one the copy came from.</summary>
    public bool LockToSource;

    public KestrelPasteRequestEvent(NetEntity grid, Vector2i origin, int rotation, bool flip, bool lockToSource)
    {
        Grid = grid;
        Origin = origin;
        Rotation = rotation;
        Flip = flip;
        LockToSource = lockToSource;
    }
}

/// <summary>Client -> server: undo the most recent paste.</summary>
[Serializable, NetSerializable]
public sealed class KestrelUndoPasteRequestEvent : EntityEventArgs
{
}

/// <summary>Server -> client: a status line for the tool window.</summary>
[Serializable, NetSerializable]
public sealed class KestrelCopyPasteStatusEvent : EntityEventArgs
{
    public string Message;
    public int UndoCount;

    public KestrelCopyPasteStatusEvent(string message, int undoCount)
    {
        Message = message;
        UndoCount = undoCount;
    }
}

/// <summary>
/// The rotate/flip maths, shared so the client preview and the server paste always agree.
/// A transform is "flip left-right first (optional), then turn counter-clockwise <c>rot</c> times".
/// Positions are measured in tiles from the bottom-left corner of the copied box.
/// </summary>
public static class KestrelCopyTransform
{
    /// <summary>Size of the box after rotating.</summary>
    public static Vector2i Size(Vector2i size, int rot)
    {
        return (rot & 1) == 0 ? size : new Vector2i(size.Y, size.X);
    }

    /// <summary>Moves a point inside a box of <paramref name="size"/> to where it ends up after the transform.</summary>
    public static Vector2 Point(Vector2 p, Vector2i size, int rot, bool flip)
    {
        float w = size.X;
        float h = size.Y;
        var x = p.X;
        var y = p.Y;

        if (flip)
            x = w - x;

        for (var i = 0; i < (rot & 3); i++)
        {
            // Quarter turn counter-clockwise, then shift so the box stays at the origin.
            var nx = h - y;
            var ny = x;
            x = nx;
            y = ny;
            (w, h) = (h, w);
        }

        return new Vector2(x, y);
    }

    /// <summary>Same as <see cref="Point"/> but for a whole tile (uses the tile's centre).</summary>
    public static Vector2i Tile(Vector2i tile, Vector2i size, int rot, bool flip)
    {
        var c = Point(new Vector2(tile.X + 0.5f, tile.Y + 0.5f), size, rot, flip);
        return new Vector2i((int) MathF.Floor(c.X), (int) MathF.Floor(c.Y));
    }

    /// <summary>
    /// New facing for an entity or decal. SS14 angles: 0 = south, +90 degrees = east (counter-clockwise).
    /// Mirroring left-right turns an angle a into -a.
    /// </summary>
    public static Angle Rotation(Angle angle, int rot, bool flip)
    {
        var theta = flip ? -angle.Theta : angle.Theta;
        theta += (rot & 3) * Math.PI / 2;
        return new Angle(theta).Reduced();
    }

    /// <summary>The transform after one more clockwise turn (matches the spawn tool's rotate direction).</summary>
    public static (int Rot, bool Flip) TurnClockwise(int rot, bool flip) => ((rot + 3) & 3, flip);

    /// <summary>The transform after mirroring the current preview left-right.</summary>
    public static (int Rot, bool Flip) FlipHorizontal(int rot, bool flip) => ((4 - rot) & 3, !flip);

    /// <summary>The transform after mirroring the current preview top-bottom.</summary>
    public static (int Rot, bool Flip) FlipVertical(int rot, bool flip) => ((6 - rot) & 3, !flip);

    // How the engine draws a tile for each RotationMirroring value (0-7): which texture corner lands on
    // each tile corner (0 = bottom-left, 1 = bottom-right, 2 = top-right, 3 = top-left).
    // Worked out from the engine's tile renderer (Clyde.GridRendering WriteTileToBuffers).
    private static readonly int[][] TileLayouts =
    {
        new[] { 0, 1, 2, 3 },
        new[] { 3, 0, 1, 2 },
        new[] { 2, 3, 0, 1 },
        new[] { 1, 2, 3, 0 },
        new[] { 1, 0, 3, 2 },
        new[] { 0, 3, 2, 1 },
        new[] { 3, 2, 1, 0 },
        new[] { 2, 1, 0, 3 },
    };

    private static readonly Vector2[] Corners =
    {
        new(-1, -1), new(1, -1), new(1, 1), new(-1, 1),
    };

    /// <summary>
    /// New RotationMirroring byte for a tile so its texture turns and mirrors along with the paste.
    /// Only matters for tiles whose definition allows rotation/mirroring.
    /// </summary>
    public static byte TileRotationMirroring(byte current, int rot, bool flip)
    {
        if (current > 7)
            return current;

        var old = TileLayouts[current];
        var moved = new int[4];

        for (var corner = 0; corner < 4; corner++)
        {
            var v = Corners[corner];
            if (flip)
                v = new Vector2(-v.X, v.Y);
            for (var i = 0; i < (rot & 3); i++)
                v = new Vector2(-v.Y, v.X);

            moved[CornerIndex(v)] = old[corner];
        }

        for (var b = 0; b < TileLayouts.Length; b++)
        {
            var layout = TileLayouts[b];
            if (layout[0] == moved[0] && layout[1] == moved[1] && layout[2] == moved[2] && layout[3] == moved[3])
                return (byte) b;
        }

        return current;
    }

    private static int CornerIndex(Vector2 v)
    {
        if (v.Y < 0)
            return v.X < 0 ? 0 : 1;
        return v.X > 0 ? 2 : 3;
    }
}
