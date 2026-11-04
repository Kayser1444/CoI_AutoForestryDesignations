# AFD native-localization pilot — 2026-10-31

Implemented for AFD 0.4.5 (unreleased). No package, version bump, manifest changes,
prototype/type/shortcut ID changes or save writes were requested or performed.

## Changes

- Migrate all 93 AFD localization IDs to `AutoForestryDesignations.<name>` in
  source, all bundles and source-tracking metadata, without collisions.
- Preserve every non-English tuple value and source-tracking value exactly.
- Align five pre-existing damaged English symbol values with the compiled C#
  fallback. Fix Unicode escape decoding in the authoring parser so sync preserves
  those symbols instead of emitting text such as `u221e`.
- Rename `pt.json` to `pt_BR.json` and `zh.json` to `zh_Hans.json`; one file per
  locale, no generated duplicates. Authoring commands support both conventions.
- Exclude the legacy localization runtime sources and late apply from AFD's
  4.3 payload. Keep the original version-selector scan and renderer/UI lifecycle.
- Preserve the legacy pipeline, adding native filename matching before neutral
  fallback. Ignore the game's native authoring export files as locale bundles.

## Automated verification

- Debug AFD implementation builds and selector: 0 warnings/errors against
  0.8.7d build 619 and installed 0.8.8 build 624. Both implementations deployed.
- Shared helper Debug builds: both reference sets, 0 warnings/errors.
- ATD, BDT and KPIE Debug builds: current API, without deployment; passed.
- Real native localization engine and compiled AFD fields: all seven non-English
  bundles, strings created before merging, repeated language changes, English
  fallback, cached versus deferred formatting, valid ownership/IDs and absence of
  the legacy adapter. Passed. Run `tools/LocalizationContracts` with the matching
  Managed directory, implementation DLL and repository root.
- Actual 0.8.7d splice/rebind: Swedish, Portuguese and Chinese, each in a fresh
  process; all 93 fields rebound with the existing translated values. Passed.
- Python temporary-fixture tests cover native/legacy filenames, ambiguous pairs,
  extraction/application/sync/status and Unicode source extraction. Passed.

## Live game evidence

Both sessions loaded the existing Strategic Marina save, paused at tick 109819,
game ID `2983648954427638228`, with the maintained mods together.

- English: session `8004965437294754288`, log
  `26-10-31_16-55-40_1522.log`. AFD selected its 4.3 payload, DLL timestamp
  `2026-10-31 16:51:46`, and reported native locale `en-US` before `Loaded game`.
- Native `export_mod_translations AutoForestryDesignations` succeeded: 93 strings,
  all properly prefixed, with no legacy/malformed-ID warning. Generated source and
  template files were copied to local scratch evidence and removed from the
  translation directory after checking.
- Swedish: session `2375676471667220205`, log
  `26-10-31_16-59-50_7452.log`. The same AFD DLL reported native locale `sv-SE`
  and the game reached loaded-world/API readiness. A test-driver check used a
  stale shell exit code afterwards, so the second export did not run; startup
  success is established by the readiness response and native locale/load log.
- Both managed test processes closed normally without force termination or
  saving. The temporary Swedish language preference was restored to the exact
  original English binary registry value after shutdown.

## Remaining visual checks

The above establishes compilation, real localization behavior and live startup/
save-load integration. It does not establish the rendered appearance of every
AFD panel. Manually inspect/reopen forestry designations, forestry information, tooltips
and confirmation dialogs in Swedish, and repeat against the old game when
available. The later same-process settings-window check below covers AFD
settings labels and tab registration through reload. Existing UI text is a snapshot; no automatic live
repainting of an already-open panel is claimed. Shared Mod Settings chrome and
other mods' key migrations are outside the AFD pilot.

## Same-process language reload — 2026-10-31

Completed after waiting for the `Machine Operators` thread to finish testing.
Its completed managed session closed normally without saving. Settings now use
stable `(ModId, TabId)` identities instead of translated titles. A new world UI
detaches the old escape handler, clears the window/factories and re-registers
tabs; only stable selection IDs survive. Older callers retain order-based IDs
and forwarding overloads. AFD explicitly uses `defaults`. Common labels allow
re-registration of their existing IDs when the window is recreated.

Verification:

- All four maintained mods' Debug payloads build for 0.8.7d b619 and 0.8.8 b624,
  zero warnings/errors, and were deployed. Binary-reference checks pass on both
  reference sets. AFD Release also compiles without deployment or packaging.
- `CoI_AutoHelpers/tools/SettingsContracts` passes 12 checks against production
  registration code: translated title replacement, explicit/legacy stable IDs,
  changed display order and dropping old-world factories. Framework types are
  stubbed; live window behavior is established separately below.
- Debug commands `afd_localization_inspect` and `afd_localization_reload` inspect
  the actual active settings window and set the same language preference as the
  picker before calling native `IMain.LoadGame`. The test does not mouse-click
  the picker or live-repaint text in the outgoing world. Commands are absent from
  Release and pre-4.3 builds.

PID `196996` loaded the same Strategic Marina save three times without an
application restart. Game ID stayed `2983648954427638228` and the
test save's SHA-256 stayed unchanged.

| Language | Native session ID | Settings window identity | Tabs |
|---|---|---|---|
| English | `2739993604137658059` | `189160448` | 7 |
| Swedish | `2920433529813409315` | `1243405864` | 7 |
| English again | `8478374316273143682` | `498614598` | 7 |

The host belonged to **DesignerToolkit** throughout, exercising AFD's primitive
cross-assembly forwarding. Every snapshot had exactly one
`auto-forestry-designations/defaults` registration, with its title changing
`Defaults` -> `Standardvärden` -> `Defaults`. The active window's actual UI tree
changed `Fertile tiles only` -> `Endast bördiga rutor` -> `Fertile tiles only`,
along with other AFD controls. Fresh window identities and stable IDs/counts
confirm scene reset without duplicate translated tabs. No shared-label duplicate
or settings factory/registration failure appeared in the log.

Log: `26-10-31_22-44-49_4489.log`. Driver, snapshots and JSON results are
local evidence in `AutoTerrainDesignations/.scratch/afd-language-reload/`.
The test game closed normally without saving or force termination. The exact
original UI-language preference bytes were restored. An initial driver attempt
used an incompatible live-log sharing mode and stopped before inspection; the
corrected rerun produced the successful result above.

This verifies settings text through world reload in one process. Screenshot
font/layout appearance, all forestry inspectors/tooltips, old-game live language
switching and changing language without a scene reload remain outside this test.
