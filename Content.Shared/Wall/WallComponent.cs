// SPDX-License-Identifier: AGPL-3.0-or-later
// Kestrel: ported early from upstream space-wizards/space-station-14 (MIT), commit 2e9280f.
// Kept at the same path and nearly identical so a future upstream merge lines up cleanly.

using Robust.Shared.GameStates;

namespace Content.Shared.Wall;

/// <summary>
/// Marks the entity as a wall.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class WallComponent : Component;
