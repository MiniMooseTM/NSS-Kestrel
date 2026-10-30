// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Audio;

namespace Content.Server._Kestrel.Mech;

/// <summary>
/// Plays a sound when a pilot climbs into this mech.
/// </summary>
[RegisterComponent]
public sealed partial class MechEntrySoundComponent : Component
{
    /// <summary>
    /// The sound to play on entry.
    /// </summary>
    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/_Kestrel/Mecha/mechpoweron.ogg");
}
