// SPDX-License-Identifier: AGPL-3.0-or-later
// Kestrel: ported early from upstream space-wizards/space-station-14 (MIT), commit 2e9280f.
// Kept at the same path and nearly identical so a future upstream merge lines up cleanly.

using Content.Shared.Wall;
using Robust.Shared.ComponentTrees;
using Robust.Shared.Physics;

namespace Content.Client.Wall.Components;

/// <summary>
/// Stores a component tree of <see cref="WallMountComponent"/>.
/// </summary>
[RegisterComponent]
public sealed partial class WallMountTreeComponent : Component, IComponentTreeComponent<WallMountComponent>
{
    /// <inheritdoc/>
    public DynamicTree<ComponentTreeEntry<WallMountComponent>> Tree { get; set; } = default!;
}
