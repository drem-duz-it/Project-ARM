using ARM.Services;
using ARM.Data;
using System.Net.Http;

namespace ARM;

public partial class App : Application
{
    private readonly ReminderService _reminders;
    private readonly Services.Sync.SyncService _sync;
    private readonly DatabaseService _db;
    private readonly AudioService _audio;
#if WINDOWS
    private Services.AgentApi _agent;
#endif

    // Aggiungiamo i due nuovi servizi al costruttore dell'App
    public App(AudioService audioService, YouTubeService ytService, DatabaseService dbService, HttpClient httpClient, ReminderService reminders, Services.Sync.SyncService sync)
    {
        _reminders = reminders;
        _sync = sync;
        _db = dbService;
        _audio = audioService;
        GmailOrdiniService.Init(dbService);   // 3.0: ordini Amazon da Gmail (solo PC)
        InitializeComponent();

        Loc.Apply();                          // lingua e formati di date/numeri
        Loc.LanguageChanged += () => _ = _reminders.RescheduleAsync();   // le notifiche già programmate cambiano lingua

        // 1-2. Legge e applica il tema salvato (al primo avvio: "Chiaro")
        ThemeService.ApplicaTemaSalvato();

        // 3. Avvia normalmente la pagina principale
        MainPage = new NavigationPage(new Views.MainHostPage(audioService, ytService, dbService, httpClient, sync));

        // Richiede il permesso per le notifiche su Android
        RichiediPermessiNotifiche();
    }

    // Riprogramma le notifiche all'avvio e ogni volta che si torna nell'app
    // e avvia la sincronizzazione Wi-Fi (PC in ascolto, telefono che cerca il PC)
    protected override void OnStart()
    {
        _ = _reminders.RescheduleAsync();
        _sync.Start();
        GmailOrdiniService.Current?.Start();
#if WINDOWS
        // API locale per assistenti esterni (solo 127.0.0.1, con chiave)
        _agent = new Services.AgentApi(_db, _sync, _audio);
        _agent.Start();
#endif
        _ = Services.MusicStorage.MigraBraniAsync(_db);
    }

    protected override void OnResume()
    {
        _ = _reminders.RescheduleAsync();
        if (!_sync.IsHost) _ = _sync.SyncAsync(null);
        // Riaprendo l'app i pulsanti della musica mostrano lo stato reale (sta suonando o no)
        _audio.RefreshPlaybackState();
    }

    private async void RichiediPermessiNotifiche()
    {
#if ANDROID
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
            if (status != PermissionStatus.Granted)
            {
                await Permissions.RequestAsync<Permissions.PostNotifications>();
            }
        }
#endif
    }
}