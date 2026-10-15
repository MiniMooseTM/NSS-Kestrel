// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Draws the copy selection box and the paste preview for the Kestrel copy/paste tool.

using System.Numerics;
using Content.Shared._Kestrel.Mapping;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Client._Kestrel.Mapping;

public sealed class KestrelCopyPasteOverlay : Overlay
{
    private static readonly Color SelectFill = Color.DodgerBlue.WithAlpha(0.20f);
    private static readonly Color SelectEdge = Color.DodgerBlue.WithAlpha(0.9f);
    private static readonly Color PasteFill = Color.LimeGreen.WithAlpha(0.25f);
    private static readonly Color PasteEdge = Color.LimeGreen.WithAlpha(0.9f);
    private static readonly Color BlockedFill = Color.Red.WithAlpha(0.25f);
    private static readonly Color BlockedEdge = Color.Red.WithAlpha(0.9f);

    private readonly KestrelCopyPasteSystem _system;
    private readonly IEntityManager _entMan;
    private readonly IEyeManager _eye;
    private readonly IInputManager _input;
    private readonly SharedMapSystem _map;
    private readonly SharedTransformSystem _xform;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public KestrelCopyPasteOverlay(
        KestrelCopyPasteSystem system,
        IEntityManager entMan,
        IEyeManager eye,
        IInputManager input,
        SharedMapSystem map,
        SharedTransformSystem xform)
    {
        _system = system;
        _entMan = entMan;
        _eye = eye;
        _input = input;
        _map = map;
        _xform = xform;
        ZIndex = 1000;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_system.Mode == KestrelCopyPasteSystem.ToolMode.None)
            return;

        var mapPos = _eye.PixelToMap(_input.MouseScreenPosition);
        if (mapPos.MapId != args.MapId)
            return;

        var handle = args.WorldHandle;

        if (_system.Mode == KestrelCopyPasteSystem.ToolMode.Copy)
            DrawCopy(handle, mapPos);
        else
            DrawPaste(handle, mapPos);

        handle.SetTransform(Matrix3x2.Identity);
    }

    private void DrawCopy(DrawingHandleWorld handle, MapCoordinates mapPos)
    {
        // While dragging: box from the start tile to the tile under the mouse, on the grid the drag started on.
        if (_system.DragGrid is { } dragGrid && _entMan.TryGetComponent<MapGridComponent>(dragGrid, out var dragGridComp))
        {
            var end = _map.TileIndicesFor(dragGrid, dragGridComp, mapPos);
            var start = _system.DragStart;
            var box = new Box2(
                Math.Min(start.X, end.X),
                Math.Min(start.Y, end.Y),
                Math.Max(start.X, end.X) + 1,
                Math.Max(start.Y, end.Y) + 1);

            handle.SetTransform(_xform.GetWorldMatrix(dragGrid));
            handle.DrawRect(box, SelectFill);
            handle.DrawRect(box, SelectEdge, false);
            return;
        }

        // Not dragging yet: outline the hovered tile.
        if (!_system.TryGetHoveredTile(out var gridUid, out _, out var tile))
            return;

        handle.SetTransform(_xform.GetWorldMatrix(gridUid));
        handle.DrawRect(new Box2(tile.X, tile.Y, tile.X + 1, tile.Y + 1), SelectEdge, false);
    }

    private void DrawPaste(DrawingHandleWorld handle, MapCoordinates mapPos)
    {
        if (!_system.HasClipboard || !_system.TryGetHoveredTile(out var gridUid, out _, out var hovered))
            return;

        var allowed = _system.CanPasteOn(gridUid);
        var fill = allowed ? PasteFill : BlockedFill;
        var edge = allowed ? PasteEdge : BlockedEdge;

        var origin = _system.PasteOrigin(hovered);
        var size = KestrelCopyTransform.Size(_system.ClipSize, _system.Rotation);

        handle.SetTransform(_xform.GetWorldMatrix(gridUid));

        foreach (var rel in _system.Footprint)
        {
            var t = origin + KestrelCopyTransform.Tile(rel, _system.ClipSize, _system.Rotation, _system.Flip);
            handle.DrawRect(new Box2(t.X, t.Y, t.X + 1, t.Y + 1), fill);
        }

        handle.DrawRect(new Box2(origin.X, origin.Y, origin.X + size.X, origin.Y + size.Y), edge, false);

        // Small marker on the copy's original bottom-left corner so you can see how it's turned/flipped.
        var corner = KestrelCopyTransform.Point(new Vector2(0.25f, 0.25f), _system.ClipSize, _system.Rotation, _system.Flip);
        var c = (Vector2) origin + corner;
        handle.DrawRect(new Box2(c.X - 0.15f, c.Y - 0.15f, c.X + 0.15f, c.Y + 0.15f), edge);
    }
}
