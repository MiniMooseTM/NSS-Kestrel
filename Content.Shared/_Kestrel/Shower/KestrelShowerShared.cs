// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Kestrel.Shower;

// These live in Shared because both sides need them: the server sets them,
// and the client reads them to draw the water (YAML GenericVisualizer) or to
// finish the wrench do-after.

/// <summary>Appearance keys the shower sends to clients.</summary>
[Serializable, NetSerializable]
public enum KestrelShowerVisuals : byte
{
    /// <summary>True while water is running.</summary>
    On,
}

/// <summary>Sprite layer names used in the shower prototype.</summary>
[Serializable, NetSerializable]
public enum KestrelShowerVisualLayers : byte
{
    Water,
}

/// <summary>The three valve settings, as in Aurora.</summary>
[Serializable, NetSerializable]
public enum KestrelShowerTemperature : byte
{
    Normal,
    Boiling,
    Freezing,
}

/// <summary>Raised when someone finishes turning the temperature valve with a wrench.</summary>
[Serializable, NetSerializable]
public sealed partial class KestrelShowerTemperatureDoAfterEvent : SimpleDoAfterEvent;
