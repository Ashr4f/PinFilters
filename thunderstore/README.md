# PinFilters

Too many icons on the map? Open the large map, click Map filters next to the vanilla toggles (or press F), and uncheck what you do not want to see. The list has a search field and shows each icon next to its name.

Pins are grouped by their icon, so every pin family gets its own line whatever mod created it.
Unchecking hides the pins visually, nothing is ever deleted, and the choices are remembered
between sessions. Hiding is reapplied every frame, so mods that constantly recreate their pins
cannot bring hidden icons back.

## Configuration

| Setting | Default | Description |
| --- | --- | --- |
| Enabled | true | Master switch. |
| Panel Key | F | Shows or hides the panel while the large map is open. |
| Max Height | 420 | Maximum panel height in pixels, the list scrolls beyond that. |
| Button Offset X | 0 | Horizontal offset of the map toggle. |
| Button Offset Y | 64 | Vertical offset of the map toggle. Raise it if labels overlap. |
| Group By Icon | true | Group pins by icon. Off groups them by pin type only, which merges different mod pins together. |
| Hidden Groups |  | Groups currently unchecked, written automatically. |

## Install

BepInEx plugin: `BepInEx/plugins/PinFilters.dll`, or via r2modman. Client side only.

## Credits

Built for the "Les Fous du Bus" server.
