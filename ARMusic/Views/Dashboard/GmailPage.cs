using ARMusic.Data;
using ARMusic.Models;
using ARMusic.Services;
using Microsoft.Maui.Controls.Shapes;

namespace ARMusic.Views.Dashboard;

// "Collega Gmail" (3.0): indirizzo + password per le app, Verifica, Scollega.
// La password per le app si crea su myaccount.google.com (Sicurezza → Verifica in due passaggi → Password per le app).
public class GmailPage : SheetPage
{
    private readonly GmailOrdiniService _gmail;
    private readonly Action _onChanged;
    private Entry _mail, _pwd;
    private readonly Label _status = Ui.Small("");
    private bool _busy;

    public GmailPage(Action onChanged) : base(Loc.T("Ordini da Gmail"))
    {
        _gmail = GmailOrdiniService.Current;
        _onChanged = onChanged;
        Render();
    }

    private void Render()
    {
        Body.Children.Clear();
        _status.Text = "";

        if (!GmailOrdiniService.Supportato)
        {
            Body.Add(Ui.Body(Loc.T("Gli ordini Amazon li legge ARM sul PC: collega Gmail da lì. Su questo dispositivo arrivano con la sincronizzazione Wi-Fi."), "SubTextColor"));
            return;
        }

        if (_gmail.Collegato)
        {
            var card = new VerticalStackLayout { Spacing = 4 };
            var row = new HorizontalStackLayout { Spacing = 8 };
            row.Add(Ui.Dot(Ui.C(string.IsNullOrEmpty(_gmail.UltimoErrore) ? "AccentColor" : "WarnColor")));
            row.Add(new Label { Text = string.IsNullOrEmpty(_gmail.UltimoErrore) ? Loc.T("Collegato") : Loc.T("Errore di lettura"), FontSize = 15, FontFamily = Ui.FontSemi, TextColor = Ui.C("TextColor") });
            card.Add(row);
            card.Add(Ui.Meta(_gmail.Indirizzo));
            card.Add(Ui.Meta(_gmail.UltimoRiuscito is DateTime t ? Loc.F("Ultima lettura riuscita: {0}", Quando(t)) : Loc.T("Nessuna lettura completata")));
            if (!string.IsNullOrEmpty(_gmail.UltimoErrore))
            {
                var err = Ui.Small(_gmail.UltimoErrore, "WarnColor");
                err.Margin = new Thickness(0, 6, 0, 0);
                card.Add(err);
            }
            Body.Add(Ui.Card(card, new Thickness(20, 16)));

            var buttons = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)), ColumnSpacing = 8 };
            buttons.Add(Ui.Button(_gmail.InLettura ? Loc.T("Lettura in corso…") : Loc.T("Aggiorna ora"), async () => { await _gmail.AggiornaAsync(); _onChanged?.Invoke(); Render(); }, Ui.Icons.Refresh));
            buttons.Add(Ui.Button(Loc.T("Scollega"), async () =>
            {
                if (!await DisplayAlert(Loc.T("Scollegare Gmail?"), Loc.T("ARM smette di leggere le email di Amazon. Gli ordini già letti restano."), Loc.T("Scollega"), Loc.T("Annulla"))) return;
                _gmail.Scollega();
                _onChanged?.Invoke();
                Render();
            }, ghost: true), 1);
            Body.Add(buttons);
            Body.Add(Privacy());
            return;
        }

        Body.Add(Ui.Body(Loc.T("ARM legge solo le email di conferma, spedizione e consegna di Amazon, per mostrarti gli ordini in arrivo. Nessun altro messaggio viene aperto, niente viene segnato come letto o spostato."), "SubTextColor"));

        _mail = Ui.Entry(Loc.T("nome@gmail.com"), Preferences.Default.Get("Gmail.UltimoIndirizzo", ""), 16, Keyboard.Email);
        _pwd = Ui.Entry(Loc.T("16 lettere, es. abcd efgh ijkl mnop"), null, 16);
        _pwd.IsPassword = true;
        Body.Add(Campo(Loc.T("Indirizzo Gmail"), _mail));
        Body.Add(Campo(Loc.T("Password per le app"), _pwd));

        var how = new VerticalStackLayout { Spacing = 6 };
        how.Add(Ui.Caps(Loc.T("Come si crea la password per le app")));
        foreach (var (n, t) in new[]
        {
            ("1", Loc.T("Apri myaccount.google.com e vai su Sicurezza.")),
            ("2", Loc.T("Attiva la Verifica in due passaggi, se non è già attiva.")),
            ("3", Loc.T("Cerca «Password per le app», scrivi «ARM» e premi Crea.")),
            ("4", Loc.T("Copia qui le 16 lettere. Potrai revocarla quando vuoi."))
        })
        {
            var g = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(20), new ColumnDefinition(GridLength.Star)), ColumnSpacing = 8 };
            g.Add(new Label { Text = n, FontSize = 13, FontFamily = Ui.FontBold, TextColor = Ui.C("SubTextColor") });
            g.Add(Ui.Small(t, "TextColor"), 1);
            how.Add(g);
        }
        how.Add(Ui.LinkLabel(Loc.T("Apri la pagina di Google"), async () => await Launcher.OpenAsync("https://myaccount.google.com/apppasswords")));
        Body.Add(Ui.Card(how, new Thickness(20, 16)));
        Body.Add(Privacy());

        _status.HorizontalTextAlignment = TextAlignment.Center;
        Body.Add(_status);
        SetFooter(Ui.Button(Loc.T("Verifica e collega"), Verifica, Ui.Icons.Mail));
    }

    private async void Verifica()
    {
        if (_busy) return;
        _busy = true;
        _status.TextColor = Ui.C("SubTextColor");
        _status.Text = Loc.T("Verifico l'accesso a Gmail…");
        var err = await _gmail.CollegaAsync(_mail.Text, _pwd.Text);
        _busy = false;
        if (err != null) { _status.TextColor = Ui.C("WarnColor"); _status.Text = err; return; }
        Preferences.Default.Set("Gmail.UltimoIndirizzo", _mail.Text.Trim());
        _onChanged?.Invoke();
        await Navigation.PopModalAsync();
    }

    private static View Campo(string label, Entry e)
    {
        var v = new VerticalStackLayout { Spacing = 6 };
        v.Add(new Label { Text = label, FontSize = 13, FontFamily = Ui.FontSemi, TextColor = Ui.C("TextColor") });
        v.Add(Ui.Field(e));
        return v;
    }

    private static View Privacy()
    {
        var g = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)), ColumnSpacing = 10 };
        g.Add(Ui.Icon(Ui.Icons.Info, Ui.C("SubTextColor"), 18));
        var t = Ui.Small(Loc.T("La password resta solo su questo PC, cifrata con il tuo utente Windows. ARM non vede mai la password di Amazon né quella di Google."));
        g.Add(t, 1);
        return g;
    }

    public static string Quando(DateTime t)
    {
        var diff = DateTime.Now - t;
        if (diff.TotalMinutes < 1) return Loc.T("adesso");
        if (diff.TotalMinutes < 60) return Loc.F("{0} min fa", (int)diff.TotalMinutes);
        if (t.Date == DateTime.Today) return Loc.F("oggi alle {0}", t.ToString("HH:mm"));
        if (t.Date == DateTime.Today.AddDays(-1)) return Loc.F("ieri alle {0}", t.ToString("HH:mm"));
        return Loc.F("{0} alle {1}", t.ToString("d MMM", Ui.It), t.ToString("HH:mm"));
    }
}

// Azioni su un ordine (tocco sulla riga)
public static class OrdineAzioni
{
    public static async Task ApriAsync(DatabaseService db, Ordine o, Action refresh)
    {
        var page = Application.Current?.MainPage;
        if (page == null) return;
        var scelte = new List<string>();
        if (!o.Consegnato) scelte.Add(Loc.T("Segna come ricevuto"));
        if (!string.IsNullOrEmpty(o.LinkTracking)) scelte.Add(Loc.T("Traccia il pacco"));
        scelte.Add(Loc.T("Apri l'ordine su Amazon"));
        scelte.Add(Loc.T("Copia il numero d'ordine"));
        string titolo = o.Prodotto + (string.IsNullOrEmpty(o.NumeroOrdine) ? "" : "\n" + o.NumeroOrdine);
        var r = await page.DisplayActionSheet(titolo, Loc.T("Chiudi"), Loc.T("Rimuovi dall'elenco"), scelte.ToArray());
        switch (r)
        {
            case string _ when r == Loc.T("Segna come ricevuto"): await db.SegnaConsegnatoAsync(o); refresh(); break;
            case string _ when r == Loc.T("Traccia il pacco"): await Launcher.OpenAsync(o.LinkTracking); break;
            case string _ when r == Loc.T("Apri l'ordine su Amazon"):
                await Launcher.OpenAsync(string.IsNullOrEmpty(o.NumeroOrdine)
                    ? "https://www.amazon.it/gp/css/order-history"
                    : $"https://www.amazon.it/gp/your-account/order-details?orderID={o.NumeroOrdine}");
                break;
            case string _ when r == Loc.T("Copia il numero d'ordine"): if (!string.IsNullOrEmpty(o.NumeroOrdine)) await Clipboard.Default.SetTextAsync(o.NumeroOrdine); break;
            case string _ when r == Loc.T("Rimuovi dall'elenco"): await db.DeleteOrdineAsync(o); refresh(); break;
        }
    }
}
