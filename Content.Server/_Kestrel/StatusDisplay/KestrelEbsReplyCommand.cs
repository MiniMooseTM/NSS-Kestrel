// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.Station.Components;
using Robust.Shared.Console;

namespace Content.Server._Kestrel.StatusDisplay;

/// <summary>
/// Staff command to answer an EBS message. The reply shows up in the EBS log on the station's
/// status display consoles, with a chime.
/// Usage: ebsreply &lt;station id&gt; "&lt;from&gt;" &lt;message...&gt;
/// The station id is printed in the admin notice when an EBS message comes in.
/// Example: ebsreply 12345 "Colonial Authority" Message received. Hold position.
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed class KestrelEbsReplyCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    public string Command => "ebsreply";
    public string Description => Loc.GetString("kestrel-ebs-reply-command-description");
    public string Help => Loc.GetString("kestrel-ebs-reply-command-help");

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 3)
        {
            shell.WriteError(Help);
            return;
        }

        if (!NetEntity.TryParse(args[0], out var netStation)
            || !_entManager.TryGetEntity(netStation, out var station)
            || !_entManager.HasComponent<StationDataComponent>(station))
        {
            shell.WriteError(Loc.GetString("kestrel-ebs-reply-command-bad-station"));
            return;
        }

        // args are already split on spaces, with "quoted text" kept together.
        var from = args[1];
        var text = string.Join(' ', args[2..]);

        var comms = _entManager.System<KestrelStatusConsoleCommsSystem>();
        if (comms.AddReply(station.Value, from, text))
            shell.WriteLine(Loc.GetString("kestrel-ebs-reply-command-sent"));
    }
}
