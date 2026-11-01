// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Nuke;
using Content.Shared.Construction;
using Content.Shared.Examine;
using Content.Shared.Nuke;
using JetBrains.Annotations;

namespace Content.Server._Kestrel.Nuke;

/// <summary>
/// Construction condition: passes only while the nuke on this entity isn't counting down.
/// Used so the self-destruct terminal can't be unbolted while armed.
/// </summary>
/// <remarks>
/// Without this, removing the terminal while armed would disarm it (NukeSystem disarms an armed nuke
/// that gets unanchored), turning the tools into a 25-second disarm that skips the disk and code.
/// </remarks>
[UsedImplicitly]
[DataDefinition]
public sealed partial class KestrelNukeNotArmed : IGraphCondition
{
    public bool Condition(EntityUid uid, IEntityManager entityManager)
    {
        return !entityManager.TryGetComponent<NukeComponent>(uid, out var nuke)
               || nuke.Status != NukeStatus.ARMED;
    }

    public bool DoExamine(ExaminedEvent args)
    {
        if (Condition(args.Examined, IoCManager.Resolve<IEntityManager>()))
            return false;

        args.PushMarkup(Loc.GetString("kestrel-construction-condition-nuke-not-armed"));
        return true;
    }

    public IEnumerable<ConstructionGuideEntry> GenerateGuideEntry()
    {
        yield return new ConstructionGuideEntry
        {
            Localization = "kestrel-construction-condition-nuke-not-armed",
        };
    }
}
