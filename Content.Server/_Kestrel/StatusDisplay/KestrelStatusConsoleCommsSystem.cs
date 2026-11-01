// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Common.Administration.Notifications;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Shared._Kestrel.StatusDisplay;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Server._Kestrel.StatusDisplay;

/// <summary>
/// The status display console's announcements and Emergency Broadcast System (EBS).
/// </summary>
/// <remarks>
/// How it works:
/// - Announcement: the same as the communications console. The text is cleaned up with the game's
///   announcement sanitiser, signed with the sender's name and job (from their ID), and sent as a
///   station announcement. Each console has a cooldown.
/// - EBS: an in-character emergency message to off-station authorities, who are played by staff.
///   The message goes into the station's EBS log (shown on every status display console there),
///   and every online admin gets a chat notice and a ping, the same way faxes to Central Command do.
///   The notice includes the exact command to answer it: staff reply with the "ebsreply" command
///   (KestrelEbsReplyCommand), which calls <see cref="AddReply"/>. The reply appears in the log on
///   the station's consoles with a chime.
/// </remarks>
public sealed class KestrelStatusConsoleCommsSystem : EntitySystem
{
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly IAdminManager _adminManager = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly KestrelStatusDisplaySystem _displays = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly SoundSpecifier ReplySound = new SoundPathSpecifier("/Audio/Machines/chime.ogg");
    private static readonly SoundSpecifier AdminPing = new SoundPathSpecifier("/Audio/Machines/high_tech_confirm.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<KestrelStatusDisplayConsoleComponent, KestrelStatusDisplayAnnounceMessage>(OnAnnounce);
        SubscribeLocalEvent<KestrelStatusDisplayConsoleComponent, KestrelStatusDisplayEbsMessage>(OnEbs);
    }

    private void OnAnnounce(Entity<KestrelStatusDisplayConsoleComponent> ent, ref KestrelStatusDisplayAnnounceMessage args)
    {
        if (_timing.CurTime < ent.Comp.NextAnnouncement)
        {
            var wait = (int) Math.Ceiling((ent.Comp.NextAnnouncement - _timing.CurTime).TotalSeconds);
            _popup.PopupEntity(Loc.GetString("kestrel-status-console-cooldown", ("seconds", wait)), ent, args.Actor);
            return;
        }

        var message = SharedChatSystem.SanitizeAnnouncement(args.Text, _cfg.GetCVar(CCVars.ChatMaxAnnouncementLength));
        if (string.IsNullOrWhiteSpace(message))
            return;

        ent.Comp.NextAnnouncement = _timing.CurTime + ent.Comp.AnnouncementCooldown;

        var author = GetAuthor(ent, args.Actor);
        message += "\n" + Loc.GetString("comms-console-announcement-sent-by") + " " + author;

        var title = Loc.TryGetString(ent.Comp.AnnouncementTitle, out var locTitle) ? locTitle : ent.Comp.AnnouncementTitle;
        _chat.DispatchStationAnnouncement(ent, message, title);

        _adminLog.Add(LogType.Chat, LogImpact.Low,
            $"{ToPrettyString(args.Actor):player} made a station announcement from {ToPrettyString(ent):entity}: {message}");
    }

    private void OnEbs(Entity<KestrelStatusDisplayConsoleComponent> ent, ref KestrelStatusDisplayEbsMessage args)
    {
        if (_timing.CurTime < ent.Comp.NextEbs)
        {
            var wait = (int) Math.Ceiling((ent.Comp.NextEbs - _timing.CurTime).TotalSeconds);
            _popup.PopupEntity(Loc.GetString("kestrel-status-console-cooldown", ("seconds", wait)), ent, args.Actor);
            return;
        }

        var text = args.Text.Trim();
        if (text.Length == 0 || !_displays.TryGetStationState(ent, out var station, out var state))
            return;

        if (text.Length > KestrelStatusDisplayText.MaxEbsLength)
            text = text[..KestrelStatusDisplayText.MaxEbsLength];

        ent.Comp.NextEbs = _timing.CurTime + ent.Comp.EbsCooldown;

        var author = GetAuthor(ent, args.Actor);
        state.EbsLog.Add(new KestrelEbsEntry(StationTime(), author, text, incoming: false));
        _displays.RefreshConsoleUis(station, state);
        _popup.PopupEntity(Loc.GetString("kestrel-ebs-sent"), ent, args.Actor);

        // Tell staff, with the command to answer it.
        var stationName = Name(station);
        _chatManager.SendAdminAnnouncement(Loc.GetString("kestrel-ebs-admin-notice",
            ("station", stationName),
            ("author", author),
            ("text", text),
            ("id", GetNetEntity(station).Id)));
        foreach (var admin in _adminManager.ActiveAdmins)
        {
            RaiseNetworkEvent(new AdminNotificationEvent(AdminPing), admin);
        }

        _adminLog.Add(LogType.Chat, LogImpact.Medium,
            $"{ToPrettyString(args.Actor):player} sent an EBS message from {ToPrettyString(ent):entity}: {text}");
    }

    /// <summary>
    /// Adds a reply from off-station to a station's EBS log, and chimes on its consoles.
    /// Used by the ebsreply admin command.
    /// </summary>
    public bool AddReply(EntityUid station, string from, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var state = EnsureComp<KestrelStationStatusDisplayComponent>(station);
        state.EbsLog.Add(new KestrelEbsEntry(StationTime(), from, text.Trim(), incoming: true));
        _displays.RefreshConsoleUis(station, state);

        // Chime on every status display console that belongs to this station.
        var consoles = EntityQueryEnumerator<KestrelStatusDisplayConsoleComponent>();
        while (consoles.MoveNext(out var uid, out _))
        {
            if (!_displays.TryGetStationState(uid, out var consoleStation, out _) || consoleStation != station)
                continue;

            _audio.PlayPvs(ReplySound, uid);
            _popup.PopupEntity(Loc.GetString("kestrel-ebs-incoming"), uid, PopupType.Medium);
        }

        _adminLog.Add(LogType.Chat, LogImpact.Low, $"EBS reply to {ToPrettyString(station):entity} from \"{from}\": {text}");
        return true;
    }

    /// <summary>
    /// The sender's name and job from their ID, like the communications console uses.
    /// </summary>
    private string GetAuthor(EntityUid console, EntityUid actor)
    {
        var ev = new TryGetIdentityShortInfoEvent(console, actor);
        RaiseLocalEvent(ev);
        return ev.Title ?? Loc.GetString("comms-console-announcement-unknown-sender");
    }

    private string StationTime()
    {
        var time = _gameTicker.RoundDuration();
        return $"{(int) time.TotalHours:D2}:{time.Minutes:D2}";
    }
}
