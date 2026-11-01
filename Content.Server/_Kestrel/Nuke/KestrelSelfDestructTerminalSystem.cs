// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Nuke;
using Content.Shared._Kestrel.Nuke;
using Content.Shared.Nuke;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server._Kestrel.Nuke;

/// <summary>
/// Runs the self-destruct terminal's extras on top of the normal nuke.
/// </summary>
/// <remarks>
/// How it works:
/// 1. Tools only. Bolting and unbolting are done with tools through the construction graph
///    (Recipes/Construction/Graphs/structures/self_destruct.yml): wrench to bolt it down; welder,
///    wrench, crowbar to remove it. The nuke's menu also has an "anchor" button, which NukeSystem
///    would act on directly. Before any button message reaches NukeSystem, the engine raises
///    BoundUserInterfaceMessageAttempt on the entity, which can be cancelled. We cancel it for the
///    anchor button and show a popup saying which tools to use instead.
/// 2. Floor lights. While the terminal is bolted down there's one floor-light entity (a 3x3 picture)
///    under it. It's spawned when the terminal is bolted (or starts the round bolted) and deleted
///    when it's unbolted or deleted.
/// 3. Pictures. Twice a second we read the nuke's status and turn it into one of four states
///    (Safe, Ready, Armed, Detonating). When it changes, we set that as appearance data on the
///    terminal and the floor; their YAML visualizers pick the matching pictures.
///    Checking twice a second is simpler than hooking every place NukeSystem changes status,
///    and there's only ever one or two of these on a map.
/// </remarks>
public sealed class KestrelSelfDestructTerminalSystem : EntitySystem
{
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<KestrelSelfDestructTerminalComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<KestrelSelfDestructTerminalComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<KestrelSelfDestructTerminalComponent, AnchorStateChangedEvent>(OnAnchorChanged);
        SubscribeLocalEvent<KestrelSelfDestructTerminalComponent, BoundUserInterfaceMessageAttempt>(OnUiMessageAttempt);
    }

    private void OnMapInit(Entity<KestrelSelfDestructTerminalComponent> ent, ref MapInitEvent args)
    {
        if (Transform(ent).Anchored)
            SpawnFloor(ent);
    }

    private void OnAnchorChanged(Entity<KestrelSelfDestructTerminalComponent> ent, ref AnchorStateChangedEvent args)
    {
        // Mapping (paused maps) doesn't get floor lights; they appear when the round starts.
        if (MetaData(ent).EntityLifeStage < EntityLifeStage.MapInitialized)
            return;

        if (args.Anchored)
            SpawnFloor(ent);
        else
            DeleteFloor(ent);
    }

    private void OnShutdown(Entity<KestrelSelfDestructTerminalComponent> ent, ref ComponentShutdown args)
    {
        DeleteFloor(ent);
    }

    private void SpawnFloor(Entity<KestrelSelfDestructTerminalComponent> ent)
    {
        if (ent.Comp.Floor is { } existing && !TerminatingOrDeleted(existing))
            return;

        ent.Comp.Floor = Spawn(ent.Comp.FloorPrototype, Transform(ent).Coordinates);
        ent.Comp.LastState = null; // force the next update to set the new floor's picture
    }

    private void DeleteFloor(Entity<KestrelSelfDestructTerminalComponent> ent)
    {
        if (ent.Comp.Floor is { } floor && !TerminatingOrDeleted(floor))
            QueueDel(floor);

        ent.Comp.Floor = null;
    }

    private void OnUiMessageAttempt(Entity<KestrelSelfDestructTerminalComponent> ent, ref BoundUserInterfaceMessageAttempt args)
    {
        if (args.Message is not NukeAnchorMessage)
            return;

        args.Cancel();
        var message = Transform(ent).Anchored ? "kestrel-self-destruct-bolted" : "kestrel-self-destruct-loose";
        _popup.PopupEntity(Loc.GetString(message), ent, args.Actor, PopupType.MediumCaution);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<KestrelSelfDestructTerminalComponent, NukeComponent>();
        while (query.MoveNext(out var uid, out var terminal, out var nuke))
        {
            if (now < terminal.NextUpdate)
                continue;

            terminal.NextUpdate = now + terminal.UpdateInterval;

            var state = GetState(nuke);
            if (state == terminal.LastState)
                continue;

            terminal.LastState = state;
            _appearance.SetData(uid, KestrelSelfDestructVisuals.State, state);

            if (terminal.Floor is { } floor && !TerminatingOrDeleted(floor))
                _appearance.SetData(floor, KestrelSelfDestructVisuals.State, state);
        }
    }

    /// <summary>
    /// Maps the nuke's own status onto Aurora's four pictures.
    /// </summary>
    private static KestrelSelfDestructState GetState(NukeComponent nuke)
    {
        if (nuke.PlayedAlertSound)
            return KestrelSelfDestructState.Detonating;

        return nuke.Status switch
        {
            NukeStatus.ARMED => KestrelSelfDestructState.Armed,
            NukeStatus.AWAIT_ARM => KestrelSelfDestructState.Ready, // code accepted: "safety off"
            _ => KestrelSelfDestructState.Safe, // no disk, waiting for code, or cooling down
        };
    }
}
