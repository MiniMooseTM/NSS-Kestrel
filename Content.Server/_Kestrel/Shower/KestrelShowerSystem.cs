// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Atmos.EntitySystems;
using Content.Server.Decals;
using Content.Server.Fluids.EntitySystems;
using Content.Server.Temperature.Systems;
using Content.Shared._Kestrel.Shower;
using Content.Shared.Atmos.Components;
using Content.Shared.Audio;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Forensics.Components;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Temperature.Components;
using Content.Shared.Tools.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server._Kestrel.Shower;

/// <summary>
/// Runs showers: turning on and off, steam, washing whoever's underneath, and the temperature valve.
/// </summary>
/// <remarks>
/// While a shower is on, every <see cref="KestrelShowerComponent.WashInterval"/> it washes its own tile:
/// - people and objects standing there: fire put out, forensic evidence (fingerprints, fibers,
///   cleanable DNA, residue) cleared from them and everything they're holding or wearing;
/// - people also have their body temperature pulled towards the water's temperature;
/// - the floor: puddles and cleanable decals (dirt, blood splats) removed.
/// </remarks>
public sealed class KestrelShowerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedAmbientSoundSystem _ambient = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly DecalSystem _decals = default!;
    [Dependency] private readonly FlammableSystem _flammable = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly PuddleSystem _puddle = default!;
    [Dependency] private readonly TemperatureSystem _temperature = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;

    private readonly HashSet<EntityUid> _underShower = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<KestrelShowerComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<KestrelShowerComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<KestrelShowerComponent, KestrelShowerTemperatureDoAfterEvent>(OnTemperatureDoAfter);
        SubscribeLocalEvent<KestrelShowerComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<KestrelShowerComponent, ComponentShutdown>(OnShutdown);
    }

    #region Interaction

    private void OnActivate(Entity<KestrelShowerComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        SetOn(ent, !ent.Comp.On);
        args.Handled = true;
    }

    private void OnInteractUsing(Entity<KestrelShowerComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !_tool.HasQuality(args.Used, ent.Comp.WrenchQuality))
            return;

        // Start a do-after; the valve only changes if the user finishes it (OnTemperatureDoAfter).
        args.Handled = _tool.UseTool(
            args.Used,
            args.User,
            ent,
            ent.Comp.WrenchDelay,
            new[] { ent.Comp.WrenchQuality },
            new KestrelShowerTemperatureDoAfterEvent());

        if (args.Handled)
            _popup.PopupEntity(Loc.GetString("kestrel-shower-wrench-start"), ent, args.User);
    }

    private void OnTemperatureDoAfter(Entity<KestrelShowerComponent> ent, ref KestrelShowerTemperatureDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        // Cycle normal -> boiling -> freezing -> normal
        ent.Comp.Temperature = ent.Comp.Temperature switch
        {
            KestrelShowerTemperature.Normal => KestrelShowerTemperature.Boiling,
            KestrelShowerTemperature.Boiling => KestrelShowerTemperature.Freezing,
            _ => KestrelShowerTemperature.Normal,
        };

        _popup.PopupEntity(
            Loc.GetString("kestrel-shower-wrench-done", ("setting", SettingName(ent.Comp.Temperature))),
            ent,
            args.User);

        // Steam follows the new temperature: start building it or let it fade.
        if (ent.Comp.On)
            ent.Comp.SteamChangeAt = _timing.CurTime + (IsHot(ent.Comp) ? ent.Comp.SteamDelay : ent.Comp.SteamLinger);

        args.Handled = true;
    }

    private void OnExamined(Entity<KestrelShowerComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString(ent.Comp.On ? "kestrel-shower-examine-on" : "kestrel-shower-examine-off"));
        args.PushMarkup(Loc.GetString("kestrel-shower-examine-setting", ("setting", SettingName(ent.Comp.Temperature))));
    }

    private void OnShutdown(Entity<KestrelShowerComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Steam is { } steam && !TerminatingOrDeleted(steam))
            QueueDel(steam);
    }

    #endregion

    #region On and off

    private void SetOn(Entity<KestrelShowerComponent> ent, bool on)
    {
        if (ent.Comp.On == on)
            return;

        var now = _timing.CurTime;
        ent.Comp.On = on;

        _appearance.SetData(ent, KestrelShowerVisuals.On, on);
        _ambient.SetAmbience(ent, on);
        _audio.PlayPvs(on ? ent.Comp.StartSound : ent.Comp.StopSound, ent);

        if (on)
        {
            ent.Comp.TurnOffAt = now + ent.Comp.AutoOffDelay;
            ent.Comp.NextWash = now; // wash straight away, like Aurora
            ent.Comp.SteamChangeAt = IsHot(ent.Comp) ? now + ent.Comp.SteamDelay : null;
        }
        else
        {
            // Let any steam linger, then clear it.
            ent.Comp.SteamChangeAt = ent.Comp.Steam != null ? now + ent.Comp.SteamLinger : null;
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<KestrelShowerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var shower, out var xform))
        {
            var ent = new Entity<KestrelShowerComponent>(uid, shower);

            if (shower.SteamChangeAt is { } steamAt && now >= steamAt)
                UpdateSteam(ent, xform);

            if (!shower.On)
                continue;

            if (now >= shower.TurnOffAt)
            {
                SetOn(ent, false);
                continue;
            }

            if (now < shower.NextWash)
                continue;

            shower.NextWash = now + shower.WashInterval;
            WashTile(ent, xform, now);
        }
    }

    /// <summary>
    /// Called when a pending steam change is due: adds steam if hot water is running, otherwise removes it.
    /// </summary>
    private void UpdateSteam(Entity<KestrelShowerComponent> ent, TransformComponent xform)
    {
        ent.Comp.SteamChangeAt = null;
        var wantSteam = ent.Comp.On && IsHot(ent.Comp);

        if (wantSteam && ent.Comp.Steam == null)
        {
            ent.Comp.Steam = Spawn(ent.Comp.SteamPrototype, xform.Coordinates);
        }
        else if (!wantSteam && ent.Comp.Steam is { } steam)
        {
            QueueDel(steam);
            ent.Comp.Steam = null;
        }
    }

    #endregion

    #region Washing

    private void WashTile(Entity<KestrelShowerComponent> ent, TransformComponent xform, TimeSpan now)
    {
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);

        // Everything standing or lying on the shower's tile (people and loose items, not walls or floors).
        _underShower.Clear();
        _lookup.GetLocalEntitiesIntersecting(gridUid, tile, _underShower, flags: LookupFlags.Dynamic | LookupFlags.Sundries);

        var showMessage = now >= ent.Comp.NextTemperatureMessage;
        foreach (var target in _underShower)
        {
            if (target == ent.Owner || target == ent.Comp.Steam)
                continue;

            WashEntity(target);

            if (HasComp<MobStateComponent>(target))
            {
                ApplyWaterTemperature(ent, target, showMessage);
                // Also wash what they're holding and wearing.
                foreach (var item in _inventory.GetHandOrInventoryEntities(target))
                {
                    WashEntity(item);
                }
            }
        }

        if (showMessage)
            ent.Comp.NextTemperatureMessage = now + ent.Comp.TemperatureMessageInterval;

        WashFloor(gridUid, grid, tile);
    }

    /// <summary>Puts out fire and clears forensic evidence, the same fields a soap/cleaning do-after clears.</summary>
    private void WashEntity(EntityUid target)
    {
        if (TryComp<FlammableComponent>(target, out var flammable) && flammable.OnFire)
            _flammable.Extinguish(target, flammable);

        if (!TryComp<ForensicsComponent>(target, out var forensics))
            return;

        forensics.Fingerprints.Clear();
        forensics.Fibers.Clear();
        forensics.Residues.Clear();
        if (forensics.CanDnaBeCleaned)
            forensics.DNAs.Clear();

        Dirty(target, forensics);
    }

    /// <summary>Removes the puddle and cleanable decals (dirt, blood splats) on the shower's tile.</summary>
    private void WashFloor(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        if (_puddle.TryGetPuddle(_map.GetTileRef(gridUid, grid, tile), out var puddle))
            QueueDel(puddle);

        var center = new System.Numerics.Vector2(tile.X + 0.5f, tile.Y + 0.5f);
        foreach (var (id, _) in _decals.GetDecalsInRange(gridUid, center, 0.5f, decal => decal.Cleanable))
        {
            _decals.RemoveDecal(gridUid, id);
        }
    }

    /// <summary>
    /// Nudges a body's temperature towards the water's, at most <see cref="KestrelShowerComponent.MaxTemperatureChangePerTick"/>
    /// per tick. Boiling water keeps heating until the normal temperature system starts burning them.
    /// </summary>
    private void ApplyWaterTemperature(Entity<KestrelShowerComponent> ent, EntityUid target, bool showMessage)
    {
        if (!TryComp<TemperatureComponent>(target, out var temp))
            return;

        var water = WaterTemperature(ent.Comp);
        var change = Math.Clamp(water - temp.CurrentTemperature,
            -ent.Comp.MaxTemperatureChangePerTick,
            ent.Comp.MaxTemperatureChangePerTick);

        _temperature.ChangeHeat(target, change * _temperature.GetHeatCapacity(target, temp), temperature: temp);

        if (!showMessage)
            return;

        if (ent.Comp.Temperature == KestrelShowerTemperature.Boiling)
            _popup.PopupEntity(Loc.GetString("kestrel-shower-boiling"), target, target, PopupType.MediumCaution);
        else if (ent.Comp.Temperature == KestrelShowerTemperature.Freezing)
            _popup.PopupEntity(Loc.GetString("kestrel-shower-freezing"), target, target, PopupType.SmallCaution);
    }

    #endregion

    private static float WaterTemperature(KestrelShowerComponent shower)
    {
        return shower.WaterTemperatures.TryGetValue(shower.Temperature, out var t) ? t : 310.15f;
    }

    private static bool IsHot(KestrelShowerComponent shower)
    {
        return WaterTemperature(shower) >= shower.SteamMinimumTemperature;
    }

    private string SettingName(KestrelShowerTemperature setting)
    {
        return Loc.GetString(setting switch
        {
            KestrelShowerTemperature.Boiling => "kestrel-shower-setting-boiling",
            KestrelShowerTemperature.Freezing => "kestrel-shower-setting-freezing",
            _ => "kestrel-shower-setting-normal",
        });
    }
}
