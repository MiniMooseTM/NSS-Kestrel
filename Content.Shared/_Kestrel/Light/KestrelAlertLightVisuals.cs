// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Shared._Kestrel.Light;

/// <summary>
/// Appearance key the alert-following lights set to their current colour.
/// </summary>
/// <remarks>
/// Nothing reads the value. The light's glow picture is re-tinted by PoweredLightVisualizerSystem
/// whenever any appearance data changes; changing the colour alone doesn't change any appearance
/// data, so we set this to make the glow pick up the new colour.
/// </remarks>
[Serializable, NetSerializable]
public enum KestrelAlertLightVisuals : byte
{
    Color,
}
