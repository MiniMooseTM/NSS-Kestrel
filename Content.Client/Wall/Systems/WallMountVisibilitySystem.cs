// SPDX-License-Identifier: AGPL-3.0-or-later
// Kestrel: ported early from upstream space-wizards/space-station-14 (MIT), commit 2e9280f.
// Kept at the same path and nearly identical so a future upstream merge lines up cleanly.
// Kestrel changes are marked "Kestrel" below.

using Content.Shared.CCVar;
using Content.Shared.Tag;
using Content.Shared.Wall;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Client.Wall.Systems;

/// <summary>
/// Manages the directional visibility overlay for wall-mounted entities.
/// </summary>
public sealed class WallMountVisibilitySystem : EntitySystem
{
    // Kestrel: classic readonly dependencies and queries, matching the rest of our codebase.
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly TagSystem _tag = default!; // Kestrel

    private EntityQuery<SpriteComponent> _spriteQuery;
    private EntityQuery<WallComponent> _wallQuery;

    // Kestrel: many Goob/RMC walls don't inherit BaseStructureWall, so they never get the new
    // Wall component. They all carry the Wall tag, so count that too.
    private static readonly ProtoId<TagPrototype> WallTag = "Wall";

    private WallMountVisibilityOverlay _overlayInstance = default!;

    /// <summary>
    /// Whether directional visibility is currently enabled.
    /// </summary>
    public bool DirectionalVisibilityEnabled = true;

    /// <summary>
    /// Whether wall-mount visibility changes fade smoothly or snap instantly.
    /// </summary>
    public bool FadeEnabled = true;

    public override void Initialize()
    {
        base.Initialize();

        _spriteQuery = GetEntityQuery<SpriteComponent>();
        _wallQuery = GetEntityQuery<WallComponent>();

        // Kestrel: subscribed here instead of with upstream's [SubscribeLocalEvent] attributes.
        SubscribeLocalEvent<WallMountComponent, ComponentShutdown>(OnWallMountShutdown);
        SubscribeLocalEvent<WallMountComponent, AfterAutoHandleStateEvent>(OnWallMountAfterHandleState);

        _overlayInstance = new WallMountVisibilityOverlay();

        Subs.CVar(_cfg, CCVars.WallMountDirectionalVisibility, OnDirectionalVisibilityChanged, true);
        Subs.CVar(_cfg, CCVars.WallMountFade, OnFadeChanged, true);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlay.RemoveOverlay(_overlayInstance);
    }

    private void OnDirectionalVisibilityChanged(bool enabled)
    {
        DirectionalVisibilityEnabled = enabled;

        if (enabled)
            _overlay.AddOverlay(_overlayInstance);
        else
        {
            _overlay.RemoveOverlay(_overlayInstance);
            _overlayInstance.RestoreAll();
        }
    }

    private void OnFadeChanged(bool enabled)
    {
        FadeEnabled = enabled;
    }

    /// <summary>
    /// Makes the entity visible again on component shutdown.
    /// </summary>
    private void OnWallMountShutdown(Entity<WallMountComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        if (!_spriteQuery.TryComp(ent, out var sprite))
            return;

        _sprite.SetVisible((ent, sprite), true);
    }

    /// <summary>
    /// Makes the entity visible again if directional visibility is disabled for this mount.
    /// </summary>
    private void OnWallMountAfterHandleState(Entity<WallMountComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (ent.Comp.DirectionalVisibility)
            return;

        if (!_spriteQuery.TryComp(ent, out var sprite))
            return;

        _sprite.SetVisible((ent, sprite), true);
    }

    /// <summary>
    /// Checks whether the tile contains any anchored blocking entity.
    /// </summary>
    public bool IsTileBlocked(Entity<MapGridComponent> grid, Vector2i tile)
    {
        var enumerator = _map.GetAnchoredEntities(grid.Owner, grid, tile);
        while (enumerator.MoveNext(out var anchored))
        {
            // Kestrel: Wall tag counts as well as the Wall component.
            if (!_wallQuery.HasComp(anchored.Value) && !_tag.HasTag(anchored.Value, WallTag))
                continue;

            return true;
        }
        return false;
    }
}
