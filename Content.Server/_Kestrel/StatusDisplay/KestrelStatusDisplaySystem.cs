// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Administration.Logs;
using Content.Server.AlertLevel;
using Content.Server.Station.Systems;
using Content.Shared._Kestrel.StatusDisplay;
using Content.Shared.Database;
using Content.Shared.Station.Components;
using Robust.Server.GameObjects;

namespace Content.Server._Kestrel.StatusDisplay;

/// <summary>
/// Lets the status display console set what every status display on its station shows:
/// the alert level (default), a line of text, or an icon.
/// </summary>
/// <remarks>
/// How it works:
/// 1. Each station gets a KestrelStationStatusDisplayComponent holding the current mode, text and icon.
/// 2. The console's menu sends one of three messages (set alert / set text / set icon). Only people
///    with the console's access can open its menu (ActivatableUIRequiresAccess in the prototype).
///    We check and clean the input, store it on the station, and push it to every display on that
///    station as appearance data. The client (KestrelStatusDisplayVisualsSystem) draws it.
/// 3. When the station's alert level changes, the alert wins: the displays go back to the alert picture.
/// 4. A display that appears later (built, or a new round) picks up the station's current setting.
/// </remarks>
public sealed class KestrelStatusDisplaySystem : EntitySystem
{
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<KestrelStatusDisplayComponent, MapInitEvent>(OnDisplayMapInit);
        SubscribeLocalEvent<AlertLevelChangedEvent>(OnAlertLevelChanged);

        SubscribeLocalEvent<KestrelStatusDisplayConsoleComponent, BoundUIOpenedEvent>(OnConsoleOpened);
        SubscribeLocalEvent<KestrelStatusDisplayConsoleComponent, KestrelStatusDisplaySetAlertMessage>(OnSetAlert);
        SubscribeLocalEvent<KestrelStatusDisplayConsoleComponent, KestrelStatusDisplaySetTextMessage>(OnSetText);
        SubscribeLocalEvent<KestrelStatusDisplayConsoleComponent, KestrelStatusDisplaySetIconMessage>(OnSetIcon);
    }

    #region Displays

    private void OnDisplayMapInit(Entity<KestrelStatusDisplayComponent> ent, ref MapInitEvent args)
    {
        if (_station.GetOwningStation(ent) is { } station && TryComp<KestrelStationStatusDisplayComponent>(station, out var state))
            ApplyTo(ent, state);
        else
            _appearance.SetData(ent, KestrelStatusDisplayVisuals.Mode, KestrelStatusDisplayMode.Alert);
    }

    private void OnAlertLevelChanged(AlertLevelChangedEvent ev)
    {
        if (!TryComp<KestrelStationStatusDisplayComponent>(ev.Station, out var state)
            || state.Mode == KestrelStatusDisplayMode.Alert)
        {
            return;
        }

        state.Mode = KestrelStatusDisplayMode.Alert;
        UpdateStation(ev.Station, state);
    }

    /// <summary>
    /// Pushes the station's setting to all its displays and refreshes any open console menus.
    /// </summary>
    private void UpdateStation(EntityUid station, KestrelStationStatusDisplayComponent state)
    {
        var displays = EntityQueryEnumerator<KestrelStatusDisplayComponent, TransformComponent>();
        while (displays.MoveNext(out var uid, out _, out var xform))
        {
            if (CompOrNull<StationMemberComponent>(xform.GridUid)?.Station == station)
                ApplyTo(uid, state);
        }

        RefreshConsoleUis(station, state);
    }

    /// <summary>
    /// Sends the current state (display setting and EBS log) to every open console menu on the station.
    /// </summary>
    public void RefreshConsoleUis(EntityUid station, KestrelStationStatusDisplayComponent state)
    {
        var consoles = EntityQueryEnumerator<KestrelStatusDisplayConsoleComponent>();
        while (consoles.MoveNext(out var uid, out var console))
        {
            if (_station.GetOwningStation(uid) == station)
                UpdateConsoleUi((uid, console), state);
        }
    }

    private void ApplyTo(EntityUid display, KestrelStationStatusDisplayComponent state)
    {
        _appearance.SetData(display, KestrelStatusDisplayVisuals.Mode, state.Mode);
        _appearance.SetData(display, KestrelStatusDisplayVisuals.Message, state.Message);
        _appearance.SetData(display, KestrelStatusDisplayVisuals.Icon, state.Icon);
    }

    #endregion

    #region Console

    private void OnConsoleOpened(Entity<KestrelStatusDisplayConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (TryGetStationState(ent, out _, out var state))
            UpdateConsoleUi(ent, state);
    }

    private void OnSetAlert(Entity<KestrelStatusDisplayConsoleComponent> ent, ref KestrelStatusDisplaySetAlertMessage args)
    {
        if (!TryGetStationState(ent, out var station, out var state))
            return;

        state.Mode = KestrelStatusDisplayMode.Alert;
        UpdateStation(station, state);
        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(args.Actor):player} set the status displays back to the alert level");
    }

    private void OnSetText(Entity<KestrelStatusDisplayConsoleComponent> ent, ref KestrelStatusDisplaySetTextMessage args)
    {
        if (!TryGetStationState(ent, out var station, out var state))
            return;

        var text = KestrelStatusDisplayText.Clean(args.Text);
        if (text.Length == 0)
            return;

        state.Mode = KestrelStatusDisplayMode.Message;
        state.Message = text;
        UpdateStation(station, state);
        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(args.Actor):player} set the status displays to show: {text}");
    }

    private void OnSetIcon(Entity<KestrelStatusDisplayConsoleComponent> ent, ref KestrelStatusDisplaySetIconMessage args)
    {
        // Only icons this console offers; a modified client can't pick anything else.
        if (!ent.Comp.Icons.Contains(args.Icon) || !TryGetStationState(ent, out var station, out var state))
            return;

        state.Mode = KestrelStatusDisplayMode.Icon;
        state.Icon = args.Icon;
        UpdateStation(station, state);
        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(args.Actor):player} set the status displays to the icon {args.Icon}");
    }

    /// <summary>
    /// Finds the console's station and its status display state (creating the state if needed).
    /// </summary>
    public bool TryGetStationState(EntityUid console, out EntityUid station, out KestrelStationStatusDisplayComponent state)
    {
        state = default!;
        if (_station.GetOwningStation(console) is not { } owner)
        {
            station = default;
            return false;
        }

        station = owner;
        state = EnsureComp<KestrelStationStatusDisplayComponent>(owner);
        return true;
    }

    private void UpdateConsoleUi(Entity<KestrelStatusDisplayConsoleComponent> console, KestrelStationStatusDisplayComponent state)
    {
        _ui.SetUiState(console.Owner, KestrelStatusDisplayConsoleUiKey.Key,
            new KestrelStatusDisplayConsoleState(state.Mode, state.Message, state.Icon, console.Comp.Icons, state.EbsLog));
    }

    #endregion
}
