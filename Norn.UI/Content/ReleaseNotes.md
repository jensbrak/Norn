Release notes for the welcome window's "What's new" section. Everything
above the first `## ` heading (this paragraph included) is discarded by
the parser, so it's safe as editor-facing instructions rather than content.

Add one `## x.y.z` section per release, newest first, with its heading
text matching `AppInfo.Version` (i.e. the `Version` value in
`Directory.Build.props`) exactly. A version with no matching section here
falls back to the welcome window's own placeholder text.

## 1.2.0

Inventory tab improvements and tweaks:

- Cheated flag can now be cleared from items (right-click menu). Better visual emphasis on cheated items (red background).
- Support for "Forge of Potential": items above "max" are shown as "max + X" and quality can now be increased beyond "max".
- Drag and drop support, just like game do it: move, merge or swap items, and Shift + click to split a stack.
- Buttons now grouped by scope, subtly renamed for consistency and also have tooltips.
- Quick repair / fill stack is now Alt + left click, since left click now picks an item up.

Compatibility adjustments:

- Tested with Valheim 1.0.15.
- Updated item data for some items.

## 1.1.0

World- and inventory tab improvements:

- World tab: "Explore All" (now "Reveal world") and "Clear map data" moved into the map view.
- World tab: New "Clear received map data" action clears exploration received from other players without touching your own.
- Inventory tab: Now possible to add or edit exact amount of stack.
- Inventory tab: Add Item now tops up an existing stack of the same item first, then overflows into another slot if the amount doesn't fit.
- Inventory tab: Rearm / restock convenience buttons.
- Inventory tab: "Add item" is now "Add items" and supports successive adds.
- Inventory tab: Items the game itself flagged as cheated now show that in the tooltip.

## 1.0.0

First release of Norn, an open source Valheim character editor that:

- Loads and displays Valheim characters.
- Allows editing a sensible set of values for a character.
- Can run on Windows or Linux desktop.
- Supports Valheim 1.0 Deep North.