# Changelog

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
