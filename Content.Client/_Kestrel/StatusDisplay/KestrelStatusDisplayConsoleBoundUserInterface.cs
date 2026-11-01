// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Kestrel.StatusDisplay;
using Robust.Client.UserInterface;

namespace Content.Client._Kestrel.StatusDisplay;

/// <summary>
/// Connects the status display console's window to the server: button presses become messages,
/// and state from the server updates the window.
/// </summary>
public sealed class KestrelStatusDisplayConsoleBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private KestrelStatusDisplayConsoleWindow? _window;

    public KestrelStatusDisplayConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<KestrelStatusDisplayConsoleWindow>();
        _window.OnSetAlert += () => SendMessage(new KestrelStatusDisplaySetAlertMessage());
        _window.OnSetText += text => SendMessage(new KestrelStatusDisplaySetTextMessage(text));
        _window.OnSetIcon += icon => SendMessage(new KestrelStatusDisplaySetIconMessage(icon));
        _window.OnAnnounce += text => SendMessage(new KestrelStatusDisplayAnnounceMessage(text));
        _window.OnSendEbs += text => SendMessage(new KestrelStatusDisplayEbsMessage(text));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is KestrelStatusDisplayConsoleState cast)
            _window?.UpdateState(cast);
    }
}
