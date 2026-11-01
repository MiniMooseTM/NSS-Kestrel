// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Kestrel.StatusDisplay;

namespace Content.Client._Kestrel.StatusDisplay;

/// <summary>
/// Client half of the status display: draws the alert picture, an icon, or ticking text.
/// </summary>
/// <remarks>
/// Client-only. The name ends in "Visuals" so the server ignores it in the shared prototype.
/// The sprite needs the layers mapped as "kestrelIcon" and "kestrelChar0".."kestrelChar(N-1)"
/// (see Structures/Wallmounts/status_display.yml).
/// </remarks>
[RegisterComponent]
public sealed partial class KestrelStatusDisplayVisualsComponent : Component
{
    /// <summary>How many characters fit on the screen at once (one layer each).</summary>
    [DataField]
    public int CharacterSlots = 4;

    /// <summary>Seconds between each one-character step when the text is too long to fit.</summary>
    [DataField]
    public float StepTime = 0.35f;

    /// <summary>Blank characters shown between the end of the text and its start again.</summary>
    [DataField]
    public int Gap = 3;

    // Runtime state, set from appearance data.
    [ViewVariables] public KestrelStatusDisplayMode Mode = KestrelStatusDisplayMode.Alert;
    [ViewVariables] public string Text = string.Empty;
    [ViewVariables] public bool Powered = true;
    [ViewVariables] public int Position;
    [ViewVariables] public float Accumulator;
}
