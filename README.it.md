# ARM

[English](README.md)

ARM è un'app di tutti i giorni in uno: **note, task, calendario, pagamenti, ordini Amazon e musica**, sincronizzati via Wi-Fi tra un PC Windows e un telefono Android, senza cloud. Realizzata con .NET MAUI. L'interfaccia è disponibile in **italiano** e **inglese**.

## Cosa fa

- **Oggi**: la dashboard con scadenze, eventi, pagamenti e ordini. In cima si scrive al volo una frase normale («dentista domani alle 15:30», «bollo auto 85€ venerdì») e ARM capisce se è un task, un evento, un pagamento o un promemoria, con data, ora e importo. *(Il riconoscimento delle frasi funziona solo in italiano.)*
- **Note, Task, Calendario** con promemoria e notifiche (Android e Windows) e widget «Oggi» su Android.
- **Ordini Amazon** (solo Windows): legge da Gmail (IMAP, sola lettura) le email di Amazon e mostra stato e consegna prevista. *(Il parser capisce solo le email Amazon in italiano.)*
- **Musica**: playlist, ricerca, brani scaricati, testi sincronizzati (LRCLIB), import di playlist pubbliche da Spotify.
- **Sincronizzazione Wi-Fi** PC ↔ telefono con abbinamento tramite codice e canale cifrato; nessun server esterno.
- **Temi** Chiaro e Classico (scuro); su Windows, a finestra larga, i tab diventano una barra laterale.
- **Lingue**: italiano e inglese, selezionabili dalle Impostazioni.
- **API locale opzionale** (solo Windows, solo `127.0.0.1`, protetta da chiave) per collegare un assistente esterno.

## Requisiti

- [.NET SDK 10](https://dotnet.microsoft.com/download) con i workload MAUI:
  ```bash
  dotnet workload install maui-android maui-windows
  ```
- **Windows 10/11** per compilare e usare la versione Windows (il target Windows si compila solo su Windows). Visual Studio con il carico di lavoro «Sviluppo di app mobili con .NET» è il modo più semplice; la riga di comando basta.
- Per Android: Android SDK (API 26 o superiore) e un telefono o un emulatore.

## Installazione e avvio

```bash
git clone <url-della-repo>
cd ARM
```

Windows (avvia direttamente l'eseguibile, non serve installare un pacchetto):

```bash
dotnet run --project ARM/ARM.csproj -f net10.0-windows10.0.19041.0
```

Android (telefono collegato o emulatore avviato):

```bash
dotnet build ARM/ARM.csproj -f net10.0-android -t:Run
```

In alternativa apri `ARM.slnx` in Visual Studio, scegli «Windows Machine» o un dispositivo Android e premi Avvia.

## Impostazioni e configurazione

**Non c'è nessuna chiave nel codice e non serve nessun file `.env`.** Le credenziali si inseriscono dall'app e restano solo sul tuo dispositivo (nelle preferenze dell'app).

Apri le **Impostazioni** con l'icona a cursori in cima a *Oggi*. Qui scegli **lingua** e **tema**, un **nome** facoltativo (usato nel saluto; se vuoto il saluto è neutro) e apri le schermate di collegamento:

| Funzione | Cosa serve | Dove si inserisce |
|---|---|---|
| Import da Spotify | un *Client ID* e un *Client Secret*: crea gratis un'app su [developer.spotify.com/dashboard](https://developer.spotify.com/dashboard) (Redirect URI `https://localhost`, spunta «Web API») | **Impostazioni → Credenziali Spotify** (oppure Musica → Libreria → Spotify) |
| Ordini Amazon da Gmail | il tuo indirizzo Gmail e una **password per le app** (non la password di Google): [myaccount.google.com/apppasswords](https://myaccount.google.com/apppasswords), richiede la verifica in due passaggi | **Impostazioni → Ordini da Gmail** (solo Windows). La password è cifrata con DPAPI, legata al tuo utente Windows |
| Sincronizzazione PC ↔ telefono | PC e telefono sulla stessa rete Wi-Fi | **Impostazioni → Sincronizzazione**: sul PC compare un codice, sul telefono lo inserisci. Porte: TCP 47800, UDP 47801 |

Variabili d'ambiente opzionali (solo Windows, per l'API locale): `ARM_AGENT_PORT` (porta, predefinita 47810) e `ARM_AGENT_FILE` (percorso del file JSON con porta e chiave, predefinito `%LOCALAPPDATA%\ARM\arm-api.json`). L'API ascolta solo su `127.0.0.1` e ogni richiesta deve portare l'header `X-Arm-Key`.

## Temi

L'app include **Chiaro** e **Classico** (scuro). Un tema è un file XAML di soli colori: per crearne uno nuovo segui la guida [docs/THEMES.it.md](docs/THEMES.it.md) ([English](docs/THEMES.md)).

## Lingue

I testi nel codice sono scritti in italiano e tradotti con un dizionario JSON per lingua (`ARM/Localization/en.json`). Per aggiungere una lingua vedi [docs/LOCALIZATION.md](docs/LOCALIZATION.md) (in inglese). Al primo avvio si usa la lingua del dispositivo se supportata, altrimenti l'inglese.

## Struttura

```
ARM/                  progetto MAUI (Android + Windows)
  Data/                   database SQLite
  Localization/           motore di traduzione e file delle lingue
  Models/                 note, task, eventi, ordini, playlist…
  Services/               audio, Spotify, YouTube, testi, promemoria, sincronizzazione, Gmail, API locale
  Themes/                 temi (Chiaro, Classico)
  Views/                  schermate (alcune in XAML, la maggior parte costruite da codice con Ui.cs)
  Platforms/              codice specifico Android e Windows
tools/TestParserAmazon/   test del parser delle email di Amazon
docs/                     guide ai temi e alle lingue
```

## Test

```bash
dotnet run --project tools/TestParserAmazon
```

Termina con `TUTTO OK` se tutti i controlli passano.

## Avvertenze e limiti noti

- **Musica da YouTube**: l'app può cercare e scaricare audio da YouTube (libreria YoutubeExplode) e l'import da Spotify cerca i brani su YouTube. Usala solo per contenuti che hai il diritto di ascoltare e rispetta i termini di servizio di YouTube e Spotify. Gli autori non distribuiscono contenuti protetti e non rispondono dell'uso che ne fai. Non c'è lo streaming: i brani vanno scaricati per poterli ascoltare.
- **Privacy**: i dati restano sul tuo dispositivo. L'app contatta solo i servizi che attivi tu: YouTube, Spotify, LRCLIB (riceve artista, titolo e durata del brano per cercare il testo) e Gmail (IMAP, sola lettura).
- Windows: l'app parte come eseguibile semplice (`WindowsPackageType=None`), non come pacchetto MSIX.
- Alcuni avvisi del compilatore su API Android recenti (`CA1416`, `CA1422`) sono noti e innocui con il livello minimo API 26.
- iOS e macOS non sono supportati.
- Inserimento rapido e parser delle email Amazon capiscono solo l'italiano.

## Contribuire

Vedi [CONTRIBUTING.md](CONTRIBUTING.md).

## Licenza

[MIT](LICENSE). Font e librerie di terze parti: vedi [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
