using ARMusic.Services;
using Microsoft.Maui.Controls.Shapes;

namespace ARMusic.Views;

// Importa una playlist da Spotify in ARMusic (i brani mancanti vengono scaricati da YouTube)
public class SpotifyImportPage : SheetPage
{
    private const string SpotifyGreen = "#1DB954";

    private readonly SpotifyService _spotify;
    private readonly Action _onImported;
    private readonly Label _status = Ui.Text("", 13, "SubTextColor");
    private Entry _link, _clientId, _clientSecret;
    private SpotifyPlaylist _preview;
    private CancellationTokenSource _cts;
    private bool _importing;

    public SpotifyImportPage(SpotifyService spotify, Action onImported) : base(Loc.T("Importa da Spotify"))
    {
        _spotify = spotify;
        _onImported = onImported;
        _status.HorizontalTextAlignment = TextAlignment.Center;
        Render();
    }

    protected override void OnClose()
    {
        _cts?.Cancel();
        base.OnClose();
    }

    private void Render()
    {
        Ui.Detach(_status);
        Body.Children.Clear();
        if (!_spotify.HasCredentials) RenderSetup();
        else if (_preview == null) RenderLink();
        else RenderPreview();
        Body.Add(_status);
    }

    private static Entry MakeEntry(string placeholder, Keyboard keyboard = null) => new()
    {
        Placeholder = placeholder,
        TextColor = Ui.C("TextColor"),
        PlaceholderColor = Ui.C("SubTextColor"),
        BackgroundColor = Colors.Transparent,
        FontSize = 15,
        Keyboard = keyboard ?? Keyboard.Text
    };

    // ---------- 1. Prima volta: collegare ARM all'API di Spotify ----------
    private void RenderSetup()
    {
        var v = new VerticalStackLayout { Spacing = 10 };
        v.Add(Ui.Text(Loc.T("Collega Spotify (una volta sola)"), 17, bold: true));
        v.Add(Ui.Text(Loc.T("Per leggere le playlist ARM usa l'API ufficiale di Spotify. Serve una piccola \"app\" gratuita a tuo nome:"), 13, "SubTextColor"));
        v.Add(Ui.Text(Loc.T("1. Apri developer.spotify.com/dashboard e accedi con il tuo account Spotify.\n") +
                      Loc.T("2. Premi \"Create app\": nome e descrizione a piacere, come Redirect URI scrivi https://localhost e spunta \"Web API\".\n") +
                      Loc.T("3. Apri l'app → Settings e copia qui sotto Client ID e Client Secret."), 13));
        var open = SecondaryButton(Loc.T("Apri developer.spotify.com"), async () => await Launcher.OpenAsync("https://developer.spotify.com/dashboard"), "Accent3Color");
        v.Add(open);
        Body.Add(Ui.Card(v));

        _clientId ??= MakeEntry(Loc.T("Client ID"));
        _clientSecret ??= MakeEntry(Loc.T("Client Secret"));
        _clientSecret.IsPassword = true;
        Ui.Detach(_clientId);
        Ui.Detach(_clientSecret);
        Body.Add(Ui.Field(_clientId));
        Body.Add(Ui.Field(_clientSecret));
        Body.Add(PrimaryButton(Loc.T("Salva"), () =>
        {
            if (string.IsNullOrWhiteSpace(_clientId.Text) || string.IsNullOrWhiteSpace(_clientSecret.Text))
            {
                _status.Text = Loc.T("Servono entrambi i codici.");
                return;
            }
            _spotify.SaveCredentials(_clientId.Text, _clientSecret.Text);
            _status.Text = "";
            Render();
        }));
    }

    // ---------- 2. Link della playlist ----------
    private void RenderLink()
    {
        var v = new VerticalStackLayout { Spacing = 10 };
        var head = new HorizontalStackLayout { Spacing = 10 };
        head.Add(new Border
        {
            WidthRequest = 38, HeightRequest = 38, StrokeShape = new RoundRectangle { CornerRadius = 19 }, Stroke = Colors.Transparent,
            BackgroundColor = Color.FromArgb(SpotifyGreen),
            Content = Ui.Icon(Ui.Icons.Music, Colors.White, 18)
        });
        var t = Ui.Text(Loc.T("Incolla il link della playlist"), 16, bold: true);
        t.VerticalOptions = LayoutOptions.Center;
        head.Add(t);
        v.Add(head);
        v.Add(Ui.Text(Loc.T("Su Spotify: apri la playlist → ⋯ → Condividi → Copia link. Deve essere pubblica."), 13, "SubTextColor"));
        _link ??= MakeEntry(Loc.T("https://open.spotify.com/playlist/..."), Keyboard.Url);
        Ui.Detach(_link);
        v.Add(Ui.Field(_link));
        Body.Add(Ui.Card(v));

        Body.Add(PrimaryButton(Loc.T("Leggi la playlist"), async () =>
        {
            var id = SpotifyService.ParsePlaylistId(_link.Text);
            if (id == null) { _status.Text = Loc.T("Questo non sembra un link a una playlist di Spotify."); return; }
            _status.Text = Loc.T("Leggo la playlist...");
            try
            {
                _preview = await _spotify.GetPlaylistAsync(id);
                _status.Text = "";
                Render();
            }
            catch (Exception ex) { _status.Text = ex.Message; }
        }));

        var reset = Ui.Text(Loc.T("Cambia le credenziali di Spotify"), 12, "SubTextColor");
        reset.HorizontalOptions = LayoutOptions.Center;
        Ui.OnTap(reset, () => { _spotify.ClearCredentials(); Render(); });
        Body.Add(reset);
    }

    // ---------- 3. Anteprima e importazione ----------
    private void RenderPreview()
    {
        var g = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(84), new ColumnDefinition(GridLength.Star)), ColumnSpacing = 14 };
        g.Add(new Border
        {
            WidthRequest = 84, HeightRequest = 84, StrokeShape = new RoundRectangle { CornerRadius = 16 }, Stroke = Colors.Transparent,
            BackgroundColor = Ui.C("SoftAccentColor"),
            Content = new Image { Source = _preview.ImageUrl, Aspect = Aspect.AspectFill }
        });
        var info = new VerticalStackLayout { Spacing = 4, VerticalOptions = LayoutOptions.Center };
        info.Add(Ui.Text(_preview.Name, 18, bold: true, maxLines: 2));
        info.Add(Ui.Text(_preview.Tracks.Count == 1 ? Loc.T("1 brano") : Loc.F("{0} brani", _preview.Tracks.Count), 13, "SubTextColor"));
        g.Add(info, 1);

        var v = new VerticalStackLayout { Spacing = 12 };
        v.Add(g);

        // Primi brani, per riconoscerla
        var list = new VerticalStackLayout { Spacing = 4 };
        foreach (var tr in _preview.Tracks.Take(5))
            list.Add(Ui.Text($"• {tr.Artist} – {tr.Title}", 13, "SubTextColor", maxLines: 1));
        if (_preview.Tracks.Count > 5) list.Add(Ui.Text(Loc.F("e altri {0}...", _preview.Tracks.Count - 5), 12, "SubTextColor"));
        v.Add(list);
        Body.Add(Ui.Card(v));

        // Avanzamento
        var bar = new ProgressBar { ProgressColor = Color.FromArgb(SpotifyGreen), IsVisible = false };
        var detail = Ui.Text("", 13, "SubTextColor");
        Body.Add(bar);
        Body.Add(detail);

        Border action = null;
        SpotifyService.ImportProgress last = null;
        bool done = false;
        action = PrimaryButton(Loc.T("Importa in ARMusic"), async () =>
        {
            if (done) { OnClose(); return; }
            if (_importing) { _cts?.Cancel(); return; }
            _importing = true;
            _cts = new CancellationTokenSource();
            bar.IsVisible = true;
            ((Label)action.Content).Text = Loc.T("Interrompi");

            var progress = new Progress<SpotifyService.ImportProgress>(p =>
            {
                last = p;
                bar.Progress = p.Total == 0 ? 1 : (double)p.Done / p.Total;
                detail.Text = $"{p.Done}/{p.Total}" + (p.Current != null ? $" · {p.Current}" : "") +
                              Loc.F("\nGià in libreria: {0} · Scaricati: {1} · Da scaricare: {2} · Non trovati: {3}", p.Reused, p.Downloaded, p.NotDownloaded, p.NotFound);
            });

            try
            {
                await _spotify.ImportAsync(_preview, progress, _cts.Token);
                _onImported?.Invoke();
                _status.Text = Loc.T("Playlist importata! La trovi in ARMusic → Libreria.");
                if (last?.NotDownloaded > 0)
                    _status.Text += "\n" + Loc.T("Alcuni brani non sono stati scaricati: ") + _spotify.LastDownloadError +
                                    " " + Loc.T("Puoi riprovare dalla playlist con «Scarica i brani mancanti».");
                ((Label)action.Content).Text = Loc.T("Chiudi");
                done = true;
                action.BackgroundColor = Ui.C("Accent3Color");
            }
            catch (OperationCanceledException)
            {
                _onImported?.Invoke();
                _status.Text = Loc.T("Importazione interrotta: la playlist contiene i brani importati finora.");
                ((Label)action.Content).Text = Loc.T("Interrotta");
            }
            catch (Exception ex)
            {
                _status.Text = ex.Message;
                ((Label)action.Content).Text = Loc.T("Riprova");
            }
            finally
            {
                _importing = false;
            }
        }, "Accent3Color");
        Body.Add(action);
        Body.Add(SecondaryButton(Loc.T("Scegli un'altra playlist"), () => { if (!_importing) { _preview = null; Render(); } }, "SubTextColor"));
    }
}
