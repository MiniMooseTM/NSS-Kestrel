# Kestrel mapping copy/paste tool

cmd-copypaste-desc = Opens the mapping copy/paste tool (needs the Mapping admin permission).
cmd-copypaste-help = Usage: copypaste

kestrel-copypaste-window-title = Copy / Paste
kestrel-copypaste-window-copy = Copy area
kestrel-copypaste-window-paste = Paste
kestrel-copypaste-window-rotate = Rotate
kestrel-copypaste-window-flip-h = Flip H
kestrel-copypaste-window-flip-v = Flip V
kestrel-copypaste-window-lock = Lock to source grid
kestrel-copypaste-window-undo = Undo last paste ({ $count } stored)
kestrel-copypaste-window-transform = Turned { $degrees }° clockwise{ $flipped ->
    [1] , flipped
   *[other] {""}
}

kestrel-copypaste-hint-copy = Left-drag a box to copy. Right-click to stop.
kestrel-copypaste-hint-paste = Left-click to paste. Middle-click rotates, Ctrl+middle-click flips. Right-click to stop.

kestrel-copypaste-copying = Copying...
kestrel-copypaste-copied = Copied { $width }×{ $height }: { $tiles } tiles, { $entities } entities, { $decals } decals.
kestrel-copypaste-pasted = Pasted.
kestrel-copypaste-undone = Undid the last paste.
kestrel-copypaste-no-grid = There's no grid there.
kestrel-copypaste-too-big = That box is too big. Max { $max } tiles per side.
kestrel-copypaste-nothing-copied = Nothing copied yet.
kestrel-copypaste-wrong-grid = Locked to the source grid: you can only paste onto the grid you copied from.
kestrel-copypaste-nothing-to-undo = Nothing to undo.
kestrel-copypaste-undo-grid-gone = That grid no longer exists, so the paste can't be undone.
kestrel-copypaste-copy-failed = Copy failed. Check the server log.
kestrel-copypaste-paste-failed = Paste failed. Check the server log.
kestrel-copypaste-paste-entities-failed = Tiles and decals were pasted, but the entities failed. Check the server log.
kestrel-copypaste-undo-entities-failed = Undo restored the tiles, but some entities could not be restored. Check the server log.
