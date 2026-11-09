# Creating a theme

[Italiano](THEMES.it.md)

ARM themes are XAML files that contain only **colors and a few numbers**. The app loads one at a time (`ThemeService.Applica`) and every screen reads the values by name. Two themes exist today:

| Name in the app | Class | File | Character |
|---|---|---|---|
| Chiaro (default) | `LightTheme` | `ARM/Themes/LightTheme.xaml` | white and greys |
| Classico | `ClassicTheme` | `ARM/Themes/ClassicTheme.xaml` | neutral dark |

The theme is chosen in **Settings → Theme** (the entries come from the `ThemeService.Temi` list); the choice is saved in the preferences (`AppTheme`).

## Keys to define

Every theme must define **all** of these keys (same names, same types): a missing key shows a fallback grey or a runtime error. The safest way is to copy `LightTheme.xaml` or `ClassicTheme.xaml` and change the values.

| Group | Keys | Meaning |
|---|---|---|
| Base | `BgColor`, `TabBgColor`, `TextColor`, `SubTextColor`, `BorderColor`, `ShadowColor` | page background, card and tab bar background, text, secondary text, borders, shadows |
| Accents (fixed meaning) | `AccentColor`, `Accent2Color`, `Accent3Color`, `Accent4Color`, `Accent5Color` | main action and notes · expenses/urgent · income/done · reminders · events |
| Action | `SoftAccentColor`, `OnAccentColor` | soft accent background · text color on top of the accent |
| Components | `FieldBgColor`, `CardBorderColor`, `SwitchOffColor`, `FabShadowColor` | text fields, card border, switch off state, shadow of the "+" button |
| Header | `Hero1Color`, `Hero2Color`, `OnHeroColor` | the two gradient colors of the header and the text on top |
| Mini player | `MiniBgColor`, `MiniTextColor`, `MiniBorderColor` | background, text and border of the mini player |
| Surfaces | `SurfaceRaisedColor`, `BorderStrongColor`, `WarnColor` | raised surface, strong border, warning/overdue |
| Categories | `Cat1` … `Cat5` | the five colored category dots |
| Notes | `Pastel1` … `Pastel6` | note backgrounds |
| Numbers (`x:Double`) | `SoftAlpha`, `ShadowOpacity`, `FabShadowOpacity` | transparency of soft backgrounds, shadow opacity |

The meaning of the accent colors **must stay the same** in every theme, because the UI uses them to convey information (for example red/orange = money going out).

## Steps

1. **Create the file** `ARM/Themes/MyTheme.xaml` with its code-behind `MyTheme.xaml.cs` (see the example below: a few lines).
2. **Register it** in `ARM/Services/ThemeService.cs`:
   - add the name to the list: `Temi = { "Chiaro", "Classico", "MyTheme" }`;
   - add the branch in the `switch` of `Applica`: `"MyTheme" => new Themes.MyTheme(),`.
3. **Android status bar**: `ThemeService.Applica` uses dark icons only for the `"Chiaro"` theme (`AppearanceLightStatusBars = tema == "Chiaro"`). If your theme has a light background, update that condition.
4. **Build and try it**: open Settings and pick your theme from the list.

The theme name is shown through `Loc.T`; to give it a translated label, add it to `ARM/Localization/en.json` (see [LOCALIZATION.md](LOCALIZATION.md)). No other changes are needed: themes are included automatically by the project.

## Minimal working example: the "Sabbia" theme

`ARM/Themes/SabbiaTheme.xaml`

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ResourceDictionary xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
                    x:Class="ARM.Themes.SabbiaTheme">

    <Color x:Key="BgColor">#F7F1E5</Color>
    <Color x:Key="TabBgColor">#FFFBF2</Color>
    <Color x:Key="TextColor">#2B2418</Color>
    <Color x:Key="SubTextColor">#7A6E58</Color>
    <Color x:Key="AccentColor">#8A5A1F</Color>
    <Color x:Key="Accent2Color">#B42318</Color>
    <Color x:Key="Accent3Color">#2E7D4F</Color>
    <Color x:Key="Accent4Color">#B7791F</Color>
    <Color x:Key="Accent5Color">#2F6FE0</Color>
    <Color x:Key="SoftAccentColor">#EFE4CE</Color>
    <Color x:Key="BorderColor">#E6DBC4</Color>
    <Color x:Key="OnAccentColor">#FFFFFF</Color>
    <Color x:Key="ShadowColor">#2B2418</Color>

    <Color x:Key="FieldBgColor">#EFE4CE</Color>
    <Color x:Key="CardBorderColor">#E6DBC4</Color>
    <Color x:Key="Hero1Color">#EFE4CE</Color>
    <Color x:Key="Hero2Color">#FFFBF2</Color>
    <Color x:Key="OnHeroColor">#2B2418</Color>
    <Color x:Key="MiniBgColor">#FFFBF2</Color>
    <Color x:Key="MiniTextColor">#2B2418</Color>
    <Color x:Key="MiniBorderColor">#E6DBC4</Color>
    <Color x:Key="SwitchOffColor">#CFC3AA</Color>
    <Color x:Key="FabShadowColor">#2B2418</Color>

    <Color x:Key="SurfaceRaisedColor">#EFE4CE</Color>
    <Color x:Key="BorderStrongColor">#CFC3AA</Color>
    <Color x:Key="WarnColor">#B42318</Color>
    <Color x:Key="Cat1">#2F6FE0</Color>
    <Color x:Key="Cat2">#B7791F</Color>
    <Color x:Key="Cat3">#7A5AD1</Color>
    <Color x:Key="Cat4">#178F82</Color>
    <Color x:Key="Cat5">#C24E7A</Color>

    <Color x:Key="Pastel1">#FFFBF2</Color>
    <Color x:Key="Pastel2">#FFFBF2</Color>
    <Color x:Key="Pastel3">#FFFBF2</Color>
    <Color x:Key="Pastel4">#FFFBF2</Color>
    <Color x:Key="Pastel5">#FFFBF2</Color>
    <Color x:Key="Pastel6">#FFFBF2</Color>

    <x:Double x:Key="SoftAlpha">0.12</x:Double>
    <x:Double x:Key="ShadowOpacity">0.10</x:Double>
    <x:Double x:Key="FabShadowOpacity">0.18</x:Double>
</ResourceDictionary>
```

`ARM/Themes/SabbiaTheme.xaml.cs`

```csharp
namespace ARM.Themes;

public partial class SabbiaTheme : ResourceDictionary
{
    public SabbiaTheme()
    {
        InitializeComponent();
    }
}
```

In `ThemeService.cs`: `Temi = { "Chiaro", "Classico", "Sabbia" }` and, in the `switch`, `"Sabbia" => new Themes.SabbiaTheme(),`. Since "Sabbia" is a light theme, also change `AppearanceLightStatusBars = tema == "Chiaro" || tema == "Sabbia";` in the same file.

## Tips

- Check the contrast between `TextColor`/`SubTextColor` and `BgColor`/`TabBgColor` (at least 4.5:1 for normal text).
- Check the theme on a phone and on a wide Windows window (≥ 720 px: the sidebar appears).
- To make your theme the default, change the fallback value `"Chiaro"` in `ThemeService`.
