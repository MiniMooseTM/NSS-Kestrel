// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Maths.FixedPoint;
using Content.Server.Fluids.EntitySystems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Damage.Systems; // DamageChangedEvent lives here in this fork

namespace Content.Server._Kestrel.Leaks;

/// <summary>
/// Spills a little liquid under an entity whenever it takes cutting or stabbing damage.
/// </summary>
/// <remarks>
/// How it works:
/// 1. Every time an entity's damage changes, the damage system raises DamageChangedEvent on it.
///    DamageDelta holds just the change from this hit (e.g. Slash: 12).
/// 2. We add up the damage types we care about (Slash and Piercing by default).
/// 3. If that's a real hit (at least MinimumDamage), we make a small solution of the reagent and
///    spill it on the entity's tile, exactly like a dropped beaker would.
/// The puddle system does the rest: spreading, mopping, and footprints all come with it.
/// </remarks>
public sealed class KestrelDamageLeakSystem : EntitySystem
{
    [Dependency] private readonly PuddleSystem _puddle = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<KestrelDamageLeakComponent, DamageChangedEvent>(OnDamageChanged);
    }

    private void OnDamageChanged(Entity<KestrelDamageLeakComponent> ent, ref DamageChangedEvent args)
    {
        // Healing also raises this event; only new damage should leak.
        if (!args.DamageIncreased || args.DamageDelta is not { } delta)
            return;

        var damage = FixedPoint2.Zero;
        foreach (var type in ent.Comp.DamageTypes)
        {
            if (delta.DamageDict.TryGetValue(type, out var amount) && amount > 0)
                damage += amount;
        }

        if (damage < ent.Comp.MinimumDamage)
            return;

        var spill = FixedPoint2.Min(damage * ent.Comp.AmountPerDamage, ent.Comp.MaxAmount);
        var solution = new Solution(ent.Comp.Reagent, spill);

        // sound: false - the hit itself already makes a sound.
        _puddle.TrySpillAt(ent.Owner, solution, out _, sound: false);
    }
}
