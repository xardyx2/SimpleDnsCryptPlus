# Translations

**The translation workflow this project used is gone, and the copy here is frozen.** Read this before
opening an issue about missing or wrong strings in a non-English UI — the answer is usually "the source
of that string is no longer reachable".

## What happened

Upstream Simple DNSCrypt managed its 34 languages in a POEditor project owned by Christian Hermann
(bitbeans), the original author. That account is not part of this fork, so nothing can be exported from
it, re-synced with it, or pushed back to it. The old `Translations/` import tooling in the repository
went with it.

What remains — and what is now authoritative — is the committed set of resource files:

```
SimpleDnsCrypt/Resources/Translation.resx          the neutral/invariant file
SimpleDnsCrypt/Resources/Translation.<lang>.resx   34 language files
```

`LocalizeDictionary` resolves through `ResourceManager`, and the language menu is built from
`LocalizationEx.GetSupportedLanguages()`. Three tests keep those three things honest
(`Tests/LocalizationCoverageTests.cs`): every offered language has a file, every file is offered by
something, and the format strings the loader passes to `string.Format` resolve for every offered
culture.

## Why some strings are English on a German screen

Anything added after the freeze exists only in `Translation.resx`. `ResourceManager` falls back to the
invariant file when the user's satellite lacks a key, so those strings render in English rather than
crashing — which matters here specifically, because `string.Format(null, …)` throws
`ArgumentNullException`, and that exact path once left the application stuck on its splash screen.

Known English-only additions so far: the update channel strings (`updater_*`,
`settings_check_for_updates`, added with the 1.0.0 updater).

The `LocalizationCoverageTests` list of loader format keys includes those new keys on purpose: it is
the proof that the invariant fallback actually works for every offered culture, not an assumption about
it.

## Contributing a translation

1. Edit `SimpleDnsCrypt/Resources/Translation.<yourlang>.resx`. Keys, not order, are what matters.
2. Keep every `{0}` `{1}` … placeholder, and keep the count the same — the code passes a fixed number of
   arguments.
3. A literal `\n` in a value (two characters: backslash, `n`) is how this codebase encodes a line break;
   several call sites `.Replace("\\n", "\n")` before display. Copy that convention from a neighbouring
   entry rather than inventing one.
4. If your language is missing entirely, add the file **and** an entry in
   `SimpleDnsCrypt/Helper/LocalizationEx.cs`. Both directions are tested, so an orphan fails CI:
   `EveryOfferedLanguageHasATranslationFile` and `EveryTranslationFileIsOfferedInTheLanguageDropdown`.

## Two traps worth knowing before you start

**BCP-47 normalisation is not optional.** `Translation.tgl.resx` sat in this repository for years with
195 translated strings that no user could ever see: `CultureInfo("tgl").Name` normalises to `tl`, so
the satellite produced from that file was never the one the runtime looked for. It is
`Translation.tl.resx` now, and Tagalog appears in the menu. A language file named with a three-letter
code that has a two-letter form is dead weight, silently.

**The assembly name is part of the translation system.** `WPFLocalizeExtension` looks resources up by
*assembly simple name*, not root namespace, so renaming the executable also renames every resource this
UI can find. There are 13 references that have to agree with `<AssemblyName>` — `LocalizationEx.cs` and
the 12 XAML files carrying `DefaultAssembly="SimpleDnsCryptPlus"`. Changing `<AssemblyName>` without
them produced a green build, a green test run, and an application that never got past its splash
dialog. A guard test exists for that (`Tests/AssemblyNameConsistencyTests.cs`); do not delete it to
make a rename easier.

## What is deliberately not planned

No new POEditor project, no Weblate/Crowdin instance: they add a second source of truth that has to be
kept in sync with the files the build actually reads, and no one is staffing that. Direct edits to the
`.resx` files, reviewed like any other change, is the workflow.
