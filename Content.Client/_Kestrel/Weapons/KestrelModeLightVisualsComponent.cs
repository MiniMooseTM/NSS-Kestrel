// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Client._Kestrel.Weapons;

/// <summary>
/// Tints a weapon's indicator light to match its selected fire mode, Aurora-style:
/// light blue for stun, red for lethal.
/// </summary>
/// <remarks>
/// The light's sprite has to be white/grey for this to work, because a tint multiplies the
/// colours (white x blue = blue, but red x blue = black).
///
/// Two kinds of mode switch are supported, matching the two systems guns use in this fork:
/// - Guns with BatteryWeaponFireModes (e.g. the energy magnum) report the projectile id of
///   the current mode. <see cref="FireModeColors"/> maps those ids to colours.
/// - Guns with an AmmoSelector (e.g. the energy shotgun) report the colour set on the chosen
///   selectableAmmo prototype, so they need no map here.
///
/// The name ends in "Visuals" so the server ignores this client-only component.
/// </remarks>
[RegisterComponent]
public sealed partial class KestrelModeLightVisualsComponent : Component
{
    /// <summary>
    /// The sprite layer to tint, e.g. "enum.GunVisualLayers.MagUnshaded" (the charge bars).
    /// </summary>
    [DataField(required: true)]
    public string Layer = default!;

    /// <summary>
    /// Projectile prototype id of a fire mode -> light colour. Only needed for guns that use
    /// BatteryWeaponFireModes.
    /// </summary>
    [DataField]
    public Dictionary<string, Color> FireModeColors = new();
}
