// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Kestrel.StatusDisplay;

namespace Content.Server._Kestrel.StatusDisplay;

/// <summary>
/// Marks a status display. It shows whatever its station's status display console last set.
/// </summary>
[RegisterComponent]
public sealed partial class KestrelStatusDisplayComponent : Component;

/// <summary>
/// The console command staff use to set what every status display on the station shows.
/// Who can use it is set by the console's AccessReader (Command).
/// </summary>
[RegisterComponent]
public sealed partial class KestrelStatusDisplayConsoleComponent : Component
{
    /// <summary>
    /// Icons the console offers: RSI states of the status display's sprite.
    /// </summary>
    [DataField]
    public List<string> Icons = new();

    /// <summary>
    /// Title shown on announcements from this console (a locale key, or plain text).
    /// </summary>
    [DataField]
    public string AnnouncementTitle = "kestrel-status-console-announcement-title";

    /// <summary>
    /// Time between announcements from this console.
    /// </summary>
    [DataField]
    public TimeSpan AnnouncementCooldown = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Time between EBS messages from this console, so staff aren't flooded.
    /// </summary>
    [DataField]
    public TimeSpan EbsCooldown = TimeSpan.FromSeconds(30);

    [ViewVariables]
    public TimeSpan NextAnnouncement;

    [ViewVariables]
    public TimeSpan NextEbs;
}

/// <summary>
/// Added to a station the first time one of its consoles is used: what its displays show,
/// and its EBS log.
/// </summary>
[RegisterComponent]
public sealed partial class KestrelStationStatusDisplayComponent : Component
{
    [ViewVariables]
    public KestrelStatusDisplayMode Mode = KestrelStatusDisplayMode.Alert;

    [ViewVariables]
    public string Message = string.Empty;

    [ViewVariables]
    public string Icon = "default";

    /// <summary>
    /// Emergency Broadcast System messages sent from this station and replies to it, oldest first.
    /// Shared by all its status display consoles.
    /// </summary>
    [ViewVariables]
    public List<KestrelEbsEntry> EbsLog = new();
}
