// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Server._Kestrel.Wall;

/// <summary>
/// When the entity appears on the map, if its wall mount faces into a wall (or into space),
/// turn it to face an open floor tile next to it instead.
/// </summary>
/// <remarks>
/// For posters and signs. They're one-sided (WallMount arc 180), and which side they show on
/// comes from their rotation. Most were mapped unrotated, which counts as facing south, so any
/// poster on a wall whose room isn't to the south would be invisible from inside the room.
/// Correctly rotated ones are left alone.
/// </remarks>
[RegisterComponent]
public sealed partial class KestrelWallMountAutoFaceComponent : Component;
