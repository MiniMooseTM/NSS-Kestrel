// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Kestrel.Nuke;
using Robust.Shared.Prototypes;

namespace Content.Server._Kestrel.Nuke;

/// <summary>
/// Turns a nuke into the station's fixed self-destruct terminal:
/// it can't be unanchored, it shows Aurora's terminal pictures, and it lights the floor around it.
/// The nuke itself (disk, code, timer, explosion) is still the normal NukeComponent.
/// </summary>
[RegisterComponent]
public sealed partial class KestrelSelfDestructTerminalComponent : Component
{
    /// <summary>
    /// The 3x3 floor-light entity spawned under the terminal.
    /// </summary>
    [DataField]
    public EntProtoId FloorPrototype = "KestrelSelfDestructFloor";

    /// <summary>
    /// The spawned floor-light entity, if any.
    /// </summary>
    [ViewVariables]
    public EntityUid? Floor;

    /// <summary>
    /// The last state shown, so pictures are only updated when it changes.
    /// </summary>
    [ViewVariables]
    public KestrelSelfDestructState? LastState;

    /// <summary>
    /// How often to check the nuke's state.
    /// </summary>
    [DataField]
    public TimeSpan UpdateInterval = TimeSpan.FromSeconds(0.5);

    [ViewVariables]
    public TimeSpan NextUpdate;
}
