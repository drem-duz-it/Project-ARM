# Localization

ARM is available in Italian and English. The user picks the language in **Settings → Language**; the first launch uses the device language if supported, otherwise English.

## How it works

Texts in the code are written in **Italian**, and the Italian text is also the lookup key (gettext style):

```csharp
Loc.T("Salva")                    // "Save" when the language is English
Loc.F("{0} task in ritardo", n)   // formatted, with the right culture for dates and numbers
```

- `ARMusic/Localization/en.json` maps each Italian text to its English translation. It is embedded in the app.
- A key that is missing from the file never causes an error: the Italian text is shown. Texts that are identical in both languages (for example "ARM", "OK") are intentionally absent.
- XAML pages use the static texts as written and are translated at runtime; texts inside `DataTemplate`s use the `{loc:Tr Text}` markup extension.
- Changing the language rebuilds the interface immediately, no restart needed.

## Adding a language (for example French)

1. Copy `ARMusic/Localization/en.json` to `fr.json` and translate the **values** (right side). Do not change the keys. Keep placeholders such as `{0}` and any line breaks.
2. Add a line to `Loc.Languages` in `ARMusic/Localization/Loc.cs`:
   ```csharp
   ("fr", "Français", "fr-FR"),
   ```
   (code, name shown in its own language, culture used for dates and numbers).
3. Nothing else: `ARMusic.csproj` embeds every `Localization/*.json` automatically, and the new language appears in Settings.
4. Build, open Settings, pick the language and go through every screen looking for untranslated Italian texts.

## Adding a new text in the code

Wrap it: `Loc.T("Testo nuovo")` (or `Loc.F` with placeholders), then add `"Testo nuovo": "New text"` to `en.json` (and to any other language file). Never concatenate translated fragments: use one string with placeholders.

## Known limits

- Quick-add on *Today* and the Amazon email parser understand Italian phrases only; they are not translated.
- Texts coming from outside the app (for example YouTube titles, Gmail subjects) are shown as received.
