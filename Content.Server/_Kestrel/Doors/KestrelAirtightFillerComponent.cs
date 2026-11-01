// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Server._Kestrel.Doors;

/// <summary>
/// Marks the invisible filler on a multi-tile door's extra tile, so the door can find
/// and reuse an existing filler instead of placing a second one.
/// </summary>
[RegisterComponent]
public sealed partial class KestrelAirtightFillerComponent : Component;
