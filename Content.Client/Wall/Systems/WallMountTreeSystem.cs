// SPDX-License-Identifier: AGPL-3.0-or-later
// Kestrel: ported early from upstream space-wizards/space-station-14 (MIT), commit 2e9280f.
// Kept at the same path and nearly identical so a future upstream merge lines up cleanly.

using System.Numerics;
using Content.Client.Wall.Components;
using Content.Shared.Wall;
using Robust.Client.GameObjects;
using Robust.Shared.ComponentTrees;
using Robust.Shared.Physics;

namespace Content.Client.Wall.Systems;

/// <summary>
/// Maintains the <see cref="WallMountTreeComponent"/> component tree.
/// </summary>
public sealed class WallMountTreeSystem : ComponentTreeSystem<WallMountTreeComponent, WallMountComponent>
{
    [Dependency] private readonly SpriteSystem _sprite = default!; // Kestrel: readonly, our codebase style

    /// <inheritdoc/>
    protected override bool DoFrameUpdate => true;

    /// <inheritdoc/>
    protected override bool DoTickUpdate => false;

    /// <inheritdoc/>
    protected override bool Recursive => false;

    /// <inheritdoc/>
    protected override Box2 ExtractAabb(in ComponentTreeEntry<WallMountComponent> entry, Vector2 pos, Angle rot)
    {
        return _sprite.CalculateBounds((entry.Uid, Comp<SpriteComponent>(entry.Uid)), pos, rot, default).CalcBoundingBox();
    }
}
