# Changelog

## 1.0.19

- The list is filled the moment the panel opens. It used to wait for the game to refresh its pins, which some performance mods throttle, leaving the panel empty for a few seconds.
- The map button no longer shows up greyed depending on when the map was first opened. The components inherited from the source button that dim it on their own are stripped, and the bright look is reapplied every frame.
- Naming a new pin works again when everything is hidden. The pin was created inside a hidden group, so it was made invisible on the same frame, along with the field waiting for its name.

## 1.0.18

- Unchecked groups are remembered in `config/PinFilters/hidden.txt` instead of the config file. A modpack update overwrites config files, which is why the choices came back all checked after an update. Existing choices are imported once.
- The map button keeps a bright look: a dim tint inherited from the source button is reset, on the label, on the graphics and in the colour block.
- The map no longer follows the cursor when a drag started in the panel and left the list. The flag that tracks an ongoing interaction was declared but never set, so only the cursor position was taken into account.
- Much cheaper hiding pass. The group of a pin is resolved once per label instead of going through the localization system for every pin on every pass, the hidden list is read once per pass, and pins that are neither hidden nor previously hidden are skipped entirely. On a map carrying thousands of pins the pass now costs almost nothing when nothing is hidden.
- The group list is only refreshed while the panel is open, which was the other half of the per pin work.

## 1.0.17

- Checking or unchecking a group now updates the map right away. It used to wait for the cursor to leave the list or the panel to close, because the pins are refreshed by the game update that the panel deliberately holds back.
- Typing in the search field no longer walks, jumps or swings. The freeze that was in place until 1.0.10 is back, and it covers the whole time the panel is open.
- The panel key types its letter when the search field has the keyboard, instead of closing the list. Searching for Fer or Framb works with the default key.

## 1.0.16

- The map button is no longer greyed out when the button it was cloned from happened to be greyed at that moment. It forces itself back to the enabled look, keeps a readable label, and ignores the dimming of the row it sits in.

## 1.0.15

- Config table in the readme: both button offsets default to 0.

## 1.0.14

- Own mod icon.

## 1.0.13

- Button placement adjusted by one pixel on both axes, so the offsets stay at 0.

## 1.0.12

- The button sits above the cartography row out of the box. The offsets are now fine tuning on top of that placement and default to 0.

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
