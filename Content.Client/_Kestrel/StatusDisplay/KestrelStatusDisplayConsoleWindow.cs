// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared._Kestrel.StatusDisplay;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._Kestrel.StatusDisplay;

/// <summary>
/// The status display console's menu, in three tabs:
/// - Displays: show the alert level, a message, or an icon on every status display on the station.
/// - Announcement: a station-wide announcement, like the communications console.
/// - EBS: the Emergency Broadcast System. Send a message off-station and read the replies.
/// </summary>
/// <remarks>
/// Built in code (no XAML) to keep it to one file. The window only sends requests; the server
/// checks them (access, cooldowns, length) and sends back the current state.
/// </remarks>
public sealed class KestrelStatusDisplayConsoleWindow : DefaultWindow
{
    public event Action? OnSetAlert;
    public event Action<string>? OnSetText;
    public event Action<string>? OnSetIcon;
    public event Action<string>? OnAnnounce;
    public event Action<string>? OnSendEbs;

    private static readonly Color OutgoingColor = Color.FromHex("#7FB2F0");
    private static readonly Color IncomingColor = Color.FromHex("#F0C27F");

    private readonly Label _current;
    private readonly LineEdit _textEdit;
    private readonly OptionButton _iconPicker;
    private readonly LineEdit _announceEdit;
    private readonly LineEdit _ebsEdit;
    private readonly BoxContainer _ebsLog;
    private readonly ScrollContainer _ebsScroll;
    private List<string> _icons = new();
    private int _ebsCount = -1;
    private bool _scrollToBottom;

    public KestrelStatusDisplayConsoleWindow()
    {
        Title = Loc.GetString("kestrel-status-console-title");
        MinSize = new(460, 380);

        var tabs = new TabContainer { VerticalExpand = true };
        Contents.AddChild(tabs);

        // ---- Displays tab (stage 1) ----
        var displays = Column();
        tabs.AddChild(displays);
        TabContainer.SetTabTitle(displays, Loc.GetString("kestrel-status-console-tab-displays"));

        _current = new Label();
        displays.AddChild(_current);

        var alertButton = new Button { Text = Loc.GetString("kestrel-status-console-show-alert") };
        alertButton.OnPressed += _ => OnSetAlert?.Invoke();
        displays.AddChild(alertButton);

        displays.AddChild(new Label { Text = Loc.GetString("kestrel-status-console-message-label") });
        _textEdit = new LineEdit
        {
            HorizontalExpand = true,
            PlaceHolder = Loc.GetString("kestrel-status-console-message-placeholder"),
        };
        _textEdit.IsValid = s => s.Length <= KestrelStatusDisplayText.MaxLength;
        var textButton = new Button { Text = Loc.GetString("kestrel-status-console-show-message") };
        textButton.OnPressed += _ => SendText();
        _textEdit.OnTextEntered += _ => SendText();
        displays.AddChild(Row(_textEdit, textButton));
        displays.AddChild(Hint(Loc.GetString("kestrel-status-console-message-hint", ("max", KestrelStatusDisplayText.MaxLength))));

        displays.AddChild(new Label { Text = Loc.GetString("kestrel-status-console-icon-label") });
        _iconPicker = new OptionButton { HorizontalExpand = true };
        _iconPicker.OnItemSelected += args => _iconPicker.SelectId(args.Id);
        var iconButton = new Button { Text = Loc.GetString("kestrel-status-console-show-icon") };
        iconButton.OnPressed += _ =>
        {
            var id = _iconPicker.SelectedId;
            if (id >= 0 && id < _icons.Count)
                OnSetIcon?.Invoke(_icons[id]);
        };
        displays.AddChild(Row(_iconPicker, iconButton));

        // ---- Announcement tab (stage 2) ----
        var announce = Column();
        tabs.AddChild(announce);
        TabContainer.SetTabTitle(announce, Loc.GetString("kestrel-status-console-tab-announce"));

        announce.AddChild(new Label { Text = Loc.GetString("kestrel-status-console-announce-label") });
        _announceEdit = new LineEdit
        {
            HorizontalExpand = true,
            PlaceHolder = Loc.GetString("kestrel-status-console-announce-placeholder"),
        };
        var announceButton = new Button { Text = Loc.GetString("kestrel-status-console-announce-button") };
        announceButton.OnPressed += _ => SendAnnouncement();
        _announceEdit.OnTextEntered += _ => SendAnnouncement();
        announce.AddChild(Row(_announceEdit, announceButton));
        announce.AddChild(Hint(Loc.GetString("kestrel-status-console-announce-hint")));

        // ---- EBS tab (stage 3) ----
        var ebs = Column();
        tabs.AddChild(ebs);
        TabContainer.SetTabTitle(ebs, Loc.GetString("kestrel-status-console-tab-ebs"));

        ebs.AddChild(Hint(Loc.GetString("kestrel-status-console-ebs-hint")));

        // The log: one line per message, oldest at the top.
        _ebsLog = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
        _ebsScroll = new ScrollContainer
        {
            VerticalExpand = true,
            HScrollEnabled = false,
            MinHeight = 160,
        };
        _ebsScroll.AddChild(_ebsLog);
        ebs.AddChild(_ebsScroll);

        _ebsEdit = new LineEdit
        {
            HorizontalExpand = true,
            PlaceHolder = Loc.GetString("kestrel-status-console-ebs-placeholder"),
        };
        _ebsEdit.IsValid = s => s.Length <= KestrelStatusDisplayText.MaxEbsLength;
        var ebsButton = new Button { Text = Loc.GetString("kestrel-status-console-ebs-send") };
        ebsButton.OnPressed += _ => SendEbs();
        _ebsEdit.OnTextEntered += _ => SendEbs();
        ebs.AddChild(Row(_ebsEdit, ebsButton));
    }

    private static BoxContainer Column()
    {
        return new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8,
            Margin = new Thickness(4),
        };
    }

    private static BoxContainer Row(Control left, Control right)
    {
        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
        row.AddChild(left);
        row.AddChild(right);
        return row;
    }

    private static Label Hint(string text)
    {
        return new Label { Text = text, FontColorOverride = Color.Gray };
    }

    private void SendText()
    {
        if (!string.IsNullOrWhiteSpace(_textEdit.Text))
            OnSetText?.Invoke(_textEdit.Text);
    }

    private void SendAnnouncement()
    {
        if (string.IsNullOrWhiteSpace(_announceEdit.Text))
            return;

        OnAnnounce?.Invoke(_announceEdit.Text);
        _announceEdit.Clear();
    }

    private void SendEbs()
    {
        if (string.IsNullOrWhiteSpace(_ebsEdit.Text))
            return;

        OnSendEbs?.Invoke(_ebsEdit.Text);
        _ebsEdit.Clear();
    }

    public void UpdateState(KestrelStatusDisplayConsoleState state)
    {
        _current.Text = state.Mode switch
        {
            KestrelStatusDisplayMode.Message => Loc.GetString("kestrel-status-console-current-message", ("text", state.Message)),
            KestrelStatusDisplayMode.Icon => Loc.GetString("kestrel-status-console-current-icon", ("icon", IconName(state.Icon))),
            _ => Loc.GetString("kestrel-status-console-current-alert"),
        };

        UpdateEbsLog(state.EbsLog);

        // Rebuild the icon list only if it changed, so the player's selection isn't reset.
        if (_icons.Count == state.Icons.Count && _icons.SequenceEqual(state.Icons))
            return;

        _icons = new List<string>(state.Icons);
        _iconPicker.Clear();
        for (var i = 0; i < _icons.Count; i++)
        {
            _iconPicker.AddItem(IconName(_icons[i]), i);
        }
    }

    /// <summary>
    /// Rebuilds the EBS log when a message has been added, and scrolls to the newest one.
    /// </summary>
    private void UpdateEbsLog(List<KestrelEbsEntry> log)
    {
        if (log.Count == _ebsCount)
            return;

        _ebsCount = log.Count;
        _ebsLog.RemoveAllChildren();

        if (log.Count == 0)
        {
            _ebsLog.AddChild(Hint(Loc.GetString("kestrel-status-console-ebs-empty")));
            return;
        }

        foreach (var entry in log)
        {
            // AddText escapes the text, so a player can't put markup tags in their message.
            var msg = new FormattedMessage();
            msg.PushColor(entry.Incoming ? IncomingColor : OutgoingColor);
            msg.AddText(Loc.GetString(entry.Incoming ? "kestrel-status-console-ebs-in" : "kestrel-status-console-ebs-out",
                ("time", entry.Time), ("from", entry.From)));
            msg.Pop();
            msg.AddText(" " + entry.Text);

            var label = new RichTextLabel { HorizontalExpand = true };
            label.SetMessage(msg);
            _ebsLog.AddChild(label);
        }

        // Scroll to the bottom on the next frame, once the layout knows the new height.
        _scrollToBottom = true;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (!_scrollToBottom)
            return;

        _scrollToBottom = false;
        _ebsScroll.SetScrollValue(new(0, float.MaxValue));
    }

    /// <summary>
    /// Readable icon name: the locale string if there is one, otherwise the state name tidied up
    /// ("ai_veryhappy" -> "veryhappy").
    /// </summary>
    private static string IconName(string state)
    {
        var key = $"kestrel-status-icon-{state.Replace('_', '-')}";
        return Loc.TryGetString(key, out var name) ? name : state.Replace("ai_", "").Replace('_', ' ');
    }
}
