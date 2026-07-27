# Changelog

## 1.0.11

- Clicking or scrolling the list can no longer move the map: the map update is skipped while the panel has the mouse, instead of relying on a method that some game versions do not have.
- Readable panel: opaque background, framed title, bigger rows, larger text, solid All and None buttons, bigger icons. New Width and Row Height settings.

## 1.0.10

- The character no longer moves while the panel is open. The keys that close the map keep working.
- Dragging a scrollbar or a field in the panel no longer moves the map behind it.
- Clicking outside the panel closes it, and the click still reaches the map.
- New setting Search Aliases: extra words per group, so a player finds a pin with the word they know whatever the label on the map.
- New setting Excluded Groups, with Shout excluded by default: those pins stay on the map but are not listed.

## 1.0.9

- Groups are built on the pin label instead of the icon, so families sharing an icon are no longer hidden together, and every family shows up on its own line.
- Counters in labels are ignored, so the same family is one entry whatever the amount.
- Pings, shouts, players and death markers are grouped by type, so player messages never appear as entries.
- Search matches the label, the group key and the icon name, anywhere in the text.

## 1.0.8

- Rows highlight on hover and the whole row is clickable, not just the checkbox.
- Hidden groups are dimmed in the list, and the title shows how many are hidden.
- The search field has a placeholder and a clear button.

## 1.0.7

- Rows are laid out with explicit rects, so the checkbox, the icon and the label sit on the same baseline.
- Default button offset raised again to clear the cartography table row.

## 1.0.6

- Fixed the invisible map button: the cloned crafting button inherited the inactive state of the closed crafting panel. It is forced active and given its own size, with new Button Width and Button Height settings.

## 1.0.5

- Real button with a background, cloned from the crafting panel, instead of a bare checkmark. Falls back to a map toggle clone if that button cannot be found.
- The panel is aligned on the right edge of the button and opens leftwards and upwards.
- Default button offset raised so labels no longer overlap.
- Scrolling in the panel no longer moves or zooms the map: input is refused while the cursor is over the panel.

## 1.0.4

- The toggle uses a map pin icon instead of the checkmark inherited from the cloned vanilla toggle.

## 1.0.3

- Rows show the pin name as the game displays it, so the list follows the user language. Nameless pins fall back to their vanilla type name.
- The panel opens right above the map toggle instead of the screen corner.
- Scrolling the list no longer zooms the map.
- Button Offset X and Y replace the single offset, so the toggle can be moved out of the way of other labels.

## 1.0.2

- A toggle is added on the map next to the vanilla ones, so the panel no longer depends on knowing a key.
- Panel reworked: search field, icon next to every name, capped height with scrolling.
- Filters pause while a portal destination picker is open, so its markers are never hidden.

## 1.0.1

- The panel is drawn by the plugin instead of patching a method that does not exist, which threw at startup.

## 1.0.0

- Initial release: map panel with one checkbox per pin group, grouping by icon so pins from any mod are covered, visual hiding reapplied every frame, choices remembered.
