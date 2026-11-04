# AFD localization

AFD uses native mod localization on Update 4.3 (0.8.8). Older games retain the
AutoHelpers late splice/rebind path. This pilot changes translation IDs only;
it does not rename prototypes, saved types, settings or shortcut IDs.

## String and file ownership

`src/AFD.Localization.cs` declares the 93 AFD strings. Keys use the manifest ID
`AutoForestryDesignations.` followed by a descriptive 4–64-character name made
of letters, digits and underscores. Do not introduce nested dotted keys.
All locale bundles and `.source-strings.json` use the same IDs. Existing
translated values were preserved in the migration.

Files are native tuple-array JSON: `["key", "translated text"]`. Preserve
numeric format placeholders and their format suffixes. Use the native filenames
`de.json`, `en.json`, `es.json`, `it.json`, `pt_BR.json`, `ru.json`, `sv.json`
and `zh_Hans.json`. Keep one source per locale. AutoHelpers authoring commands
still use `pt` and `zh` work-order fields and resolve the native filenames.

## Startup and cached text

The game loader registers `<mod root>/Translations` and merges the selected
language after mod loading and prototype registration. Windows treats the
existing `translations` directory as that directory. The version selector scans
the selected AFD payload within the native loading scope. Do not remove that
scan when changing the old localization workaround.

The 4.3 payload excludes AutoHelpers' localization runtime sources: no private
dictionary writes, rebind pass or late translation apply runs. Renderer-init
still builds AFD's settings tabs and resolves render services after native
merging. The defaults tab uses stable ID `defaults`. The shared host resets its
window and registrations for each new world UI so a language change/reload does
not retain translated tabs or factories from the outgoing world. `LocStr` follows the current language; `LocStrFormatted` and already
built UI retain a snapshot. Rebuild or refresh captured text where needed rather
than assuming a language change updates an existing label. Shared Mod Settings
chrome belongs to AutoHelpers and is outside this AFD-only string migration.

Before 4.3, the renderer-init callback applies the original AutoHelpers pipeline.
Locale selection now also checks `CurrentLangInfo.FileName`, so the same native
Portuguese and Chinese bundles work without duplicate legacy filenames.
The old fields stay mutable because that branch must rebind them.

## Translation maintenance

Use the shared `CoI_AutoHelpers/tools/translate.py` workflow. New strings need a
prefixed ID, English fallback and contextual comment; preserve existing locale
values while migrating keys. Translation quality review remains separate from
coverage. Use Swedish for the first in-game check.

## Verification

Build both game-reference variants before deploying. The existing dual-build
tool accepts `-ModIds AutoForestryDesignations` and `-Deploy`; it does not release
or package the mod. The preserved 0.8.7d reference snapshot is local test data.

`tools/LocalizationContracts` runs the actual game localization implementation
and compiled AFD fields outside Unity. Supply the matching Managed directory,
AFD DLL and repository root. For the native variant it checks all seven non-English
bundles, strings captured before merging, language changes, English fallback,
delayed versus captured formatting, and absence of the legacy runtime adapter.
For the legacy variant pass one locale filename as the fourth argument and run
each locale in a fresh process (`sv.json`, `pt_BR.json`, `zh_Hans.json`).
These checks do not establish rendered UI or save/load behavior. Record those
separately in `docs/test/`.

The historical rationale is DeznekCZ's
[mod-localization proposal](https://coigame.com/Topic/54/Mod-localization): frozen
localization values and already-captured UI text were separate failure modes.
4.3 resolves the former; migration must still account for the latter. Update 5
removes legacy ownership attribution, so keep new AFD IDs prefixed now and retire
the older-game branch only when older-game support ends.
