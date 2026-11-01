// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.AlertLevel;
using Content.Server.Light.EntitySystems;
using Content.Server.Station.Systems;
using Content.Shared._Kestrel.Light;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Station.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Color = Robust.Shared.Maths.Color; // same alias EmergencyLightSystem uses, avoids a name clash

namespace Content.Server._Kestrel.Light;

/// <summary>
/// Recolours alert lights to match their station's alert level.
/// </summary>
/// <remarks>
/// How it works:
/// A powered light takes its colour from the tube or bulb inside it. So instead of fighting the
/// light system, we recolour the bulb, then ask the light to refresh (SetState with its current
/// on/off), which copies the bulb's colour onto the light.
/// The colour for each level is the level's emergencyLightColor from AlertLevels/alert_levels.yml,
/// the same colour emergency lights use; on "normal" levels (green by default) it's NormalColor.
/// This runs:
///  - whenever a station's alert level changes (for lights on that station),
///  - when the light first appears on a running map,
///  - when a new bulb is put in (so a fresh bulb doesn't stay white on red alert).
/// A bulb taken out keeps the alert colour; put it in a normal light and it'll glow that colour.
/// </remarks>
public sealed class KestrelAlertLightSystem : EntitySystem
{
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly LightBulbSystem _bulb = default!;
    [Dependency] private readonly PoweredLightSystem _light = default!;
    [Dependency] private readonly StationSystem _station = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AlertLevelChangedEvent>(OnAlertLevelChanged);
        SubscribeLocalEvent<KestrelAlertLightComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<KestrelAlertLightComponent, EntInsertedIntoContainerMessage>(OnBulbInserted);
    }

    private void OnAlertLevelChanged(AlertLevelChangedEvent ev)
    {
        var query = EntityQueryEnumerator<KestrelAlertLightComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var alertLight, out var xform))
        {
            if (CompOrNull<StationMemberComponent>(xform.GridUid)?.Station != ev.Station)
                continue;

            ApplyColour((uid, alertLight), ev.Station, ev.AlertLevel);
        }
    }

    private void OnMapInit(Entity<KestrelAlertLightComponent> ent, ref MapInitEvent args)
    {
        RefreshFromStation(ent);
    }

    private void OnBulbInserted(Entity<KestrelAlertLightComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != SharedPoweredLightSystem.LightBulbContainer)
            return;

        RefreshFromStation(ent);
    }

    private void RefreshFromStation(Entity<KestrelAlertLightComponent> ent)
    {
        if (_station.GetOwningStation(ent) is not { } station
            || !TryComp<AlertLevelComponent>(station, out var alert))
        {
            return;
        }

        ApplyColour(ent, station, alert.CurrentLevel);
    }

    private void ApplyColour(Entity<KestrelAlertLightComponent> ent, EntityUid station, string level)
    {
        var colour = ent.Comp.NormalColor;
        if (!ent.Comp.NormalLevels.Contains(level)
            && TryComp<AlertLevelComponent>(station, out var alert)
            && alert.AlertLevels != null
            && alert.AlertLevels.Levels.TryGetValue(level, out var details))
        {
            colour = details.EmergencyLightColor;
        }

        if (!TryComp<PoweredLightComponent>(ent, out var light)
            || _light.GetBulb(ent, light) is not { } bulb)
        {
            return;
        }

        _bulb.SetColor(bulb, colour);
        _light.SetState(ent, light.On, light); // refresh: copies the bulb's colour onto the light
        _appearance.SetData(ent, KestrelAlertLightVisuals.Color, colour); // re-tints the glow picture
    }
}
