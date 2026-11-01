// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Toggleable;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Reflection;

namespace Content.Client._Kestrel.Weapons;

/// <summary>
/// Applies <see cref="KestrelModeLightVisualsComponent"/>: whenever the gun's appearance data
/// changes, works out the current mode's colour and tints the light layer with it.
/// </summary>
/// <remarks>
/// This only changes the layer's colour. Which charge picture is shown (and whether it's hidden
/// when empty) is still handled by the normal MagazineVisuals system, so the two don't conflict.
/// </remarks>
public sealed class KestrelModeLightVisualsSystem : VisualizerSystem<KestrelModeLightVisualsComponent>
{
    [Dependency] private readonly IReflectionManager _reflection = default!;

    protected override void OnAppearanceChange(EntityUid uid,
        KestrelModeLightVisualsComponent component,
        ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        Color? color = null;

        // Fire-mode guns: the server stores the current mode's projectile id.
        if (AppearanceSystem.TryGetData<string>(uid, BatteryWeaponFireModeVisuals.State, out var mode, args.Component)
            && component.FireModeColors.TryGetValue(mode, out var modeColor))
        {
            color = modeColor;
        }
        // Ammo-selector guns: the shared system stores the chosen ammo's colour directly.
        else if (AppearanceSystem.TryGetData<Color>(uid, ToggleableVisuals.Color, out var selectorColor, args.Component))
        {
            color = selectorColor;
        }

        // No mode reported yet (e.g. just spawned): keep the colour set on the layer in YAML.
        if (color == null)
            return;

        // Layer names like "enum.GunVisualLayers.MagUnshaded" refer to an enum value; anything
        // else is a plain string key.
        int layer;
        bool found;
        if (_reflection.TryParseEnumReference(component.Layer, out var enumKey, false))
            found = SpriteSystem.LayerMapTryGet((uid, args.Sprite), enumKey, out layer, false);
        else
            found = SpriteSystem.LayerMapTryGet((uid, args.Sprite), component.Layer, out layer, false);

        if (found)
            SpriteSystem.LayerSetColor((uid, args.Sprite), layer, color.Value);
    }
}
