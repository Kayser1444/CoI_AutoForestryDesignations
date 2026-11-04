# Release preparation 0.4.5 — 2026-11-04

Prepared against Captain of Industry 0.8.8a b628 and retained 0.8.7d b619 references. This is focused compatibility verification; extensive gameplay testing remains outstanding.

## Completed checks

- Debug and Release version-switching builds: both implementation targets and loader, zero warnings/errors
- Current and legacy compiled-reference checks, including nested type/member references
- Shared settings registration/reset contracts and native/legacy localization contracts
- Manifest schema, locale keys/placeholders and translation coverage checks
- Existing b628 save loaded with all four candidate mod versions
- Same-process English → Swedish → English reload: seven unique settings tab identities, recreated settings windows and localized AFD controls; original save hash unchanged
- Test game closed normally and original language preference restored

Release evidence is retained locally in the workspace .scratch/shipping-20261104 directory. The aggregate runner completed 23 checks successfully; schema validation completed 46 assertions. Live testing used Debug payloads; Release payloads were compiled and reference-checked separately.

## Scope and limits

No public upload or Hub page publication is part of preparation. Historical Hub checkpoints remain preserved. No extended simulation, complete visual review, or new older-game runtime session was performed. Existing earlier owner confirmation of legacy game/save loading is separate from these build checks.

## Verified shipping package

- ZIP: AutoForestryDesignations-0.4.5.zip
- Entries: 18; bytes: 1155485
- SHA-256: 2CFCD84F4A61D357DF6468DBB490D3E6850806D7CB7698EFB3C1EF1A06F55D12
- Exact runtime whitelist and every entry's source bytes verified; both implementation DLLs match the Release build overlays
- No debug symbols, authoring tools, laboratory proposals or translation metadata in the ZIP
- Preparation commit: d61f297; shared AutoHelpers commit: 765d2c1
