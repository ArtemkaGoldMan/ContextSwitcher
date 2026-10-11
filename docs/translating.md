# Translating

Every text ContextSwitcher shows lives in one file,
[`src/ContextSwitcher.App/Resources/Strings.resx`](../src/ContextSwitcher.App/Resources/Strings.resx),
in English. A translation is a copy of it with the language in its name — `Strings.uk.resx`,
`Strings.pl.resx` — holding the same keys with translated values. Anything a translation leaves out
shows in English, so a language can be added a part at a time.

## Which language the app uses

**Settings → Appearance → Language**: *Match macOS* (the default), or one of the translations, each
named in its own language. *Match macOS* takes the first language in System Settings → General →
Language & Region that the app is translated into — so a Mac listing Polish, then Ukrainian, shows
Ukrainian — and English if none is. macOS's own per-app choice (Language & Region → Applications)
counts too: the app reads the list through `NSLocale`, which includes it.

Language is chosen once, at startup, before any window: Settings says so after a change, and the
app picks it up the next time it opens. Numbers and dates keep the Mac's region — Ukrainian on a Mac
set to Poland formats like `uk-PL` ("3,5 год").

macOS also gets the list: `scripts/build-app.sh` declares every translation in the bundle's
`Info.plist` and writes a `<code>.lproj/InfoPlist.strings` with the Automation prompt's text
(`InfoPlist_AppleEventsUsage`), so that prompt is translated too.

## Adding a language

1. Copy `Strings.resx` to `Strings.<code>.resx`, where `<code>` is the language's two-letter code
   (`de`, `pl`, `uk`…).
2. Translate each `<value>`. Leave the `name`s alone. The `<comment>` under each says where the text
   appears and what any `{0}` is — you can delete the comments in the copy.
3. Add the language to `AppLanguages.Supported` in
   [`Resources/AppLanguages.cs`](../src/ContextSwitcher.App/Resources/AppLanguages.cs), with its name
   in that language. That puts it in Settings; the build script picks the file up by itself.
4. Run `dotnet test`. A language in that list must be translated in full — every key, and every form
   its counted phrases take — and keep the English placeholders; the tests list anything missing.
5. Choose the language in Settings, reopen the app and look at each screen: translations are often
   longer than English, and the menu bar dashboard is narrow.

Rider and Visual Studio both edit `.resx` files as a table with a column per language, which is
easier than the XML.

## Not translatable yet

What a switch reports about its steps ("Couldn't quit Visual Studio Code…") and the configuration
check's messages still come from the switch engine as finished English sentences. Turning them into
translatable messages is the next step.

## Rules for translators

- **Keep the placeholders.** `{0}`, `{1}` are filled in by the app — a profile name, a version, a
  count. Move them wherever the sentence needs them, but don't drop or renumber them. Some carry a
  format, like `{0:HH:mm}` (a time); keep that part as it is.
- **Counted phrases have one key per form.** "1 day / 2 days" is `Duration_Days_One` and
  `Duration_Days_Other` in English. Languages with more forms add them: Ukrainian and Polish use
  `_One` (1, and in Ukrainian 21, 31…), `_Few` (2–4, 22–24…) and `_Many` (5–20, 25–30…, and 0). A
  form you leave out falls back to `_Other`.
- **Section titles are in capitals** (keys ending in `_Section`, such as `SWITCH TO`); keep them in
  capitals.
- **Don't translate**: the product name, app and browser names (Google Chrome, Spotify), and the
  names of the Focus Shortcuts — `ContextSwitcher - Focus Do Not Disturb` is the exact name the app
  runs, so it stays in English inside any translated sentence.
- **Use macOS's own words** for macOS features — Focus, Shortcuts, System Settings, Privacy &
  Security — as they appear on a Mac in that language. The Ukrainian translation uses
  «Зосередження», «Швидкі команди», «Системні параметри», «Приватність і безпека», «смуга меню».
- **The switch labels** (`Common_On`, `Common_Off`) sit beside every switch; keep them short.

## For developers

- No visible text goes straight into a view or a view model. Add it to `Strings.resx` and use it as
  `{x:Static r:Strings.Key}` in XAML (`xmlns:r="using:ContextSwitcher.App.Resources"`) or
  `Strings.Key` in C#. A test fails if a view has text written into it.
- Name keys `Area_What`: `Settings_Theme_Light`, `Dashboard_ActiveFor`. Add a `<comment>` saying
  where it shows and what each placeholder is — translators see only the file.
- Patterns with placeholders go through `Localized.Format(Strings.Key, value)`, so numbers and dates
  use the user's formats. Counted phrases go through `Plurals.Format("Base", count)`.
- The log, the command line's output, and identifiers stay in English: the log is what people send
  with bug reports, and scripts read the command line.
