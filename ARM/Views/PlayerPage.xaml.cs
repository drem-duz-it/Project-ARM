using ARM.Models;
using ARM.Services;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Maui.Core.Primitives; // Fondamentale per far riconoscere Position!

namespace ARM.Views;

public partial class PlayerPage : ContentPage
{
    private readonly YouTubeService _youTubeService;
    private readonly AudioService _audioService;
    private bool _isLooping = false;
    private bool _isDragging = false;

    // Testo della canzone
    private readonly LyricsService _lyrics = IPlatformApplication.Current.Services.GetService<LyricsService>();
    private bool _testoVisibile;
    private Lyrics _testo;
    private string _testoId;
    private CancellationTokenSource _cercaTesto;
    private readonly List<Label> _righe = new();
    private int _rigaAttiva = -1;
    private bool _scrollAutomatico;
    private DateTime _scrollUtente = DateTime.MinValue;   // quando hai scorso a mano: per qualche secondo non ti riporto alla riga attuale

    // Costruttore con iniezione dei servizi globali
    public PlayerPage(Song song, YouTubeService ytService, AudioService audioService)
    {
        InitializeComponent();
        Loc.Register(this);

        // il pannello del testo è largo come serve, ma mai più dello schermo (telefono compreso)
        SizeChanged += (_, _) => { if (Width > 0) { var s = Math.Max(240, Math.Min(334, Width - 56)); CoverBox.WidthRequest = CoverBox.HeightRequest = LyricsBox.WidthRequest = LyricsBox.HeightRequest = s; } };

        _youTubeService = ytService;
        _audioService = audioService;

        TitleLabel.Text = song.Title;
        ArtistLabel.Text = song.Artist;
        CoverImage.Source = song.ThumbnailUrl;
        NavigationPage.SetHasNavigationBar(this, false);

        // Controlla se il brano fa parte di una playlist
        if (!_audioService.IsQueueActive)
        {
            // Niente playlist? Il bottone si ingrigisce e non può essere cliccato!
            ShuffleButtonContainer.Opacity = 0.3;
            ShuffleButtonContainer.InputTransparent = true;
        }
        else
        {
            ShuffleButtonContainer.Opacity = 1;
            ShuffleButtonContainer.InputTransparent = false;
            AggiornaGraficaShuffle();
        }

        // Gli eventi del player globale si agganciano in OnAppearing (e si sganciano in OnDisappearing)
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _audioService.Player.PositionChanged += Player_PositionChanged;
        _audioService.Player.MediaOpened += Player_MediaOpened;
        _audioService.OnSongChanged += AudioService_OnSongChanged;
        _audioService.PlaybackChanged += AggiornaIconaPlayPause;

        // Allinea i pulsanti allo stato REALE del lettore (es. app riaperta mentre la musica suona)
        AggiornaIconaPlayPause(_audioService.IsPlaying);
        _isLooping = _audioService.Player.ShouldLoopPlayback;
        AggiornaGraficaLoop();
        if (_audioService.IsQueueActive) AggiornaGraficaShuffle();
        Player_MediaOpened(this, EventArgs.Empty);
    }
    private void AudioService_OnSongChanged(Song nuovaCanzone)
    {
        // Usiamo il MainThread perché stiamo modificando la grafica
        MainThread.BeginInvokeOnMainThread(() =>
        {
            TitleLabel.Text = nuovaCanzone.Title;
            ArtistLabel.Text = nuovaCanzone.Artist;
            CoverImage.Source = nuovaCanzone.ThumbnailUrl;
            AggiornaIconaPlayPause(_audioService.IsPlaying);

            // Il testo è quello della canzone precedente: se la vista è aperta cerca il nuovo, altrimenti lo scarta
            if (_testoId != nuovaCanzone.Id)
            {
                _testo = null; _testoId = null; _rigaAttiva = -1;
                if (_testoVisibile) _ = CaricaTestoAsync();
                else MostraStato("");
            }
        });
    }

    // ---------------- TESTO ----------------

    private async void OnLyricsClicked(object sender, TappedEventArgs e)
    {
        _testoVisibile = !_testoVisibile;
        CoverBox.IsVisible = !_testoVisibile;
        LyricsBox.IsVisible = _testoVisibile;
        // Chip "Testo" attiva: fondo TextColor, testo e icona BgColor
        string fg = _testoVisibile ? "BgColor" : "TextColor";
        LyricsButtonContainer.SetDynamicResource(VisualElement.BackgroundColorProperty, _testoVisibile ? "TextColor" : "TabBgColor");
        LyricsIcon.SetDynamicResource(Microsoft.Maui.Controls.Shapes.Shape.FillProperty, fg);
        LyricsLabel.SetDynamicResource(Label.TextColorProperty, fg);
        if (_testoVisibile) await CaricaTestoAsync();
    }

    private void MostraStato(string testo)
    {
        LyricsStatus.Text = testo;
        LyricsStatus.IsVisible = testo.Length > 0;
    }

    private async Task CaricaTestoAsync()
    {
        var canzone = _audioService.CurrentSong;
        if (canzone == null || _lyrics == null) return;
        if (_testoId == canzone.Id && _testo != null) return;

        _cercaTesto?.Cancel();
        var cts = _cercaTesto = new CancellationTokenSource();
        LyricsLines.Children.Clear(); _righe.Clear(); _rigaAttiva = -1;
        MostraStato(Loc.T("Cerco il testo…"));

        try
        {
            // la durata serve a scegliere la versione giusta: aspetta che il lettore la conosca (al massimo 2 secondi)
            for (int i = 0; i < 10 && _audioService.Player.Duration.TotalSeconds <= 0 && !cts.IsCancellationRequested; i++)
                await Task.Delay(200, cts.Token);

            var testo = await _lyrics.CercaAsync(canzone, _audioService.Player.Duration, cts.Token);
            if (cts.IsCancellationRequested || _audioService.CurrentSong?.Id != canzone.Id) return;
            _testo = testo; _testoId = canzone.Id;
            CostruisciTesto(testo);
        }
        catch (OperationCanceledException) { }
        catch { if (!cts.IsCancellationRequested) MostraStato(Loc.T("Il servizio dei testi non risponde.\nControlla la connessione o riprova tra poco.")); }
    }

    private void CostruisciTesto(Lyrics t)
    {
        LyricsLines.Children.Clear(); _righe.Clear(); _rigaAttiva = -1;

        if (t == null || !t.Trovato) { MostraStato(Loc.T("Nessun testo trovato per questo brano.")); return; }
        if (t.Strumentale && t.Righe.Count == 0 && t.Semplice.Length == 0) { MostraStato(Loc.T("Brano strumentale.")); return; }
        MostraStato("");

        if (t.Righe.Count > 0)
        {
            foreach (var riga in t.Righe)
            {
                var label = new Label { Text = riga.Testo, FontSize = 19, FontFamily = Ui.FontHeavy, HorizontalTextAlignment = TextAlignment.Center, Opacity = 0.45 };
                label.SetDynamicResource(Label.TextColorProperty, "SubTextColor");
                var tap = new TapGestureRecognizer();
                double secondi = riga.Secondi;
                tap.Tapped += (_, _) => { _scrollUtente = DateTime.MinValue; _audioService.Player.SeekTo(TimeSpan.FromSeconds(secondi)); };   // tocca una riga per saltare lì
                label.GestureRecognizers.Add(tap);
                _righe.Add(label);
                LyricsLines.Children.Add(label);
            }
            AggiornaRigaTesto(_audioService.Player.Position);
        }
        else
        {
            // senza tempi: testo intero da scorrere a mano
            var label = new Label { Text = t.Semplice, FontSize = 17, FontFamily = Ui.FontSemi, HorizontalTextAlignment = TextAlignment.Center, LineHeight = 1.25 };
            label.SetDynamicResource(Label.TextColorProperty, "TextColor");
            LyricsLines.Padding = new Thickness(20, 24);
            LyricsLines.Children.Add(label);
        }
    }

    // Evidenzia la riga che si sta cantando e la porta al centro
    private async void AggiornaRigaTesto(TimeSpan posizione)
    {
        var righe = _testo?.Righe;
        if (!_testoVisibile || righe == null || righe.Count == 0 || _righe.Count != righe.Count) return;

        double s = posizione.TotalSeconds + 0.25;   // un soffio di anticipo: la riga si accende appena prima di essere cantata
        int idx = -1;
        for (int i = 0; i < righe.Count && righe[i].Secondi <= s; i++) idx = i;
        if (idx == _rigaAttiva) return;

        if (_rigaAttiva >= 0 && _rigaAttiva < _righe.Count)
        {
            _righe[_rigaAttiva].Opacity = 0.45;
            _righe[_rigaAttiva].SetDynamicResource(Label.TextColorProperty, "SubTextColor");
        }
        _rigaAttiva = idx;
        if (idx < 0) return;

        _righe[idx].Opacity = 1;
        _righe[idx].SetDynamicResource(Label.TextColorProperty, "AccentColor");

        if ((DateTime.UtcNow - _scrollUtente).TotalSeconds < 4) return;
        _scrollAutomatico = true;
        try { await LyricsScroll.ScrollToAsync(_righe[idx], ScrollToPosition.Center, true); }
        catch { /* la vista è stata chiusa nel frattempo */ }
        finally { _scrollAutomatico = false; }
    }

    private void OnLyricsScrolled(object sender, ScrolledEventArgs e)
    {
        if (!_scrollAutomatico) _scrollUtente = DateTime.UtcNow;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Sganciamo gli eventi per evitare memory leak, MA la musica non si ferma!
        _audioService.Player.PositionChanged -= Player_PositionChanged;
        _audioService.Player.MediaOpened -= Player_MediaOpened;
        _audioService.OnSongChanged -= AudioService_OnSongChanged;
        _audioService.PlaybackChanged -= AggiornaIconaPlayPause;
    }

    // Nota il punto interrogativo su object? per rispettare la firma esatta del delegato
    // Usiamo 'dynamic' per il parametro e, così ignoriamo il problema del namespace!
    private void Player_PositionChanged(object? sender, dynamic e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var currentPosition = _audioService.Player.Position;
            var duration = _audioService.Player.Duration; // Leggiamo la durata attuale in tempo reale

            // IL FIX È QUI: Aggiorniamo la durata massima (Maximum) e il testo non appena diventano disponibili!
            if (duration.TotalSeconds > 0 && PositionSlider.Maximum != duration.TotalSeconds)
            {
                PositionSlider.Maximum = duration.TotalSeconds;
                TotalTimeLabel.Text = duration.ToString(@"m\:ss");
            }

            // Aggiorna il testo del tempo trascorso
            CurrentTimeLabel.Text = currentPosition.ToString(@"m\:ss");
            AggiornaRigaTesto(currentPosition);

            // Aggiorna la posizione della "pallina" SOLO se non stiamo trascinando
            if (!_isDragging && currentPosition.TotalSeconds >= 0 && currentPosition.TotalSeconds <= PositionSlider.Maximum)
            {
                PositionSlider.Value = currentPosition.TotalSeconds;
            }
        });
    }
    private void Player_MediaOpened(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_audioService.Player.Duration.TotalSeconds > 0)
            {
                PositionSlider.Maximum = _audioService.Player.Duration.TotalSeconds;
                TotalTimeLabel.Text = _audioService.Player.Duration.ToString(@"m\:ss");
            }
        });
    }
    private void OnSliderDragStarted(object sender, EventArgs e)
    {
        _isDragging = true;
    }
    private void OnSliderDragCompleted(object sender, EventArgs e)
    {
        // Sposta la canzone al punto in cui hai lasciato lo slider
        _audioService.Player.SeekTo(TimeSpan.FromSeconds(PositionSlider.Value));

        // Riabilita l'aggiornamento automatico
        _isDragging = false;
    }

    private void OnPlayPauseClicked(object sender, TappedEventArgs e)
    {
        // L'icona si aggiorna da sola quando il lettore cambia davvero stato (PlaybackChanged)
        _audioService.TogglePlayPause();
    }

    // Metodo di supporto per cambiare graficamente il vettore
    private void AggiornaIconaPlayPause(bool isPlaying)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (isPlaying)
            {
                // Icona PAUSA
                PlayPauseIcon.Data = (Microsoft.Maui.Controls.Shapes.Geometry)new Microsoft.Maui.Controls.Shapes.PathGeometryConverter().ConvertFromInvariantString(Ui.Icons.Pause);
            }
            else
            {
                // Icona PLAY
                PlayPauseIcon.Data = (Microsoft.Maui.Controls.Shapes.Geometry)new Microsoft.Maui.Controls.Shapes.PathGeometryConverter().ConvertFromInvariantString(Ui.Icons.Play);
            }
        });
    }
    private void OnLoopClicked(object sender, EventArgs e)
    {
        _isLooping = !_isLooping;
        _audioService.Player.ShouldLoopPlayback = _isLooping;
        AggiornaGraficaLoop();
    }

    private void AggiornaGraficaLoop()
    {
        if (_isLooping)
        {
            if (Application.Current.Resources.TryGetValue("AccentColor", out var accentColor))
                LoopIcon.Fill = new SolidColorBrush((Color)accentColor);
            LoopButtonContainer.SetDynamicResource(Border.BackgroundColorProperty, "TabBgColor");
        }
        else
        {
            if (Application.Current.Resources.TryGetValue("SubTextColor", out var subTextColor))
                LoopIcon.Fill = new SolidColorBrush((Color)subTextColor);
            LoopButtonContainer.BackgroundColor = Colors.Transparent;
        }
    }

    private void OnShuffleClicked(object sender, EventArgs e)
    {
        _audioService.IsShuffleEnabled = !_audioService.IsShuffleEnabled;
        AggiornaGraficaShuffle();
    }

    private void AggiornaGraficaShuffle()
    {
        if (_audioService.IsShuffleEnabled)
        {
            // Acceso: Colore accento e sfondo cerchiato
            if (Application.Current.Resources.TryGetValue("AccentColor", out var accentColor))
                ShuffleIcon.Fill = new SolidColorBrush((Color)accentColor);

            ShuffleButtonContainer.SetDynamicResource(Border.BackgroundColorProperty, "TabBgColor");
        }
        else
        {
            // Spento: Colore grigio e sfondo trasparente
            if (Application.Current.Resources.TryGetValue("SubTextColor", out var subTextColor))
                ShuffleIcon.Fill = new SolidColorBrush((Color)subTextColor);

            ShuffleButtonContainer.BackgroundColor = Colors.Transparent;
        }
    }
    private async void OnCloseClicked(object sender, TappedEventArgs e)
    {
        // Tasto Indietro Universale e Sicuro
        if (Navigation.ModalStack.Count > 0)
        {
            // Se è stata aperta dal basso (Modal)
            await Navigation.PopModalAsync();
        }
        else if (Navigation.NavigationStack.Count > 1)
        {
            // Se è stata aperta di lato (Push normale)
            await Navigation.PopAsync();
        }
    }
    private void OnPreviousClicked(object sender, TappedEventArgs e)
    {
        _audioService.PlayPrevious();
    }
    private void OnSkipClicked(object sender, TappedEventArgs e)
    {
        _audioService.PlayNext();
    }
}