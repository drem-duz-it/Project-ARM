# Creare un tema

[English](THEMES.md)

I temi di ARM sono file XAML che contengono soltanto **colori e qualche numero**. L'app ne carica uno alla volta (`ThemeService.Applica`) e tutte le schermate leggono i valori per nome. Oggi esistono due temi:

| Nome nell'app | Classe | File | Carattere |
|---|---|---|---|
| Chiaro (predefinito) | `LightTheme` | `ARMusic/Themes/LightTheme.xaml` | bianco e grigi |
| Classico | `SpotifyTheme` | `ARMusic/Themes/SpotifyTheme.xaml` | scuro neutro |

Il tema si sceglie in **Impostazioni → Tema** (le voci vengono dall'elenco `ThemeService.Temi`); la scelta è salvata nelle preferenze (`AppTheme`).

## Le chiavi da definire

Ogni tema deve definire **tutte** queste chiavi (stessi nomi, stessi tipi): una chiave mancante fa comparire un grigio di ripiego o un errore a runtime. Il modo più sicuro è copiare `LightTheme.xaml` o `SpotifyTheme.xaml` e cambiare i valori.

| Gruppo | Chiavi | Significato |
|---|---|---|
| Base | `BgColor`, `TabBgColor`, `TextColor`, `SubTextColor`, `BorderColor`, `ShadowColor` | sfondo pagina, sfondo di card e barra dei tab, testo, testo secondario, bordi, ombre |
| Accenti (significato fisso) | `AccentColor`, `Accent2Color`, `Accent3Color`, `Accent4Color`, `Accent5Color` | azione principale e note · uscite/urgente · entrate/completato · promemoria · eventi |
| Azione | `SoftAccentColor`, `OnAccentColor` | fondo tenue dell'accento · colore del testo sopra l'accento |
| Componenti | `FieldBgColor`, `CardBorderColor`, `SwitchOffColor`, `FabShadowColor` | campi di testo, bordo card, interruttore spento, ombra del pulsante «+» |
| Testata | `Hero1Color`, `Hero2Color`, `OnHeroColor` | i due colori del fondo a sfumatura e il testo sopra |
| Mini player | `MiniBgColor`, `MiniTextColor`, `MiniBorderColor` | fondo, testo e bordo del mini player |
| Superfici | `SurfaceRaisedColor`, `BorderStrongColor`, `WarnColor` | superficie rialzata, bordo marcato, avviso/ritardi |
| Categorie | `Cat1` … `Cat5` | i cinque punti colorati delle categorie |
| Note | `Pastel1` … `Pastel6` | fondi delle note |
| Numeri (`x:Double`) | `SoftAlpha`, `ShadowOpacity`, `FabShadowOpacity` | trasparenza dei fondi tenui, opacità delle ombre |

Il significato dei colori accento **deve restare lo stesso** in tutti i temi, perché l'interfaccia lo usa per dare informazioni (per esempio rosso/arancio = soldi in uscita).

## Passi

1. **Crea il file** `ARMusic/Themes/MioTema.xaml` con il suo code-behind `MioTema.xaml.cs` (vedi l'esempio sotto: sono poche righe).
2. **Registralo** in `ARMusic/Services/ThemeService.cs`:
   - aggiungi il nome all'elenco: `Temi = { "Chiaro", "Classico", "MioTema" }`;
   - aggiungi il ramo nello `switch` di `Applica`: `"MioTema" => new Themes.MioTema(),`.
3. **Barra di stato Android**: `ThemeService.Applica` mette le icone scure solo per il tema `"Chiaro"` (`AppearanceLightStatusBars = tema == "Chiaro"`). Se il tuo tema ha lo sfondo chiaro, aggiorna quella condizione.
4. **Compila e provalo**: apri Impostazioni e scegli il tuo tema dall'elenco.

Non servono altre modifiche: i temi sono inclusi automaticamente dal progetto.

## Esempio minimo funzionante: tema «Sabbia»

`ARMusic/Themes/SabbiaTheme.xaml`

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ResourceDictionary xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
                    x:Class="ARMusic.Themes.SabbiaTheme">

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

`ARMusic/Themes/SabbiaTheme.xaml.cs`

```csharp
namespace ARMusic.Themes;

public partial class SabbiaTheme : ResourceDictionary
{
    public SabbiaTheme()
    {
        InitializeComponent();
    }
}
```

In `ThemeService.cs`: `Temi = { "Chiaro", "Classico", "Sabbia" }` e, nello `switch`, `"Sabbia" => new Themes.SabbiaTheme(),`. Poiché «Sabbia» è un tema chiaro, nello stesso file cambia anche `AppearanceLightStatusBars = tema == "Chiaro" || tema == "Sabbia";`.

## Consigli

- Verifica il contrasto tra `TextColor`/`SubTextColor` e `BgColor`/`TabBgColor` (almeno 4,5:1 per il testo normale).
- Controlla il tema su telefono e su finestra larga di Windows (≥ 720 px: compare la barra laterale).
- Se vuoi che il tema venga proposto di default, cambia il valore di ripiego `"Chiaro"` in `ThemeService`.
