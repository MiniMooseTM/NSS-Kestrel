// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Kestrel.Shower;
using Content.Shared.Tools;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server._Kestrel.Shower;

/// <summary>
/// A shower: click to turn it on, stand under it to get washed. A wrench sets the water temperature.
/// Ported from Aurorastation's /obj/structure/machinery/shower.
/// </summary>
[RegisterComponent, Access(typeof(KestrelShowerSystem))]
public sealed partial class KestrelShowerComponent : Component
{
    /// <summary>Whether water is running.</summary>
    [DataField]
    public bool On;

    /// <summary>Current valve setting.</summary>
    [DataField]
    public KestrelShowerTemperature Temperature = KestrelShowerTemperature.Normal;

    /// <summary>Water temperature in kelvin for each valve setting (Aurora: 310 K, 100 °C, 0 °C).</summary>
    [DataField]
    public Dictionary<KestrelShowerTemperature, float> WaterTemperatures = new()
    {
        { KestrelShowerTemperature.Normal, 310.15f },
        { KestrelShowerTemperature.Boiling, 373.15f },
        { KestrelShowerTemperature.Freezing, 273.15f },
    };

    /// <summary>Most a body's temperature can change per wash tick, in kelvin, so it eases in.</summary>
    [DataField]
    public float MaxTemperatureChangePerTick = 4f;

    /// <summary>Water at or above this temperature makes steam (Aurora: 20 °C).</summary>
    [DataField]
    public float SteamMinimumTemperature = 293.15f;

    /// <summary>How long a shower runs before switching itself off.</summary>
    [DataField]
    public TimeSpan AutoOffDelay = TimeSpan.FromMinutes(5);

    /// <summary>How long hot water runs before steam appears.</summary>
    [DataField]
    public TimeSpan SteamDelay = TimeSpan.FromSeconds(5);

    /// <summary>How long steam lingers after the water stops or turns cold.</summary>
    [DataField]
    public TimeSpan SteamLinger = TimeSpan.FromSeconds(25);

    /// <summary>How often whatever's under the shower gets washed.</summary>
    [DataField]
    public TimeSpan WashInterval = TimeSpan.FromSeconds(1);

    /// <summary>Minimum time between "searing hot!" / "freezing cold!" messages for the same shower.</summary>
    [DataField]
    public TimeSpan TemperatureMessageInterval = TimeSpan.FromSeconds(4);

    /// <summary>How long turning the valve with a wrench takes (Aurora: 5 seconds).</summary>
    [DataField]
    public float WrenchDelay = 5f;

    [DataField]
    public ProtoId<ToolQualityPrototype> WrenchQuality = "Anchoring";

    [DataField]
    public EntProtoId SteamPrototype = "KestrelShowerSteam";

    [DataField]
    public SoundSpecifier StartSound = new SoundPathSpecifier("/Audio/_Kestrel/Machines/Shower/shower_start.ogg");

    [DataField]
    public SoundSpecifier StopSound = new SoundPathSpecifier("/Audio/_Kestrel/Machines/Shower/shower_end.ogg");

    // Runtime state below: not set in YAML.

    [ViewVariables]
    public TimeSpan TurnOffAt;

    [ViewVariables]
    public TimeSpan NextWash;

    [ViewVariables]
    public TimeSpan NextTemperatureMessage;

    /// <summary>When steam should appear (while hot water runs) or disappear (after it stops), if pending.</summary>
    [ViewVariables]
    public TimeSpan? SteamChangeAt;

    [ViewVariables]
    public EntityUid? Steam;
}
