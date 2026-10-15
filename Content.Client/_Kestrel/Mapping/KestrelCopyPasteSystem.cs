// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Kestrel mapping copy/paste tool (client side): the tool window, mouse handling and the preview overlay.
// All real work happens on the server (Content.Server/_Kestrel/Mapping/KestrelCopyPasteSystem.cs).
//
// Controls while the tool is active:
//   Copy mode:  left-drag a box over the area.
//   Paste mode: left-click to paste (repeatable), middle-click rotate, Ctrl+middle-click flip, right-click stop.

using System.Numerics;
using Content.Client.Administration.Managers;
using Content.Shared._Kestrel.Mapping;
using Content.Shared.Administration;
using Content.Shared.Input;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Placement;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;

namespace Content.Client._Kestrel.Mapping;

public sealed class KestrelCopyPasteSystem : EntitySystem
{
    [Dependency] private readonly IClientAdminManager _admin = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly IPlacementManager _placement = default!;
    [Dependency] private readonly InputSystem _inputSystem = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    public enum ToolMode : byte
    {
        None,
        Copy,
        Paste,
    }

    public ToolMode Mode { get; private set; } = ToolMode.None;

    // Copy drag
    public EntityUid? DragGrid { get; private set; }
    public Vector2i DragStart { get; private set; }

    // What the server says we copied
    public bool HasClipboard { get; private set; }
    public EntityUid? SourceGrid { get; private set; }
    public Vector2i ClipSize { get; private set; }
    public List<Vector2i> Footprint { get; private set; } = new();

    // Paste options
    public int Rotation { get; private set; }
    public bool Flip { get; private set; }
    public bool LockToSource { get; set; }

    public int UndoCount { get; private set; }

    private KestrelCopyPasteWindow? _window;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<KestrelCopyAreaResultEvent>(OnCopyResult);
        SubscribeNetworkEvent<KestrelCopyPasteStatusEvent>(OnStatus);

        _overlays.AddOverlay(new KestrelCopyPasteOverlay(this, EntityManager, _eye, _input, _map, _xform));

        CommandBinds.Builder
            .Bind(EngineKeyFunctions.EditorPlaceObject, new PointerStateInputCmdHandler(OnLeftDown, OnLeftUp, true))
            .Bind(EngineKeyFunctions.EditorCancelPlace, new PointerStateInputCmdHandler(OnRightDown, OnPassUp, true))
            .Bind(EngineKeyFunctions.EditorRotateObject, new PointerStateInputCmdHandler(OnMiddleDown, OnPassUp, true))
            .Bind(ContentKeyFunctions.EditorFlipObject, new PointerStateInputCmdHandler(OnCtrlMiddleDown, OnPassUp, true))
            .Register<KestrelCopyPasteSystem>();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay<KestrelCopyPasteOverlay>();
        CommandBinds.Unregister<KestrelCopyPasteSystem>();
        _window?.Close();
        _window = null;
    }

    #region Window

    public void ToggleWindow()
    {
        if (_window is { IsOpen: true })
        {
            _window.Close();
            return;
        }

        if (!_admin.HasFlag(AdminFlags.Mapping))
        {
            Log.Warning("Copy/paste needs the Mapping admin permission.");
            return;
        }

        if (_window == null)
        {
            _window = new KestrelCopyPasteWindow();
            _window.CopyButton.OnPressed += _ => SetMode(Mode == ToolMode.Copy ? ToolMode.None : ToolMode.Copy);
            _window.PasteButton.OnPressed += _ => SetMode(Mode == ToolMode.Paste ? ToolMode.None : ToolMode.Paste);
            _window.RotateButton.OnPressed += _ => TurnClockwise();
            _window.FlipHButton.OnPressed += _ => FlipHorizontal();
            _window.FlipVButton.OnPressed += _ => FlipVertical();
            _window.UndoButton.OnPressed += _ => RaiseNetworkEvent(new KestrelUndoPasteRequestEvent());
            _window.LockCheckBox.OnToggled += args => LockToSource = args.Pressed;
            _window.OnClose += () => SetMode(ToolMode.None);
        }

        _window.OpenCentered();
        RefreshWindow();
    }

    private void RefreshWindow()
    {
        if (_window == null)
            return;

        _window.CopyButton.Pressed = Mode == ToolMode.Copy;
        _window.PasteButton.Pressed = Mode == ToolMode.Paste;
        _window.PasteButton.Disabled = !HasClipboard;
        _window.RotateButton.Disabled = !HasClipboard;
        _window.FlipHButton.Disabled = !HasClipboard;
        _window.FlipVButton.Disabled = !HasClipboard;
        _window.UndoButton.Disabled = UndoCount == 0;
        _window.UndoButton.Text = Loc.GetString("kestrel-copypaste-window-undo", ("count", UndoCount));
        _window.LockCheckBox.Pressed = LockToSource;
        _window.TransformLabel.Text = Loc.GetString("kestrel-copypaste-window-transform",
            ("degrees", ((4 - Rotation) & 3) * 90),
            ("flipped", Flip ? 1 : 0));
    }

    private void SetStatus(string text)
    {
        if (_window != null)
            _window.StatusLabel.Text = text;
    }

    private void SetHint(string text)
    {
        if (_window != null)
            _window.HintLabel.Text = text;
    }

    #endregion

    #region Modes and transform

    public void SetMode(ToolMode mode)
    {
        if (mode == ToolMode.Paste && !HasClipboard)
            mode = ToolMode.None;

        Mode = mode;
        DragGrid = null;

        if (mode == ToolMode.None)
        {
            _inputSystem.SetEntityContextActive();
        }
        else
        {
            // Stop the spawn/erase tool so it doesn't also react to our clicks.
            _placement.Clear();
            _input.Contexts.SetActiveContext("editor");
        }

        SetHint(mode switch
        {
            ToolMode.Copy => Loc.GetString("kestrel-copypaste-hint-copy"),
            ToolMode.Paste => Loc.GetString("kestrel-copypaste-hint-paste"),
            _ => string.Empty,
        });

        RefreshWindow();
    }

    private void TurnClockwise()
    {
        (Rotation, Flip) = KestrelCopyTransform.TurnClockwise(Rotation, Flip);
        RefreshWindow();
    }

    private void FlipHorizontal()
    {
        (Rotation, Flip) = KestrelCopyTransform.FlipHorizontal(Rotation, Flip);
        RefreshWindow();
    }

    private void FlipVertical()
    {
        (Rotation, Flip) = KestrelCopyTransform.FlipVertical(Rotation, Flip);
        RefreshWindow();
    }

    #endregion

    #region Mouse

    /// <summary>Grid and tile under the mouse right now, if any.</summary>
    public bool TryGetHoveredTile(out EntityUid gridUid, out MapGridComponent? grid, out Vector2i tile)
    {
        var mapPos = _eye.PixelToMap(_input.MouseScreenPosition);
        return TryGetTile(mapPos, out gridUid, out grid, out tile);
    }

    private bool TryGetTile(MapCoordinates mapPos, out EntityUid gridUid, out MapGridComponent? grid, out Vector2i tile)
    {
        tile = default;
        grid = null;
        gridUid = default;

        if (mapPos.MapId == MapId.Nullspace)
            return false;

        if (!_map.TryFindGridAt(mapPos, out gridUid, out var found))
            return false;

        grid = found;
        tile = _map.TileIndicesFor(gridUid, found, mapPos);
        return true;
    }

    /// <summary>Bottom-left tile the paste would land on, given the tile under the mouse (preview is centred on the mouse).</summary>
    public Vector2i PasteOrigin(Vector2i hovered)
    {
        var size = KestrelCopyTransform.Size(ClipSize, Rotation);
        return hovered - new Vector2i(size.X / 2, size.Y / 2);
    }

    /// <summary>Whether pasting onto this grid is allowed by the lock setting.</summary>
    public bool CanPasteOn(EntityUid grid)
    {
        return !LockToSource || SourceGrid == null || SourceGrid == grid;
    }

    private bool OnLeftDown(ICommonSession? session, EntityCoordinates coords, EntityUid uid)
    {
        if (Mode == ToolMode.None)
            return false;

        if (!TryGetTile(_xform.ToMapCoordinates(coords), out var gridUid, out _, out var tile))
            return true;

        if (Mode == ToolMode.Copy)
        {
            DragGrid = gridUid;
            DragStart = tile;
            return true;
        }

        // Paste
        if (!CanPasteOn(gridUid))
        {
            SetStatus(Loc.GetString("kestrel-copypaste-wrong-grid"));
            return true;
        }

        RaiseNetworkEvent(new KestrelPasteRequestEvent(GetNetEntity(gridUid), PasteOrigin(tile), Rotation, Flip, LockToSource));
        return true;
    }

    private bool OnLeftUp(ICommonSession? session, EntityCoordinates coords, EntityUid uid)
    {
        if (Mode == ToolMode.None)
            return false;

        if (Mode != ToolMode.Copy || DragGrid is not { } dragGrid)
            return true;

        DragGrid = null;

        // Finish the box on the same grid it started on, even if the mouse drifted off it.
        var mapPos = _xform.ToMapCoordinates(coords);
        if (!TryComp<MapGridComponent>(dragGrid, out var grid) || mapPos.MapId != Transform(dragGrid).MapID)
            return true;

        var end = _map.TileIndicesFor(dragGrid, grid, mapPos);
        RaiseNetworkEvent(new KestrelCopyAreaRequestEvent(GetNetEntity(dragGrid), DragStart, end));
        SetStatus(Loc.GetString("kestrel-copypaste-copying"));
        return true;
    }

    private bool OnRightDown(ICommonSession? session, EntityCoordinates coords, EntityUid uid)
    {
        if (Mode == ToolMode.None)
            return false;

        SetMode(ToolMode.None);
        return true;
    }

    private bool OnMiddleDown(ICommonSession? session, EntityCoordinates coords, EntityUid uid)
    {
        if (Mode != ToolMode.Paste)
            return false;

        TurnClockwise();
        return true;
    }

    private bool OnCtrlMiddleDown(ICommonSession? session, EntityCoordinates coords, EntityUid uid)
    {
        if (Mode != ToolMode.Paste)
            return false;

        FlipHorizontal();
        return true;
    }

    private bool OnPassUp(ICommonSession? session, EntityCoordinates coords, EntityUid uid)
    {
        return Mode != ToolMode.None;
    }

    #endregion

    #region Network

    private void OnCopyResult(KestrelCopyAreaResultEvent ev)
    {
        HasClipboard = true;
        SourceGrid = GetEntity(ev.SourceGrid);
        ClipSize = ev.Size;
        Footprint = ev.Footprint;
        Rotation = 0;
        Flip = false;

        // Straight into paste mode, since that's almost always the next step.
        SetMode(ToolMode.Paste);
    }

    private void OnStatus(KestrelCopyPasteStatusEvent ev)
    {
        UndoCount = ev.UndoCount;
        SetStatus(ev.Message);
        RefreshWindow();
    }

    #endregion
}
