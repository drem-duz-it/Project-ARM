# Contributing to ARM

Thanks for your interest! Contributions, bug reports and ideas are welcome.

## Before you start
1. Follow the [README](README.md) to install the requirements and build.
2. For a new theme see [docs/THEMES.md](docs/THEMES.md); for a new language see [docs/LOCALIZATION.md](docs/LOCALIZATION.md).

## Practical rules
- The source texts in the code are in **Italian** and wrapped in `Loc.T(...)`/`Loc.F(...)`; every new user-visible text needs an entry in `ARMusic/Localization/en.json`.
- Screens are mostly built in code with the helpers in `ARMusic/Ui.cs`: colors always come from theme tokens (`Ui.C("TextColor")`), never hard-coded values.
- **No secrets in the code or in commits**: keys and passwords are entered from the app. Do not commit `.pfx`, `.keystore` or `.env` files.
- No personal data (names, e-mail addresses, local paths) in the code, tests or sample data.
- Small, focused changes: one pull request, one thing.

## Before opening a pull request
```bash
dotnet build ARMusic/ARMusic.csproj -f net10.0-windows10.0.19041.0
dotnet build ARMusic/ARMusic.csproj -f net10.0-android
dotnet run --project tools/TestParserAmazon
```
The code must build without new warnings and the test must end with `TUTTO OK`. Describe in the PR what changes and how you tried it.
