// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.AlertLevel;
using Content.Shared._Kestrel.StatusDisplay;
using Content.Shared.AlertLevel;
using Robust.Client.GameObjects;

namespace Content.Client._Kestrel.StatusDisplay;

/// <summary>
/// Draws the status display: alert picture, icon, or text.
/// </summary>
/// <remarks>
/// How it works:
/// - The server sets three appearance values (mode, text, icon). When they change we show the right
///   layers: the alert layer (owned by the upstream AlertLevelDisplaySystem), the "kestrelIcon" layer,
///   or the character layers. We run after AlertLevelDisplaySystem because it shows the alert layer
///   whenever the display has power; we hide it again if we're showing something else.
/// - Text: the screen has a few character slots, each a sprite layer with one letter of Aurora's
///   font (status_display_font.rsi). Short text just sits there. Longer text "ticks": every StepTime
///   seconds the window over the text moves one character, like an LED sign, then wraps round after
///   a short gap. (Sprite layers can't be cropped, so sliding pixel by pixel would make letters pop
///   in and out at the screen's edges; stepping a whole character avoids that.)
/// </remarks>
public sealed class KestrelStatusDisplayVisualsSystem : EntitySystem
{
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private const string IconLayer = "kestrelIcon";
    private const string CharLayerPrefix = "kestrelChar";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<KestrelStatusDisplayVisualsComponent, AppearanceChangeEvent>(OnAppearanceChange,
            after: new[] { typeof(AlertLevelDisplaySystem) });
    }

    private void OnAppearanceChange(Entity<KestrelStatusDisplayVisualsComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        var comp = ent.Comp;
        comp.Mode = _appearance.TryGetData<KestrelStatusDisplayMode>(ent, KestrelStatusDisplayVisuals.Mode, out var mode, args.Component)
            ? mode
            : KestrelStatusDisplayMode.Alert;
        comp.Powered = !_appearance.TryGetData<bool>(ent, AlertLevelDisplay.Powered, out var powered, args.Component) || powered;

        if (_appearance.TryGetData<string>(ent, KestrelStatusDisplayVisuals.Message, out var text, args.Component) && text != comp.Text)
        {
            comp.Text = text;
            comp.Position = 0;
            comp.Accumulator = 0;
        }

        var sprite = (ent.Owner, args.Sprite);

        // Alert picture: only in alert mode (and only with power, which AlertLevelDisplaySystem already handles).
        if (_sprite.LayerMapTryGet(sprite, AlertLevelDisplay.Layer, out var alertLayer, false))
        {
            if (comp.Mode != KestrelStatusDisplayMode.Alert)
                _sprite.LayerSetVisible(sprite, alertLayer, false);
        }

        // Icon.
        if (_sprite.LayerMapTryGet(sprite, IconLayer, out var iconLayer, false))
        {
            var showIcon = comp.Powered && comp.Mode == KestrelStatusDisplayMode.Icon;
            if (showIcon && _appearance.TryGetData<string>(ent, KestrelStatusDisplayVisuals.Icon, out var icon, args.Component))
                _sprite.LayerSetRsiState(sprite, iconLayer, icon);

            _sprite.LayerSetVisible(sprite, iconLayer, showIcon);
        }

        DrawText(ent, args.Sprite);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var query = EntityQueryEnumerator<KestrelStatusDisplayVisualsComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var comp, out var sprite))
        {
            // Only text too long for the screen moves.
            if (comp.Mode != KestrelStatusDisplayMode.Message || !comp.Powered || comp.Text.Length <= comp.CharacterSlots)
                continue;

            comp.Accumulator += frameTime;
            if (comp.Accumulator < comp.StepTime)
                continue;

            comp.Accumulator -= comp.StepTime;
            comp.Position = (comp.Position + 1) % (comp.Text.Length + comp.Gap);
            DrawText((uid, comp), sprite);
        }
    }

    /// <summary>
    /// Puts the right letter in each character slot, or hides the slots when not showing text.
    /// </summary>
    private void DrawText(Entity<KestrelStatusDisplayVisualsComponent> ent, SpriteComponent spriteComp)
    {
        var comp = ent.Comp;
        var sprite = (ent.Owner, spriteComp);
        var show = comp.Powered && comp.Mode == KestrelStatusDisplayMode.Message;

        // Short text is centred and still; long text is a window starting at Position.
        var text = comp.Text;
        var fits = text.Length <= comp.CharacterSlots;
        var lead = fits ? (comp.CharacterSlots - text.Length) / 2 : 0;
        var loopLength = text.Length + comp.Gap;

        for (var slot = 0; slot < comp.CharacterSlots; slot++)
        {
            if (!_sprite.LayerMapTryGet(sprite, CharLayerPrefix + slot, out var layer, false))
                continue;

            var ch = ' ';
            if (fits)
            {
                var i = slot - lead;
                if (i >= 0 && i < text.Length)
                    ch = text[i];
            }
            else
            {
                var i = (comp.Position + slot) % loopLength;
                if (i < text.Length)
                    ch = text[i];
            }

            var visible = show && ch != ' ';
            if (visible && KestrelStatusDisplayText.Glyphs.TryGetValue(ch, out var state))
                _sprite.LayerSetRsiState(sprite, layer, state);

            _sprite.LayerSetVisible(sprite, layer, visible);
        }
    }
}
