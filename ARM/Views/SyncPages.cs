using ARM.Services.Sync;
using Microsoft.Maui.Controls.Shapes;

namespace ARM.Views;

// Base dei fogli a comparsa (ARM 2.5): intestazione [chiudi] titolo [spazio], contenuto con margine 20
public abstract class SheetPage : ContentPage
{
    protected readonly VerticalStackLayout Body = new() { Spacing = 18, Padding = new Thickness(20, 8, 20, 32) };
    protected readonly Grid Root;

    protected SheetPage(string title)
    {
        BackgroundColor = Ui.C("BgColor");
        NavigationPage.SetHasNavigationBar(this, false);

        Root = new Grid { RowDefinitions = new RowDefinitionCollection(new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto)) };
        Root.Add(EditorPageBase.SheetHeader(title, OnClose));
        Root.Add(new ScrollView { Content = new ContentView { Content = Body, MaximumWidthRequest = 640 } }, 0, 1);
        Content = Root;
    }

    protected virtual async void OnClose() => await Navigation.PopModalAsync();

    // Barra fissa in fondo (es. "Applica le scelte")
    protected void SetFooter(View v)
    {
        var bar = new Grid { Padding = new Thickness(20, 12, 20, 28), BackgroundColor = Ui.C("BgColor") };
        bar.Add(new BoxView { HeightRequest = 1, Color = Ui.C("BorderColor"), VerticalOptions = LayoutOptions.Start, Margin = new Thickness(-20, -12, -20, 0) });
        bar.Add(v);
        Root.Add(bar, 0, 2);
    }

    protected static Border PrimaryButton(string text, Action onTap, string colorKey = "AccentColor") => Ui.Button(text, onTap, colorKey: colorKey);

    protected static Border SecondaryButton(string text, Action onTap, string colorKey = "AccentColor") => Ui.Button(text, onTap, ghost: true, colorKey: colorKey == "SubTextColor" ? "TextColor" : colorKey);
}

// ================= SINCRONIZZAZIONE =================
public class SyncPage : SheetPage
{
    private readonly SyncService _sync;
    private readonly Label _status = Ui.Small("");
    private string _code;                 // PC: codice di abbinamento mostrato
    private (string Host, string Id, string Name)? _found;   // telefono: PC trovato
    private Entry _codeEntry;
    private Entry _ipEntry;
    private bool _searched;

    public SyncPage(SyncService sync) : base(Loc.T("Sincronizzazione"))
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
        _sync.StopPairing();
    }

    private void OnStatus() => _status.Text = _sync.Status;

    private void Render()
    {
        Ui.Detach(_status);
        Body.Children.Clear();
        Body.Add(Privacy());

        if (_sync.IsHost) RenderPc(); else RenderPhone();

        _status.Text = _sync.Status;
        Body.Add(_status);
        Body.Add(InfoCard());
    }

    // Riga informativa su fondo FieldBg
    private static View Privacy()
    {
        var g = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)), ColumnSpacing = 10 };
        g.Add(Ui.Icon(Ui.Icons.Wifi, Ui.C("AccentColor"), 20));
        var t = Ui.Small(Loc.T("Solo rete Wi-Fi di casa, niente cloud. I dati non escono dai tuoi dispositivi."), "TextColor");
        t.FontFamily = Ui.FontRegular;
        t.VerticalOptions = LayoutOptions.Center;
        g.Add(t, 1);
        return new Border { Padding = new Thickness(14, 12), StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 16 }, BackgroundColor = Ui.C("FieldBgColor"), Content = g };
    }

    // Riga dispositivo: icona tonda 40 · nome · sottotitolo · azione a destra
    private static Grid DeviceRow(string icon, string colorKey, string name, string sub, View right = null)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)), ColumnSpacing = 12, Padding = new Thickness(16, 8, 8, 8), MinimumHeightRequest = 64 };
        row.Add(Ui.IconTile(icon, colorKey, 40));
        var info = new VerticalStackLayout { Spacing = 1, VerticalOptions = LayoutOptions.Center };
        var n = Ui.Body(name);
        n.FontFamily = Ui.FontBold;
        info.Add(n);
        var s = Ui.Small(sub);
        s.FontSize = 12;
        info.Add(s);
        row.Add(info, 1);
        if (right != null) row.Add(right, 2);
        return row;
    }

    // ---------- PC ----------
    private void RenderPc()
    {
        // Abbinamento appena completato: torna all'elenco dei dispositivi
        if (_code != null && !_sync.IsPairing) _code = null;

        if (_code != null)
        {
            var v = new VerticalStackLayout { Spacing = 10 };
            var cap = Ui.Caps(Loc.T("Codice di abbinamento"));
            cap.HorizontalOptions = LayoutOptions.Center;
            v.Add(cap);
            var code = new Label { Text = $"{_code[..3]} {_code[3..]}", FontSize = 44, FontFamily = Ui.FontHeavy, CharacterSpacing = 8, TextColor = Ui.C("TextColor"), HorizontalOptions = LayoutOptions.Center };
            v.Add(code);
            var valid = Ui.Small(Loc.T("Valido per 5 minuti · sul telefono apri ARM → Oggi → Sincronizzazione"));
            valid.HorizontalTextAlignment = TextAlignment.Center;
            v.Add(valid);
            var renew = Ui.Button(Loc.T("Genera un nuovo codice"), () => { _code = _sync.StartPairing(); Render(); }, ghost: true, small: true);
            renew.HorizontalOptions = LayoutOptions.Center;
            renew.Margin = new Thickness(0, 6, 0, 0);
            v.Add(renew);
            Body.Add(Ui.Card(v, new Thickness(22)));
            Body.Add(SecondaryButton(Loc.T("Annulla"), () => { _code = null; _sync.StopPairing(); Render(); }, "SubTextColor"));
            return;
        }

        if (!_sync.IsPaired)
        {
            Body.Add(PrimaryButton(Loc.T("Associa un telefono o tablet"), () => { _code = _sync.StartPairing(); Render(); }));
            return;
        }

        // Dispositivi associati, ognuno con la sua ultima sincronizzazione
        var list = new VerticalStackLayout();
        foreach (var d in _sync.Devices)
        {
            var device = d;
            if (list.Children.Count > 0) list.Add(Ui.Divider());
            bool recent = device.LastSync is DateTime last && (DateTime.Now - last).TotalDays < 1;
            var remove = new Label { Text = Loc.T("Dissocia"), FontSize = 14, FontFamily = Ui.FontBold, TextColor = Ui.C("Accent2Color"), VerticalOptions = LayoutOptions.Center, Padding = new Thickness(8, 12) };
            Ui.OnTap(remove, async () =>
            {
                if (!await DisplayAlert(Loc.T("Dissocia"), Loc.F("{0} smetterà di sincronizzarsi con questo PC. I dati restano dove sono.", device.Name), Loc.T("Dissocia"), Loc.T("Annulla"))) return;
                _sync.UnpairDevice(device.Id);
                Render();
            });
            list.Add(DeviceRow(SyncIcons.Phone, recent ? "Accent3Color" : "SubTextColor", device.Name,
                device.LastSync is DateTime t ? Loc.F("Sincronizzato {0} alle {1:HH:mm}", Ui.RelativeDate(t).ToLower(Ui.It), t) : Loc.T("Mai sincronizzato"), remove));
        }
        Body.Add(new VerticalStackLayout { Spacing = 8, Children = { Ui.Caps(Loc.T("Dispositivi associati")), Ui.Card(list, new Thickness(0, 2)) } });
        Body.Add(SecondaryButton(Loc.T("Associa un altro dispositivo"), () => { _code = _sync.StartPairing(); Render(); }));
    }

    // ---------- Telefono ----------
    private void RenderPhone()
    {
        if (_sync.IsPaired)
        {
            var v = new VerticalStackLayout { Spacing = 10 };
            var ok = Ui.IconTile(Ui.Icons.Check, "Accent3Color", 56);
            ok.HorizontalOptions = LayoutOptions.Center;
            v.Add(ok);
            var t = Ui.CardTitle(Loc.F("Collegato a {0}", _sync.PeerName), 0);
            t.HorizontalTextAlignment = TextAlignment.Center;
            v.Add(t);
            var s = Ui.Small(_sync.LastSync is DateTime d ? Loc.F("Ultima sincronizzazione: {0} alle {1:HH:mm}", Ui.RelativeDate(d).ToLower(), d) : Loc.T("Non ancora sincronizzati"));
            s.HorizontalTextAlignment = TextAlignment.Center;
            v.Add(s);
            var unpair = Ui.LinkLabel(Loc.T("Dissocia questo telefono"), Unpair);
            unpair.HorizontalOptions = LayoutOptions.Center;
            v.Add(unpair);
            Body.Add(Ui.Card(v, new Thickness(22)));

            if (_sync.PendingConflicts > 0)
                Body.Add(PrimaryButton(_sync.PendingConflicts == 1 ? Loc.T("Risolvi 1 conflitto") : Loc.F("Risolvi {0} conflitti", _sync.PendingConflicts), SyncNow, "Accent2Color"));
            else Body.Add(PrimaryButton(Loc.T("Sincronizza ora"), SyncNow, "AccentColor"));
            return;
        }

        // Non abbinato: 1) trova il PC  2) inserisci il codice
        if (_found == null)
        {
            Body.Add(Ui.Caps(Loc.T("1 · Trova il PC")));
            Body.Add(PrimaryButton(Loc.T("Cerca il PC sulla rete Wi-Fi"), async () =>
            {
                _status.Text = Loc.T("Cerco il PC...");
                _found = await _sync.DiscoverAsync();
                _searched = true;
                Render();
                _status.Text = _found == null ? Loc.T("Nessun PC trovato. Apri ARM sul PC e premi \"Associa un telefono o tablet\", oppure scrivi qui sotto l'indirizzo mostrato sul PC.") : "";
            }));

            if (_searched)
            {
                // Ripiego: indirizzo IP inserito a mano (si legge sulla schermata del PC)
                _ipEntry ??= Ui.Entry(Loc.T("Indirizzo del PC (es. 192.168.1.20)"), null, 16, Keyboard.Url);
                Ui.Detach(_ipEntry);
                Body.Add(Ui.Field(_ipEntry));
                Body.Add(SecondaryButton(Loc.T("Usa questo indirizzo"), () =>
                {
                    if (string.IsNullOrWhiteSpace(_ipEntry.Text)) return;
                    _found = (_ipEntry.Text.Trim(), "", _ipEntry.Text.Trim());
                    Render();
                }));
            }
            return;
        }

        Body.Add(Ui.Caps(Loc.T("1 · Trova il PC")));
        var pc = Ui.Card(DeviceRow(SyncIcons.Laptop, "AccentColor", _found.Value.Name, _found.Value.Host), new Thickness(0));
        pc.Stroke = new SolidColorBrush(Ui.C("AccentColor"));
        pc.StrokeThickness = 2;
        Body.Add(pc);

        Body.Add(Ui.Caps(Loc.T("2 · Inserisci il codice mostrato sul PC")));
        _codeEntry ??= Ui.Entry("000000", null, 26, Keyboard.Numeric);
        _codeEntry.MaxLength = 7;
        _codeEntry.FontFamily = Ui.FontHeavy;
        _codeEntry.CharacterSpacing = 8;
        _codeEntry.HorizontalTextAlignment = TextAlignment.Center;
        _codeEntry.PlaceholderColor = Ui.C("BorderColor");
        Ui.Detach(_codeEntry);
        var field = Ui.Field(_codeEntry);
        field.MinimumHeightRequest = 56;
        Body.Add(field);

        Body.Add(PrimaryButton(Loc.T("Abbina"), async () =>
        {
            var code = (_codeEntry.Text ?? "").Replace(" ", "");
            if (code.Length != 6) { _status.Text = Loc.T("Il codice è di 6 cifre."); return; }
            _status.Text = Loc.T("Abbino...");
            var errore = await _sync.PairAsync(_found.Value.Host, code);
            if (errore != null) { _status.Text = errore; return; }
            Render();
        }));
        Body.Add(SecondaryButton(Loc.T("Cerca di nuovo"), () => { _found = null; Render(); }, "SubTextColor"));
    }

    private View InfoCard()
    {
        var v = new VerticalStackLayout { Spacing = 8 };
        v.Add(Ui.CardTitle(Loc.T("Come funziona")));
        string testo = _sync.IsHost
            ? Loc.F("Il PC resta in ascolto mentre ARM è aperto (anche ridotto a icona). Il telefono si sincronizza da solo quando siete sulla stessa rete Wi-Fi.\n\nIn ascolto su {0}", SyncService.LocalAddress())
            : Loc.T("Quando telefono e PC sono sulla stessa rete Wi-Fi (e ARM è aperto sul PC), le modifiche si uniscono da sole. Se hai cambiato la stessa cosa su entrambi, ti chiedo quale versione tenere. I brani scaricati passano da un dispositivo all'altro.");
        var t = Ui.Small(testo);
        t.FontFamily = Ui.FontRegular;
        v.Add(t);
        return Ui.Card(v);
    }

    private async void SyncNow()
    {
        await _sync.SyncAsync(async conflicts =>
        {
            var page = new SyncConflictsPage(conflicts, _sync.PeerName);
            await Navigation.PushModalAsync(page);
            return await page.Result;
        });
        Render();
    }

    private async void Unpair()
    {
        if (!await DisplayAlert(Loc.T("Dissocia"), Loc.T("I due dispositivi smetteranno di sincronizzarsi. I dati restano dove sono."), Loc.T("Dissocia"), Loc.T("Annulla"))) return;
        _sync.Unpair();
        _found = null;
        Render();
    }
}

// ================= CONFLITTI =================
// Per ogni elemento le due versioni affiancate (raggio 18, almeno 120 alte); quella scelta ha bordo 2 Accent e fondo SoftAccent
public class SyncConflictsPage : SheetPage
{
    private readonly TaskCompletionSource<bool> _result = new();
    private readonly List<(SyncConflict Conflict, Border Local, Border Remote)> _rows = new();
    public Task<bool> Result => _result.Task;

    public SyncConflictsPage(List<SyncConflict> conflicts, string peerName) : base(Loc.T("Conflitti"))
    {
        string remoteName = string.IsNullOrWhiteSpace(peerName) ? Loc.T("Sul PC") : peerName;

        Body.Add(Ui.Body(conflicts.Count == 1
            ? Loc.T("Questo elemento è stato modificato su entrambi i dispositivi. Scegli quale versione tenere: tutto il resto è già stato unito.")
            : Loc.F("Questi {0} elementi sono stati modificati su entrambi i dispositivi. Scegli quale versione tenere: tutto il resto è già stato unito.", conflicts.Count), "SubTextColor"));

        var all = new HorizontalStackLayout { Spacing = 8 };
        all.Add(Ui.Chip(Loc.T("Tutto da qui"), false, "TextColor", () => SetAll(true), SyncIcons.Phone));
        all.Add(Ui.Chip(Loc.T("Tutto dall'altro"), false, "TextColor", () => SetAll(false), SyncIcons.Laptop));
        Body.Add(all);

        foreach (var c in conflicts)
        {
            var v = new VerticalStackLayout { Spacing = 10 };
            var head = new HorizontalStackLayout { Spacing = 8 };
            head.Add(Ui.Badge(c.Kind, "AccentColor"));
            var title = Ui.CardTitle(c.Title, 1);
            title.VerticalOptions = LayoutOptions.Center;
            head.Add(title);
            v.Add(head);

            var local = Option(Loc.T("Su questo dispositivo"), c.LocalSummary);
            var remote = Option(remoteName, c.RemoteSummary);
            var conflict = c;
            Ui.Press(local, () => { conflict.UseLocal = true; Refresh(); }, 0.98);
            Ui.Press(remote, () => { conflict.UseLocal = false; Refresh(); }, 0.98);
            var pair = new Grid { ColumnSpacing = 10, ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)) };
            pair.Add(local);
            pair.Add(remote, 1);
            v.Add(pair);
            Body.Add(v);
            _rows.Add((c, local, remote));
        }

        SetFooter(PrimaryButton(Loc.T("Applica le scelte"), async () =>
        {
            _result.TrySetResult(true);
            await Navigation.PopModalAsync();
        }));
        Refresh();
    }

    protected override async void OnClose()
    {
        _result.TrySetResult(false);
        await Navigation.PopModalAsync();
    }

    private static Border Option(string who, string summary)
    {
        var top = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)) };
        var cap = Ui.Caps(who);
        cap.FontSize = 10;
        top.Add(cap);
        var tick = Ui.Icon(Ui.Icons.Task, Ui.C("AccentColor"), 18);
        top.Add(tick, 1);
        var t = Ui.Small(summary, "TextColor");
        t.FontFamily = Ui.FontRegular;
        t.MaxLines = 5;
        t.LineBreakMode = LineBreakMode.TailTruncation;
        return new Border
        {
            Content = new VerticalStackLayout { Spacing = 6, Children = { top, t } },
            Padding = new Thickness(14),
            MinimumHeightRequest = 120,
            StrokeShape = new RoundRectangle { CornerRadius = 18 },
            ClassId = "opt"
        };
    }

    private void SetAll(bool useLocal)
    {
        foreach (var r in _rows) r.Conflict.UseLocal = useLocal;
        Refresh();
    }

    // Evidenzia la versione scelta (aggiorna i colori, senza ricreare le viste)
    private void Refresh()
    {
        foreach (var (c, local, remote) in _rows)
        {
            Style(local, c.UseLocal);
            Style(remote, !c.UseLocal);
        }

        static void Style(Border b, bool selected)
        {
            b.BackgroundColor = selected ? Ui.C("SoftAccentColor") : Ui.C("TabBgColor");
            b.Stroke = new SolidColorBrush(selected ? Ui.C("AccentColor") : Ui.C("BorderColor"));
            b.StrokeThickness = selected ? 2 : 1;
            var tick = ((Grid)((VerticalStackLayout)b.Content).Children[0]).Children[1] as View;
            if (tick != null) tick.IsVisible = selected;
        }
    }
}

public static class SyncIcons
{
    public const string Sync = Ui.Icons.Sync;
    public const string Phone = Ui.Icons.Phone;
    public const string Laptop = Ui.Icons.Pc;
}
