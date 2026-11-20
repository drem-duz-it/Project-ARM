using ARM.Services.Sync;
using Microsoft.Maui.Controls.Shapes;

namespace ARM.Views;

// Sincronizzazione cloud (Supabase): accesso, chiave di cifratura (primo dispositivo o codice), stato.
// Da qui si raggiunge anche la vecchia sincronizzazione Wi-Fi, finché il cloud non è attivo.
public class CloudSyncPage : SheetPage
{
    private readonly SyncService _sync;
    private CloudSyncService Cloud => _sync.Cloud;
    private readonly Label _status = Ui.Small("");
    private string _code;            // codice di abbinamento mostrato
    private bool? _serverHasData;    // null = da controllare
    private bool _busy;

    private Entry _url, _key, _email, _password, _codeEntry;

    public CloudSyncPage(SyncService sync) : base(Loc.T("Sincronizzazione"))
    {
        _sync = sync;
        _status.HorizontalTextAlignment = TextAlignment.Center;
        _status.FontFamily = Ui.FontRegular;
        Render();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _sync.StatusChanged += OnStatus;
        _sync.DataSynced += Render;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _sync.StatusChanged -= OnStatus;
        _sync.DataSynced -= Render;
    }

    private void OnStatus() => _status.Text = _sync.Status;

    private void Render()
    {
        Ui.Detach(_status);
        Body.Children.Clear();
        Body.Add(Info());

        if (Cloud.IsEnabled && Cloud.HasKey) RenderActive();
        else if (Cloud.IsSignedIn) RenderKey();
        else RenderSignIn();

        Body.Add(_status);

        // Sincronizzazione Wi-Fi: si usa ancora finché il cloud non è attivo su questo dispositivo
        if (!Cloud.IsEnabled)
            Body.Add(SecondaryButton(Loc.T("Sincronizzazione Wi-Fi (senza cloud)"), () => Navigation.PushModalAsync(new SyncPage(_sync)), "SubTextColor"));
        else if (_sync.IsWifiPaired)
            Body.Add(Ui.Small(Loc.T("La sincronizzazione Wi-Fi è ferma su questo dispositivo: ora passa tutto dal cloud.")));
    }

    private static View Info()
    {
        var g = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)), ColumnSpacing = 10 };
        g.Add(Ui.Icon(Ui.Icons.Sync, Ui.C("AccentColor"), 20));
        var t = Ui.Small(Loc.T("Ovunque ci sia Internet, in pochi secondi. I dati sono cifrati sul dispositivo: Supabase vede solo contenuti illeggibili. I brani non passano dal cloud: ogni dispositivo li scarica da YouTube (il telefono solo in Wi-Fi)."), "TextColor");
        t.FontFamily = Ui.FontRegular;
        t.VerticalOptions = LayoutOptions.Center;
        g.Add(t, 1);
        return new Border { Padding = new Thickness(14, 12), StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 16 }, BackgroundColor = Ui.C("FieldBgColor"), Content = g };
    }

    private static Entry Reuse(ref Entry entry, Func<Entry> create)
    {
        entry ??= create();
        Ui.Detach(entry);
        return entry;
    }

    // ---------- 1. Accesso ----------
    private void RenderSignIn()
    {
        Body.Add(Ui.Caps(Loc.T("1 · Accedi al tuo progetto Supabase")));
        Body.Add(Ui.Field(Reuse(ref _url, () => Ui.Entry(Loc.T("Indirizzo del progetto (https://….supabase.co)"), Cloud.Url, 15, Keyboard.Url))));
        Body.Add(Ui.Field(Reuse(ref _key, () => Ui.Entry(Loc.T("Chiave publishable (o anon)"), Cloud.ApiKey, 15, Keyboard.Plain))));
        var hint = Ui.Small(Loc.T("Le trovi in Supabase → Project Settings → API Keys. Non usare mai la chiave secret (service_role)."));
        hint.FontFamily = Ui.FontRegular;
        Body.Add(hint);
        Body.Add(Ui.Field(Reuse(ref _email, () => Ui.Entry(Loc.T("Email"), Cloud.Email, 15, Keyboard.Email))));
        Body.Add(Ui.Field(Reuse(ref _password, () =>
        {
            var e = Ui.Entry(Loc.T("Password"), null, 15);
            e.IsPassword = true;
            return e;
        })));

        Body.Add(PrimaryButton(Loc.T("Accedi"), async () =>
        {
            if (_busy) return;
            _busy = true;
            _status.Text = Loc.T("Accedo...");
            var errore = await Cloud.SignInAsync(_url.Text, _key.Text, _email.Text, _password.Text);
            _busy = false;
            if (errore != null) { _status.Text = errore; return; }
            _password.Text = "";
            _status.Text = "";
            _serverHasData = null;
            Render();
        }));
    }

    // ---------- 2. Chiave di cifratura ----------
    private void RenderKey()
    {
        Body.Add(Ui.Caps(Loc.T("2 · Chiave di cifratura")));
        var who = Ui.Small(Loc.F("Accesso fatto come {0}.", Cloud.Email));
        who.FontFamily = Ui.FontRegular;
        Body.Add(who);

        if (_serverHasData == null)
        {
            _status.Text = Loc.T("Controllo il cloud...");
            CheckServer();
            return;
        }

        if (_serverHasData == false)
        {
            var t = Ui.Small(Loc.T("Il cloud è vuoto: se questo è il primo dispositivo, crea la chiave. Prima di partire faccio una copia del database."));
            t.FontFamily = Ui.FontRegular;
            Body.Add(t);
            Body.Add(PrimaryButton(Loc.T("Questo è il primo dispositivo"), async () =>
            {
                if (_busy) return;
                _busy = true;
                _status.Text = Loc.T("Creo la chiave e carico i dati...");
                try
                {
                    await Cloud.CreateKeyAsync();
                    _sync.StopWifi();
                    _status.Text = "";
                }
                catch (Exception ex) { _status.Text = ex.Message; }
                _busy = false;
                Render();
            }));
            Body.Add(Ui.Caps(Loc.T("Oppure, se hai già un dispositivo collegato")));
        }
        else
        {
            var t = Ui.Small(Loc.T("Sull'altro dispositivo apri Sincronizzazione → «Associa un altro dispositivo» e scrivi qui il codice."));
            t.FontFamily = Ui.FontRegular;
            Body.Add(t);
        }

        var code = Reuse(ref _codeEntry, () =>
        {
            var e = Ui.Entry("XXXXX-XXXXX", null, 24, Keyboard.Plain);
            e.MaxLength = 11;
            e.FontFamily = Ui.FontHeavy;
            e.CharacterSpacing = 4;
            e.HorizontalTextAlignment = TextAlignment.Center;
            e.PlaceholderColor = Ui.C("BorderColor");
            return e;
        });
        var field = Ui.Field(code);
        field.MinimumHeightRequest = 56;
        Body.Add(field);

        Action associa = async () =>
        {
            if (_busy) return;
            _busy = true;
            _status.Text = Loc.T("Associo...");
            try
            {
                var errore = await Cloud.JoinWithCodeAsync(_codeEntry.Text);
                if (errore != null) _status.Text = errore;
                else { _sync.StopWifi(); _status.Text = ""; }
            }
            catch (Exception ex) { _status.Text = ex.Message; }
            _busy = false;
            Render();
        };
        // Se il cloud è vuoto il pulsante principale è "primo dispositivo", questo diventa secondario
        Body.Add(_serverHasData == false
            ? SecondaryButton(Loc.T("Associa con il codice"), associa)
            : PrimaryButton(Loc.T("Associa con il codice"), associa));

        Body.Add(Ui.LinkLabel(Loc.T("Esci"), SignOut));
    }

    private async void CheckServer()
    {
        try
        {
            _serverHasData = await Cloud.ServerHasDataAsync();
            _status.Text = "";
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
            return;
        }
        Render();
    }

    // ---------- 3. Attiva ----------
    private void RenderActive()
    {
        var v = new VerticalStackLayout { Spacing = 6 };
        var t = Ui.CardTitle(Loc.T("Sincronizzazione cloud attiva"), 0);
        v.Add(t);
        var s = Ui.Small(Cloud.LastSync is DateTime last
            ? Loc.F("{0} · ultimo allineamento {1}", Cloud.Email, last.ToString(last.Date == DateTime.Today ? "HH:mm" : "d MMM HH:mm", Ui.It))
            : Cloud.Email);
        s.FontFamily = Ui.FontRegular;
        v.Add(s);
        if (_sync.PendingConflicts > 0)
            v.Add(Ui.Small(_sync.PendingConflicts == 1 ? Loc.T("1 conflitto da risolvere") : Loc.F("{0} conflitti da risolvere", _sync.PendingConflicts), "TextColor"));
        Body.Add(Ui.Card(v));

        Body.Add(PrimaryButton(_sync.PendingConflicts > 0 ? Loc.T("Risolvi conflitti") : Loc.T("Sincronizza ora"), SyncNow));

        if (_code != null)
        {
            var c = new VerticalStackLayout { Spacing = 10 };
            var cap = Ui.Caps(Loc.T("Codice per il nuovo dispositivo"));
            cap.HorizontalOptions = LayoutOptions.Center;
            c.Add(cap);
            c.Add(new Label { Text = _code, FontSize = 34, FontFamily = Ui.FontHeavy, CharacterSpacing = 4, TextColor = Ui.C("TextColor"), HorizontalOptions = LayoutOptions.Center });
            var valid = Ui.Small(Loc.T("Valido per 10 minuti · sul nuovo dispositivo accedi con lo stesso account e scrivi il codice"));
            valid.HorizontalTextAlignment = TextAlignment.Center;
            c.Add(valid);
            Body.Add(Ui.Card(c, new Thickness(22)));
        }
        else
        {
            Body.Add(SecondaryButton(Loc.T("Associa un altro dispositivo"), async () =>
            {
                if (_busy) return;
                _busy = true;
                try { _code = await Cloud.CreatePairingCodeAsync(); }
                catch (Exception ex) { _status.Text = ex.Message; }
                _busy = false;
                Render();
            }));
        }

        var esci = Ui.LinkLabel(Loc.T("Esci da questo dispositivo"), SignOut);
        esci.HorizontalOptions = LayoutOptions.Center;
        Body.Add(esci);
    }

    private async void SyncNow()
    {
        await _sync.SyncAsync(async conflicts =>
        {
            var page = new SyncConflictsPage(conflicts, Loc.T("Nel cloud"));
            await Navigation.PushModalAsync(page);
            return await page.Result;
        });
        Render();
    }

    private async void SignOut()
    {
        if (!await DisplayAlert(Loc.T("Esci"), Loc.T("Questo dispositivo smette di sincronizzarsi. I dati restano qui e nel cloud."), Loc.T("Esci"), Loc.T("Annulla"))) return;
        await Cloud.SignOutAsync();
        _code = null;
        _serverHasData = null;
        Render();
    }
}
