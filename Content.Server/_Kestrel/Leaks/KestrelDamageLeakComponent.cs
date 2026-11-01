// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._Kestrel.Leaks;

/// <summary>
/// Spills a liquid onto the floor when the entity takes certain kinds of damage.
/// Purely cosmetic: nothing is drained from the entity, and it has no bloodstream.
/// Used to make IPCs leak machine oil when cut or stabbed.
/// </summary>
[RegisterComponent]
public sealed partial class KestrelDamageLeakComponent : Component
{
    /// <summary>
    /// The liquid that leaks out.
    /// </summary>
    [DataField]
    public ProtoId<ReagentPrototype> Reagent = "MachineOil";

    /// <summary>
    /// Only these damage types cause a leak. Blunt, burn, shock etc. don't puncture anything.
    /// </summary>
    [DataField]
    public List<ProtoId<DamageTypePrototype>> DamageTypes = new() { "Slash", "Piercing" };

    /// <summary>
    /// Hits smaller than this (counting only <see cref="DamageTypes"/>) don't leak,
    /// so a scratch doesn't leave a puddle.
    /// </summary>
    [DataField]
    public FixedPoint2 MinimumDamage = 5;

    /// <summary>
    /// Units of liquid spilled per point of qualifying damage.
    /// </summary>
    [DataField]
    public FixedPoint2 AmountPerDamage = 0.5;

    /// <summary>
    /// The most liquid a single hit can spill, so one huge hit doesn't flood the room.
    /// </summary>
    [DataField]
    public FixedPoint2 MaxAmount = 10;
}
