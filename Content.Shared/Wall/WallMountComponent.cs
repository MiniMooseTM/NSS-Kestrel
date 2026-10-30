// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.ComponentTrees; // Kestrel
using Robust.Shared.GameStates;
using Robust.Shared.Physics; // Kestrel

namespace Content.Shared.Wall;

/// <summary>
///     This component enables an entity to ignore some obstructions for interaction checks.
/// </summary>
/// <remarks>
///     This will only exempt anchored entities that intersect the wall-mount. Additionally, this exemption will apply
///     in a limited arc, providing basic functionality for directional wall mounts.
/// </remarks>
// Kestrel: raiseAfterAutoHandleState and IComponentTreeEntry added for directional visibility (upstream port).
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class WallMountComponent : Component, IComponentTreeEntry<WallMountComponent>
{
    /// <summary>
    ///     Range of angles for which the exemption applies. Bigger is more permissive.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("arc"), AutoNetworkedField]
    public Angle Arc = new(MathF.PI);

    /// <summary>
    ///     The direction in which the exemption arc is facing, relative to the entity's rotation. Defaults to south.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("direction"), AutoNetworkedField]
    public Angle Direction = Angle.Zero;

    // Kestrel start: directional visibility, ported from upstream.
    /// <summary>
    ///     If true, the sprite is only visible from within the facing <see cref="Arc"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool DirectionalVisibility = true;

    /// <inheritdoc/>
    [ViewVariables]
    public EntityUid? TreeUid { get; set; }

    /// <inheritdoc/>
    [ViewVariables]
    public DynamicTree<ComponentTreeEntry<WallMountComponent>>? Tree { get; set; }

    /// <inheritdoc/>
    [ViewVariables]
    public bool AddToTree => Arc < Math.Tau && DirectionalVisibility;

    /// <inheritdoc/>
    public bool TreeUpdateQueued { get; set; }
    // Kestrel end
}