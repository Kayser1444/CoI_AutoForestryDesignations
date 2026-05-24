# 🌲 Kayser's Automatic Forestry Designations v0.1.5

This is a follow-up release for AFD's Forestry information panel and per-tower controls, focused on making forestry towers remember what you told them, making the tree overview easier to read, and making the growth chart actually useful while managing harvests.

The short version: per-tower settings now persist through save/load, the Forestry information panel looks more like a native CoI panel, and the growth breakdown can highlight and mark trees directly from the chart.

## 💾 Persistent Tower Settings

AFD now saves per-tower Forestry designations settings with the vanilla save file using CoI AutoHelpers JSON state storage.

That means tower-specific options and collapsed panel states survive quit/reload. They also remain preserved in the save if the mod is temporarily removed and later added again.

The saved state is intentionally compact: AFD stores only values that differ from the current global defaults.

## 📊 Better Forestry Information Panel

The Forestry information panel has had a visual pass so it feels less like a prototype and more like part of the game:

- Cards now use a speckled panel background texture with a subtle tint to better match the game's panel style.
- The growth breakdown bar has cleaner alignment, a clearer border, and improved spacing.
- The Trees KPI now uses a live green display box that refreshes the current managed-tree count while the inspector is open.

The Trees tooltip also now makes the two numbers clearer: the first number is the live count of current managed trees, and the second is the estimated capacity based on valid planting positions.

## 🪓 Interactive Growth Stages

The growth distribution bar is now more than a passive chart.

Hover a growth-stage segment to highlight the corresponding trees in the world. The highlight uses a brighter tint of that segment's own colour, so the link between the chart and the actual forest is easier to read.

Click a segment to mark or unmark those highlighted trees for harvest. AFD also opens the **Tree harvesting** overlay so the affected trees are visible, then hides the overlay again when the inspector closes.

The segment tooltips have been cleaned up too:

- growth brackets now have names like **Young**, **Growing**, **Maturing**, and **Fully mature**
- tooltip text is split into clearer lines
- harvest-threshold wording is shorter
- hover/click instructions are localized

## 🌐 UI Wording and Localization

Portuguese translation has been added.

Several English labels were adjusted toward vanilla-style sentence case:

- **Only fertile tiles** is now **Fertile tiles only**.
- **Only reachable tiles** is now **Reachable tiles only**.
- **Max tiles** is now **Maximum number of designations**.

Localized strings and tooltips were updated to match across the included languages.

## 📦 Compatibility

AFD remains compatible with existing saves and does not require starting a new game.

The new per-tower state is save-backed and stored through vanilla save data. Existing global settings continue to work as before.

Thanks to everyone testing the Forestry information panel in real saves. v0.1.5 is a "make the overview earn its place in the inspector" release: less forgetting, clearer tree data, and faster harvest decisions from the chart itself.
