// SPDX-License-Identifier: AGPL-3.0-or-later

using Color = Robust.Shared.Maths.Color; // avoids a name clash

namespace Content.Server._Kestrel.Light;

/// <summary>
/// Makes a powered light change colour with its station's alert level.
/// Used by the alert floor lights.
/// </summary>
[RegisterComponent]
public sealed partial class KestrelAlertLightComponent : Component
{
    /// <summary>
    /// Alert levels where the light shows <see cref="NormalColor"/> instead of the alert colour.
    /// </summary>
    [DataField]
    public HashSet<string> NormalLevels = new() { "green" };

    /// <summary>
    /// Colour on the <see cref="NormalLevels"/>.
    /// </summary>
    [DataField]
    public Color NormalColor = Color.FromHex("#FFE4CE"); // warm white, same as the plain light tube
}
