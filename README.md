# ARM

[Italiano](README.it.md)

ARM is an all-in-one everyday app: **notes, tasks, calendar, payments, Amazon orders and music**, kept in sync between a Windows PC and an Android phone over your local Wi-Fi, with no cloud. Built with .NET MAUI. The interface is available in **English** and **Italian**.

## Features

- **Today**: a dashboard with deadlines, events, payments and orders. Type a plain sentence at the top ("dentist tomorrow at 3:30pm", "car tax 85€ friday") and ARM works out whether it is a task, an event, a payment or a reminder, with date, time and amount. *(The quick-add parser understands Italian phrases only.)*
- **Notes, Tasks, Calendar** with reminders and notifications (Android and Windows) and a "Today" widget on Android.
- **Amazon orders** (Windows only): reads Amazon emails from Gmail (IMAP, read-only) and shows status and expected delivery. *(The email parser understands Italian Amazon emails only.)*
- **Music**: playlists, search, downloaded songs, synced lyrics (LRCLIB), import of public Spotify playlists.
- **Wi-Fi sync** PC ↔ phone, paired with a code over an encrypted channel; no external server.
- **Themes** Light ("Chiaro") and Classic ("Classico", dark); on a wide Windows window the tabs become a sidebar.
- **Languages**: English and Italian, switchable from Settings.
- **Optional local API** (Windows only, `127.0.0.1` only, key-protected) to connect an external assistant.

## Requirements

- [.NET SDK 10](https://dotnet.microsoft.com/download) with the MAUI workloads:
  ```bash
  dotnet workload install maui-android maui-windows
  ```
- **Windows 10/11** to build and run the Windows version (the Windows target only builds on Windows). Visual Studio with the ".NET Multi-platform App UI development" workload is the easiest way; the command line is enough.
- For Android: Android SDK (API 26 or newer) and a phone or an emulator.

## Build and run

```bash
git clone <repo-url>
cd ARM
```

Windows (runs the executable directly, no package install needed):

```bash
dotnet run --project ARMusic/ARMusic.csproj -f net10.0-windows10.0.19041.0
```

Android (phone connected or emulator running):

```bash
dotnet build ARMusic/ARMusic.csproj -f net10.0-android -t:Run
```

Or open `ARMusic.slnx` in Visual Studio, pick "Windows Machine" or an Android device and press Start.

## Settings and configuration

**There are no keys in the code and no `.env` file is needed.** Credentials are entered inside the app and stay on your device (in the app preferences).

Open **Settings** with the sliders icon at the top of *Today*. There you can choose the **language**, the **theme**, an optional **name** (used for the greeting; leave it empty for a neutral one) and open the connection screens:

| Feature | What you need | Where to enter it |
|---|---|---|
| Spotify import | a *Client ID* and *Client Secret*: create a free app at [developer.spotify.com/dashboard](https://developer.spotify.com/dashboard) (Redirect URI `https://localhost`, tick "Web API") | **Settings → Spotify credentials** (or Music → Library → Spotify) |
| Amazon orders from Gmail | your Gmail address and an **app password** (not your Google password): [myaccount.google.com/apppasswords](https://myaccount.google.com/apppasswords), requires 2-step verification | **Settings → Orders from Gmail** (Windows only). The password is encrypted with DPAPI, tied to your Windows user |
| PC ↔ phone sync | PC and phone on the same Wi-Fi network | **Settings → Sync**: the PC shows a code, you type it on the phone. Ports: TCP 47800, UDP 47801 |

Optional environment variables (Windows only, for the local API): `ARM_AGENT_PORT` (port, default 47810) and `ARM_AGENT_FILE` (path of the JSON file holding port and key, default `%LOCALAPPDATA%\ARM\arm-api.json`). The API listens on `127.0.0.1` only and every request must carry the `X-Arm-Key` header.

## Themes

ARM ships with **Chiaro** (light) and **Classico** (dark). A theme is a XAML file containing only colors: to create a new one follow [docs/THEMES.md](docs/THEMES.md) (required keys, registration in `ThemeService`, minimal working example).

## Languages

Texts in the code are written in Italian and translated through a JSON dictionary per language (`ARMusic/Localization/en.json`). To add a language see [docs/LOCALIZATION.md](docs/LOCALIZATION.md). The first launch uses the device language if supported, otherwise English.

## Project layout

```
ARMusic/                  MAUI project (Android + Windows)
  Data/                   SQLite database
  Localization/           translation engine and language files
  Models/                 notes, tasks, events, orders, playlists…
  Services/               audio, Spotify, YouTube, lyrics, reminders, sync, Gmail, local API
  Themes/                 themes (Chiaro, Classico)
  Views/                  screens (a few in XAML, most built in code with Ui.cs)
  Platforms/              Android- and Windows-specific code
tools/TestParserAmazon/   test for the Amazon email parser
docs/                     theme and localization guides
```

## Tests

There is a command-line test for the Amazon email parser:

```bash
dotnet run --project tools/TestParserAmazon
```

It ends with `TUTTO OK` when every check passes.

## Notes and known limits

- **YouTube music**: the app can search and download audio from YouTube (YoutubeExplode library) and the Spotify import looks tracks up on YouTube. Use it only for content you have the right to listen to and respect the YouTube and Spotify terms of service. The authors do not distribute protected content and are not responsible for how you use it. There is no streaming: songs must be downloaded before they can be played.
- **Privacy**: data stays on your device. The app only contacts services you turn on: YouTube, Spotify, LRCLIB (receives artist, title and duration to find lyrics) and Gmail (IMAP, read-only).
- Windows: the app starts as a plain executable (`WindowsPackageType=None`), not as an MSIX package.
- Some compiler warnings about recent Android APIs (`CA1416`, `CA1422`) are known and harmless with minimum API level 26.
- iOS and macOS are not supported.
- Quick-add and the Amazon email parser understand Italian only.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).

## License

[MIT](LICENSE). Third-party fonts and libraries: see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
