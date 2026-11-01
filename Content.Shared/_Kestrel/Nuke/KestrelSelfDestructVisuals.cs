// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Shared._Kestrel.Nuke;

/// <summary>
/// Appearance key for the self-destruct terminal and the floor around it.
/// The server sets it; the YAML GenericVisualizer picks the pictures.
/// </summary>
[Serializable, NetSerializable]
public enum KestrelSelfDestructVisuals : byte
{
    State,
}

/// <summary>
/// What the self-destruct is doing, in the four steps Aurora's art shows.
/// </summary>
[Serializable, NetSerializable]
public enum KestrelSelfDestructState : byte
{
    /// <summary>No disk, or disk in but no code yet. Blue lights, floor dark.</summary>
    Safe,

    /// <summary>Code accepted, ready to arm ("safety off"). Red lights, floor lit.</summary>
    Ready,

    /// <summary>Counting down. Flashing red, floor pulsing.</summary>
    Armed,

    /// <summary>Final seconds (the alert sound has played). Detonating picture.</summary>
    Detonating,
}
