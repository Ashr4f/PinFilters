# PinFilters

Too many icons on the map? Open the large map, click Map filters next to the vanilla toggles (or press F), and uncheck what you do not want to see. The list has a search field, shows each icon next to its name, highlights the row under the cursor and dims what is hidden.

Pins are grouped by their label, so every pin family gets its own line whatever mod created it, even when several families share the same icon.
Unchecking hides the pins visually, nothing is ever deleted, and the choices are remembered
between sessions. Hiding is reapplied every frame, so mods that constantly recreate their pins
cannot bring hidden icons back.

## Configuration

| Setting | Default | Description |
| --- | --- | --- |
| Enabled | true | Master switch. |
| Panel Key | F | Shows or hides the panel while the large map is open. |
| Max Height | 420 | Maximum panel height in pixels, the list scrolls beyond that. |
| Button Width | 150 | Width of the map button. |
| Button Height | 36 | Height of the map button. |
| Button Offset X | 0 | Horizontal offset of the map toggle. |
| Button Offset Y | 136 | Vertical offset of the map toggle. Raise it if labels overlap. |
| Group By Icon | true | Use the icon name as fallback when a pin has no label. Off falls back to the pin type. |
| Search Aliases |  | Extra search words per group, format group=word1\|word2, comma separated. |
| Excluded Groups | Shout | Groups never listed, they stay visible on the map. |
| Hidden Groups |  | Groups currently unchecked, written automatically. |

## Install

BepInEx plugin: `BepInEx/plugins/PinFilters.dll`, or via r2modman. Client side only.

## Credits

Built for the "Les Fous du Bus" server.
