# Release preparation: AutoForestryDesignations 0.4.4

Prepared 2026-10-27. This release provides technical/experimental compatibility
with Captain of Industry Update 4.3 (0.8.8); extensive playtesting remains.
The owner confirmed successful game/save loading on 0.8.7d and 0.8.8 and
approved the current feature set for release. This does not certify extended
gameplay on either version.

- Release implementations built against complete 0.8.7d build 619 and 0.8.8
  build 622 references. Both resolve their game assembly versions, types and
  members. Selectors and implementations have Release optimization enabled.
- Release IL checks confirm automatic web API startup is disabled. Debug-only
  startup remains available to development builds.
- One ZIP includes the selector and both original-named implementation DLLs.
  Assets, translations and settings retain their existing installation paths.
- Public and private changelogs record the experimental compatibility scope.
  Supported translation bundles have complete coverage and matching numeric
  placeholders. Existing public Hub Markdown was checkpointed before review;
  no Hub page or package has been published during preparation.
- The selector and `PRE_4_3_COMPAT` paths are temporary. They can be retired
  after Update 5 or when older-game support is no longer needed.

The Release ZIP passed CRC, exact-content, UTF-8 manifest, DLL layout,
translation-key/placeholder and byte-equality checks. Input and archive hashes
are recorded in the adjacent versioned package JSON after the preparation
commits were pushed. KPIE additionally passed its 14-bundle and nine-name-pool
checks. No next-version advancement is
authorized until public distribution is confirmed.
