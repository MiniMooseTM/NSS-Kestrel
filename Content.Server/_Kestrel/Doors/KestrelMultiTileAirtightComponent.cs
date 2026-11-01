// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;

namespace Content.Server._Kestrel.Doors;

/// <summary>
/// Goes on an airlock that covers more than one tile. SS14 only makes the tile an
/// airtight entity stands on airtight, so this places an invisible filler on the
/// door's extra tile and keeps it airtight exactly when the door is.
/// </summary>
[RegisterComponent, Access(typeof(KestrelMultiTileAirtightSystem))]
public sealed partial class KestrelMultiTileAirtightComponent : Component
{
    /// <summary>
    /// Where the extra tile is, in tiles, relative to the door's own tile (before rotation).
    /// For example 0,1 is the tile above.
    /// </summary>
    [DataField]
    public Vector2i Offset = new(0, 1);

    /// <summary>
    /// What to place on the extra tile.
    /// </summary>
    [DataField]
    public EntProtoId FillerPrototype = "KestrelAirlockFiller";

    /// <summary>
    /// The filler this door is currently using.
    /// </summary>
    [ViewVariables]
    public EntityUid? Filler;
}
