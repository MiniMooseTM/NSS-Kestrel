// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Mech.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;

namespace Content.Server._Kestrel.Mech;

/// <summary>
/// Plays a mech's entry sound when someone is put in its pilot seat.
/// </summary>
public sealed class MechEntrySoundSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MechEntrySoundComponent, EntInsertedIntoContainerMessage>(OnInserted);
    }

    private void OnInserted(EntityUid uid, MechEntrySoundComponent comp, EntInsertedIntoContainerMessage args)
    {
        if (!TryComp<MechComponent>(uid, out var mech))
            return;

        if (args.Container.ID != mech.PilotSlotId)
            return;

        _audio.PlayPvs(comp.Sound, uid);
    }
}
