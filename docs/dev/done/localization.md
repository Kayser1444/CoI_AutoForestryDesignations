# Localization — Architecture Reference

## Feature summary

AFD uses the Captain of Industry `Loc.Str` system for all user-visible strings.
All strings are declared as `static LocStr` fields in `AfdLocalization.cs` and
rebound from translation JSON files at renderer-init time via
`CoI.AutoHelpers.Localization`.

## Source files

| File | Role |
|---|---|
| `AFD.Localization.cs` | `AfdLocalization` static class; all `LocStr` field declarations |
| `AFD.Mod.cs` | Registers late-apply callback in `Initialize`; calls `RegisterLocalizationLateApply` |
| `translations/` | Per-locale JSON translation files (e.g. `en.json`, `de.json`) |

---

## String declaration pattern

All strings are declared as `public static LocStr` fields:

```csharp
internal static class AfdLocalization
{
    public static LocStr ForestryDesignationsTitle = Loc.Str(
        "AFD_ForestryDesignationsTitle",
        "Forestry designations",
        "Title of the Forestry designations inspector panel.");
    // ...
}
```

The `Loc.Str` call registers the English default with the game engine at
startup. The key scheme is `AFD_` + PascalCase description. The third argument
is a translator-facing description that appears in export tooling.

---

## Runtime rebind

Translation files are applied at renderer-init state to ensure the game's
localization system has finished loading before AFD overwrites the `LocStr`
field values.

`RegisterLocalizationLateApply` (called in `IMod.Initialize`) registers a
`IGameLoopEvents.RegisterRendererInitState` callback that:

1. Resolves the `translations/` directory relative to the mod root.
2. Calls `ModTranslationLoader.LoadFromDirectory` (from
   `CoI.AutoHelpers.Localization`).
3. Calls `ModTranslations.Apply` with `AFD_` as the key prefix and
   `typeof(AfdLocalization)` as the target type so only AFD fields are rebound.
4. Logs a summary of the apply result via `AutoForestryDesignation.s_log`.

### Key prefix scoping

The `translationKeyPrefixes: new[] { "AFD_" }` parameter on
`ModTranslationsApplyOptions` restricts the rebind to fields whose `LocStr` IDs
begin with `AFD_`. This prevents accidental overwrites of vanilla or other mod
strings if a user puts a file with broad keys in the translations folder.

---

## Translation file format

Files are JSON arrays of string tuples:

```json
[
  ["AFD_Key", "Translated text"]
]
```

Two-item tuples are ordinary strings. Three-item tuples are reserved for
singular/plural translations:

```json
[
  ["AFD_CountFmt", "{0} tree", "{0} trees"]
]
```

Do not use the third item for translator notes or context; the loader treats it
as plural UI text. Files are loaded in alphabetical order; later files override
earlier ones for duplicate keys (controlled by
`DuplicateTranslationKeyBehavior.LastWins`).

### Format placeholders

Prefer numeric .NET placeholders such as `{0}`, `{1}`, and `{2:P0}` for all new
localized strings. Avoid named placeholders such as `{entity}` unless a vanilla
API explicitly requires that exact token. Numeric placeholders are harder for
machine translation to mistranslate and are validated naturally by
`string.Format` call sites.

Translators may reorder numeric placeholders to fit the target language, but
must preserve the exact placeholder spelling and format suffix. For example,
`{2:P0}` must remain `{2:P0}`, not `{2}` or a translated word.

### Rich text in UI strings

AFD tooltips support Unity rich text. The following tags were verified in-game
in a translated tooltip:

| Markup | Result |
|---|---|
| `<b>text</b>` | Bold |
| `<i>text</i>` | Italic |
| `<u>text</u>` | Underline |
| `<color=#66ff66>text</color>` | Hex color |
| `<size=120%>text</size>` | Relative text size |
| `\n` | Line break |

Use rich text sparingly and only when it improves readability in a tooltip or
other UI surface. Keep tags balanced, and preserve placeholders exactly when
wrapping formatted text around them.

The `TranslationTemplateExporter` from `CoI.AutoHelpers.Localization` can
generate a template file from the declared `LocStr` fields for use by
translators.

---

## Adding a new string

1. Add a `public static LocStr` field to `AfdLocalization` with a key following
   the `AFD_` prefix scheme and a descriptive English default.
2. Rebuild — the field is picked up automatically because `Apply` reflects over
   `typeof(AfdLocalization)`.
3. Run the template exporter (or `dotnet build` with the export target) to update
   the English template in `translations/`.
