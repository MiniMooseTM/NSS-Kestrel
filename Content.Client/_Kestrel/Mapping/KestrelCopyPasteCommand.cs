// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Console;

namespace Content.Client._Kestrel.Mapping;

/// <summary>
/// Opens or closes the copy/paste tool window. Bound to F12 in keybinds.yml.
/// Anyone can type it, but the server ignores requests from players without the Mapping admin flag.
/// </summary>
public sealed class KestrelCopyPasteCommand : LocalizedEntityCommands
{
    [Dependency] private readonly KestrelCopyPasteSystem _copyPaste = default!;

    public override string Command => "copypaste";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        _copyPaste.ToggleWindow();
    }
}
