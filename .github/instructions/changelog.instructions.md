---
description: Instructions for maintaining the AutoForestryDesignations change log.
applyTo: "change log.md"
---

# Change Log Maintenance Rules

## File location
`change log.md` in the workspace root.

## Format
- Top-level heading: `# CHANGE LOG`
- Sub-heading per release: `## <semver>` (e.g. `## 0.1.13`)
- Top-level bullet entries use `*`.
- Sub-bullets use 4 spaces followed by `-`.
- No date stamps on release headings.
- Unreleased / in-progress work goes in the **first section** at the top of the file, titled with the next version number. Do not use an "Unreleased" or "WIP" label.

## Content rules
- One bullet per user-visible change (feature, fix, or behavioral change).
- Internal refactors, code cleanup, and build tooling changes are omitted unless they affect behavior.
- API additions visible to external modders are documented (e.g. new `AutoForestryDesignationsApi` methods).
- Settings file additions are documented with the key name(s) and what they control.
- Start each bullet with a capital letter; no trailing period.
- Use **bold** for setting names and UI control names (e.g. **Only fertile tiles**, **Max tiles**).
- Fixes start with `Fixed:` followed by a short description.
- Sub-bullets are used when a single feature has multiple related details worth calling out.

## Versioning
- Version numbers follow `MAJOR.MINOR.PATCH` (currently in `0.x.y` range).
- Check the manifest version and existing change log entries to determine the current version number:
- Increment PATCH for bug fixes and minor additions.
- Increment MINOR for new features or behavioral changes that are noticeable to the user.
- After a release is packaged (zip exists in `artifacts/` matching the manifest version), start a new section for the next version.

## Example entry
```markdown
## 0.1.13
* **Forestry Designations** panel can now be embedded in external mod inspectors via `AutoForestryDesignationsApi.BuildDesignationPanel`
* Added **AFDsettings.json** defaults for **Only fertile tiles**, **Avoid tiles with trees**, **Avoid mining designations**, **Max tiles**, and **Mark ready for harvest**
```
