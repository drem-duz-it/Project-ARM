using ARMusic.Services;
using ARMusic.Services.Sync;
using ARMusic.Views.Dashboard;

namespace ARMusic.Views;

// Impostazioni: lingua, tema, nome e scorciatoie verso i collegamenti (sincronizzazione, Gmail, Spotify).
// Le chiavi e le password non stanno nel codice: si inseriscono da qui (o dalle pagine collegate).
public class SettingsPage : SheetPage
{
    private readonly SyncService _sync;

    public SettingsPage(SyncService sync) : base(Loc.T("Impostazioni"))
    {
        _sync = sync;
        Render();
    }

    private void Render()
    {
        BackgroundColor = Ui.C("BgColor");
        Body.Children.Clear();

        // ---- Lingua ----
        Body.Add(Section(Loc.T("Lingua"),
            new Ui.ChipGroup(Loc.Languages.Select(l => (l.Code, l.Name, "AccentColor", (string)null)), Loc.Language, code =>
            {
                Loc.SetLanguage(code);
                Rebuild();
            }, columns: -1)));

        // ---- Tema ----
        Body.Add(Section(Loc.T("Tema"),
            new Ui.ChipGroup(ThemeService.Temi.Select(t => (t, Loc.T(t), "AccentColor", (string)null)), ThemeService.TemaAttuale, tema =>
            {
                ThemeService.Imposta(tema);
                Rebuild();
            }, columns: -1)));

        // ---- Nome (per il saluto in "Oggi") ----
        var name = Ui.Entry(Loc.T("Il tuo nome (facoltativo)"), Preferences.Default.Get("Utente.Nome", ""), 16);
        name.TextChanged += (s, e) => Preferences.Default.Set("Utente.Nome", (e.NewTextValue ?? "").Trim());
        Body.Add(Section(Loc.T("Nome"), Ui.Field(name)));

        // ---- Collegamenti ----
        var links = new VerticalStackLayout { Spacing = 8 };
        links.Add(Ui.Button(Loc.T("Sincronizzazione"), () => Navigation.PushModalAsync(new SyncPage(_sync)), Ui.Icons.Sync, ghost: true));
        if (GmailOrdiniService.Supportato)
            links.Add(Ui.Button(Loc.T("Ordini da Gmail"), () => Navigation.PushModalAsync(new GmailPage(null)), Ui.Icons.Mail, ghost: true));
        links.Add(Ui.Button(Loc.T("Credenziali Spotify"), () =>
        {
            var spotify = IPlatformApplication.Current.Services.GetService<SpotifyService>();
            Navigation.PushModalAsync(new SpotifyImportPage(spotify, () => { }));
        }, Ui.Icons.Music, ghost: true));
        Body.Add(Section(Loc.T("Collegamenti"), links));

        Body.Add(Ui.Small(Loc.F("ARM versione {0}", AppInfo.Current.VersionString)));
    }

    // Dopo un cambio di lingua o di tema la pagina si ridisegna (titolo compreso)
    private void Rebuild()
    {
        if (Root.Children.Count > 0 && Root.Children[0] is View oldHeader) Root.Remove(oldHeader);
        Root.Add(EditorPageBase.SheetHeader(Loc.T("Impostazioni"), OnClose), 0, 0);
        Render();
    }

    private static View Section(string title, View content)
    {
        var v = new VerticalStackLayout { Spacing = 8 };
        v.Add(Ui.Caps(title));
        v.Add(content);
        return v;
    }
}
