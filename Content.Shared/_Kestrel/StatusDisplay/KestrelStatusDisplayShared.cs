// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Shared._Kestrel.StatusDisplay;

/// <summary>
/// What the station's status displays are showing.
/// </summary>
[Serializable, NetSerializable]
public enum KestrelStatusDisplayMode : byte
{
    /// <summary>The alert level picture (default).</summary>
    Alert,

    /// <summary>A line of text, ticking across the screen.</summary>
    Message,

    /// <summary>A picture chosen from the console's list (logo, biohazard, AI faces...).</summary>
    Icon,
}

/// <summary>
/// Appearance keys the server sets on each status display; the client draws from them.
/// </summary>
[Serializable, NetSerializable]
public enum KestrelStatusDisplayVisuals : byte
{
    Mode,    // KestrelStatusDisplayMode
    Message, // string, already cleaned up (upper case, only characters the font has)
    Icon,    // string, an RSI state of status_display.rsi
}

[Serializable, NetSerializable]
public enum KestrelStatusDisplayConsoleUiKey : byte
{
    Key,
}

/// <summary>
/// Sent to the console's menu: what the displays show now, the icons it may pick from,
/// and the station's EBS message log.
/// </summary>
[Serializable, NetSerializable]
public sealed class KestrelStatusDisplayConsoleState(
    KestrelStatusDisplayMode mode,
    string message,
    string icon,
    List<string> icons,
    List<KestrelEbsEntry> ebsLog) : BoundUserInterfaceState
{
    public readonly KestrelStatusDisplayMode Mode = mode;
    public readonly string Message = message;
    public readonly string Icon = icon;
    public readonly List<string> Icons = icons;
    public readonly List<KestrelEbsEntry> EbsLog = ebsLog;
}

/// <summary>
/// One message in the Emergency Broadcast System log: sent from the station, or a reply from
/// off-station (written by staff with the ebsreply command).
/// </summary>
[Serializable, NetSerializable]
public sealed class KestrelEbsEntry(string time, string from, string text, bool incoming)
{
    /// <summary>Station time it was sent, already formatted (e.g. "00:42").</summary>
    public readonly string Time = time;

    /// <summary>Who sent it: the crew member's name and job, or the sender staff gave a reply.</summary>
    public readonly string From = from;

    public readonly string Text = text;

    /// <summary>True for replies coming in from off-station.</summary>
    public readonly bool Incoming = incoming;
}

/// <summary>Menu button: make a station announcement.</summary>
[Serializable, NetSerializable]
public sealed class KestrelStatusDisplayAnnounceMessage(string text) : BoundUserInterfaceMessage
{
    public readonly string Text = text;
}

/// <summary>Menu button: send an Emergency Broadcast System message off-station.</summary>
[Serializable, NetSerializable]
public sealed class KestrelStatusDisplayEbsMessage(string text) : BoundUserInterfaceMessage
{
    public readonly string Text = text;
}

/// <summary>Menu button: show the alert level again.</summary>
[Serializable, NetSerializable]
public sealed class KestrelStatusDisplaySetAlertMessage : BoundUserInterfaceMessage;

/// <summary>Menu button: show this text.</summary>
[Serializable, NetSerializable]
public sealed class KestrelStatusDisplaySetTextMessage(string text) : BoundUserInterfaceMessage
{
    public readonly string Text = text;
}

/// <summary>Menu button: show this icon.</summary>
[Serializable, NetSerializable]
public sealed class KestrelStatusDisplaySetIconMessage(string icon) : BoundUserInterfaceMessage
{
    public readonly string Icon = icon;
}

/// <summary>
/// The characters the status display font can draw, and message cleanup shared by server and client.
/// </summary>
public static class KestrelStatusDisplayText
{
    public const int MaxLength = 40;

    /// <summary>Longest EBS message, in characters.</summary>
    public const int MaxEbsLength = 500;

    /// <summary>Character -> font RSI state. Anything not listed is drawn as a space.</summary>
    public static readonly Dictionary<char, string> Glyphs = BuildGlyphs();

    private static Dictionary<char, string> BuildGlyphs()
    {
        var glyphs = new Dictionary<char, string> { [' '] = "char_space" };
        for (var c = 'A'; c <= 'Z'; c++)
            glyphs[c] = $"char_{c}";
        for (var c = '0'; c <= '9'; c++)
            glyphs[c] = $"char_{c}";

        glyphs['~'] = "char_tilde";
        glyphs[':'] = "char_colon";
        glyphs['\''] = "char_apostrophe";
        glyphs['('] = "char_lparen";
        glyphs[')'] = "char_rparen";
        glyphs['*'] = "char_star";
        glyphs['+'] = "char_plus";
        glyphs['='] = "char_equals";
        glyphs['/'] = "char_slash";
        glyphs['.'] = "char_period";
        glyphs[','] = "char_comma";
        glyphs['-'] = "char_dash";
        glyphs['?'] = "char_question";
        glyphs['!'] = "char_exclaim";
        return glyphs;
    }

    /// <summary>
    /// Upper-cases the text, turns characters the font doesn't have into spaces, and trims it to
    /// <see cref="MaxLength"/>.
    /// </summary>
    public static string Clean(string text)
    {
        var chars = text.Trim().ToUpperInvariant().ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (!Glyphs.ContainsKey(chars[i]))
                chars[i] = ' ';
        }

        var clean = new string(chars);
        return clean.Length > MaxLength ? clean[..MaxLength] : clean;
    }
}
