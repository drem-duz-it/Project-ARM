using ARM.Data;
using ARM.Models;
#if WINDOWS
using System.Runtime.InteropServices;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
#endif

namespace ARM.Services;

// Ordini in arrivo dalle email di Amazon (3.0).
// Solo il PC legge la posta: Gmail via IMAP con una "password per le app" (non la password vera di Google,
// mai quella di Amazon), salvata cifrata con DPAPI (legata al tuo utente Windows). Legge solo le email che
// arrivano da Amazon, in sola lettura (niente segnato come letto, niente spostato). Gli ordini arrivano su
// telefono e tablet con la sincronizzazione Wi-Fi.
public class GmailOrdiniService
{
    public static GmailOrdiniService Current { get; private set; }

    private readonly DatabaseService _db;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private Timer _timer;

    public event Action Changed;              // stato cambiato (in lettura, errore, aggiornato)

    public static void Init(DatabaseService db) => Current ??= new GmailOrdiniService(db);
    private GmailOrdiniService(DatabaseService db) { _db = db; }

    // Solo su Windows (il PC è l'host della sincronizzazione)
    public static bool Supportato =>
#if WINDOWS
        true;
#else
        false;
#endif

    // I test (database di prova) usano chiavi separate, così non toccano il collegamento vero
    private static string K(string name)
    {
#if DEBUG
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ARMUSIC_DB_PATH"))) return "GmailTest." + name;
#endif
        return "Gmail." + name;
    }

    public string Indirizzo => Preferences.Default.Get(K("Indirizzo"), "");
    public bool Collegato => Supportato && Indirizzo != "" && Preferences.Default.Get(K("Password"), "") != "";
    public bool InLettura => _lock.CurrentCount == 0;
    public string UltimoErrore => Preferences.Default.Get(K("Errore"), "");
    public DateTime? UltimoAggiornamento => Leggi(K("Ultimo"));
    public DateTime? UltimoRiuscito => Leggi(K("UltimoOk"));

    private static DateTime? Leggi(string k)
    {
        var t = Preferences.Default.Get(k, 0L);
        return t == 0 ? null : new DateTime(t, DateTimeKind.Local);
    }

    // Avvia la lettura periodica (all'avvio e ogni 20 minuti)
    public void Start()
    {
        if (!Supportato || _timer != null) return;
        _timer = new Timer(_ => { if (Collegato) _ = AggiornaAsync(); }, null, TimeSpan.FromSeconds(8), TimeSpan.FromMinutes(20));
    }

    // Prova l'accesso e, se va, salva i dati e legge subito la posta. Restituisce null se tutto ok, altrimenti il motivo.
    public async Task<string> CollegaAsync(string indirizzo, string passwordApp)
    {
        indirizzo = (indirizzo ?? "").Trim();
        passwordApp = (passwordApp ?? "").Replace(" ", "").Trim();   // Google la mostra a gruppi di 4
        if (!indirizzo.Contains('@')) return Loc.T("Scrivi l'indirizzo Gmail completo.");
        if (passwordApp.Length < 8) return Loc.T("La password per le app è di 16 lettere.");
#if WINDOWS
        try
        {
            using var client = new ImapClient { Timeout = 20000 };
            await client.ConnectAsync("imap.gmail.com", 993, SecureSocketOptions.SslOnConnect);
            await client.AuthenticateAsync(indirizzo, passwordApp);
            await client.DisconnectAsync(true);
        }
        catch (AuthenticationException) { return Loc.T("Gmail ha rifiutato l'accesso: controlla indirizzo e password per le app."); }
        catch (Exception ex) { return Loc.T("Non riesco a raggiungere Gmail: ") + ex.Message; }

        Preferences.Default.Set(K("Indirizzo"), indirizzo);
        Preferences.Default.Set(K("Password"), Proteggi(passwordApp));
        Preferences.Default.Remove(K("Uid"));
        Preferences.Default.Remove(K("UidValidity"));
        Preferences.Default.Remove(K("Errore"));
        Changed?.Invoke();
        _ = AggiornaAsync();
        return null;
#else
        await Task.CompletedTask;
        return Loc.T("Il collegamento a Gmail si fa da ARM sul PC.");
#endif
    }

    // Scollega: cancella indirizzo e password. Gli ordini già letti restano.
    public void Scollega()
    {
        foreach (var k in new[] { "Indirizzo", "Password", "Uid", "UidValidity", "Errore", "Ultimo", "UltimoOk" })
            Preferences.Default.Remove(K(k));
        Changed?.Invoke();
    }

    // Legge le email nuove di Amazon e aggiorna gli ordini. Restituisce quante email ha letto (-1 = errore).
    public async Task<int> AggiornaAsync()
    {
        if (!Collegato) return 0;
        if (!await _lock.WaitAsync(0)) return 0;
        Changed?.Invoke();
        try
        {
#if WINDOWS
            int n = await LeggiPostaAsync();
            Preferences.Default.Remove(K("Errore"));
            Preferences.Default.Set(K("UltimoOk"), DateTime.Now.Ticks);
            return n;
#else
            await Task.CompletedTask;
            return 0;
#endif
        }
        catch (Exception ex)
        {
            string msg = ex.GetType().Name.Contains("Authentication")
                ? Loc.T("Gmail ha rifiutato la richiesta (accesso scaduto o password per le app revocata). Riprova o ricollega l'account.")
                : Loc.T("Gmail non risponde: ") + ex.Message;
            Preferences.Default.Set(K("Errore"), msg);
            System.Diagnostics.Debug.WriteLine($"GmailOrdini: {ex}");
            return -1;
        }
        finally
        {
            Preferences.Default.Set(K("Ultimo"), DateTime.Now.Ticks);
            _lock.Release();
            Changed?.Invoke();
        }
    }

#if WINDOWS
    private async Task<int> LeggiPostaAsync()
    {
        var password = Svela(Preferences.Default.Get(K("Password"), ""));
        using var client = new ImapClient { Timeout = 30000 };
        await client.ConnectAsync("imap.gmail.com", 993, SecureSocketOptions.SslOnConnect);
        await client.AuthenticateAsync(Indirizzo, password);

        // "Tutti i messaggi" (anche le email archiviate), altrimenti la posta in arrivo
        IMailFolder cartella = null;
        if ((client.Capabilities & (ImapCapabilities.SpecialUse | ImapCapabilities.XList)) != 0)
            try { cartella = client.GetFolder(SpecialFolder.All); } catch { cartella = null; }
        cartella ??= client.Inbox;
        await cartella.OpenAsync(FolderAccess.ReadOnly);

        // Solo le email nuove dall'ultima lettura (UID), al primo giro quelle degli ultimi 60 giorni
        long validity = Preferences.Default.Get(K("UidValidity"), 0L);
        uint ultimoUid = (uint)Preferences.Default.Get(K("Uid"), 0L);
        if (validity != cartella.UidValidity) ultimoUid = 0;

        var query = SearchQuery.FromContains("amazon.").And(SearchQuery.DeliveredAfter(DateTime.Today.AddDays(-60)));
        var uids = (await cartella.SearchAsync(query)).Where(u => u.Id > ultimoUid).OrderBy(u => u.Id).ToList();

        var lette = new List<EmailOrdine>();
        foreach (var uid in uids)
        {
            var msg = await cartella.GetMessageAsync(uid);
            var from = msg.From.Mailboxes.FirstOrDefault()?.Address ?? "";
            // Solo mittenti di Amazon (amazon.it, amazon.com…), non chi cita Amazon nel nome
            if (!System.Text.RegularExpressions.Regex.IsMatch(from, @"@([a-z0-9-]+\.)*amazon\.[a-z.]+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) continue;
            var e = AmazonEmailParser.Parse(msg.Subject, msg.TextBody, msg.HtmlBody, msg.Date.LocalDateTime, msg.MessageId);
            if (e.Utile) lette.Add(e);
        }
        await client.DisconnectAsync(true);

        if (lette.Count > 0)
        {
            var esistenti = await _db.GetAllOrdiniAsync();
            var (cambiati, annullati) = AmazonEmailParser.Unisci(esistenti, lette);
            await _db.SaveOrdiniAsync(cambiati);
            foreach (var o in annullati) await _db.DeleteOrdineAsync(o);
        }

        if (uids.Count > 0) Preferences.Default.Set(K("Uid"), (long)uids.Max(u => u.Id));
        Preferences.Default.Set(K("UidValidity"), (long)cartella.UidValidity);
        return uids.Count;
    }

    // ---- DPAPI (CryptProtectData): la password si può leggere solo con il tuo utente Windows ----
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DATA_BLOB { public int cbData; public IntPtr pbData; }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptProtectData(ref DATA_BLOB pDataIn, string szDataDescr, IntPtr pOptionalEntropy, IntPtr pvReserved, IntPtr pPromptStruct, int dwFlags, ref DATA_BLOB pDataOut);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptUnprotectData(ref DATA_BLOB pDataIn, IntPtr ppszDataDescr, IntPtr pOptionalEntropy, IntPtr pvReserved, IntPtr pPromptStruct, int dwFlags, ref DATA_BLOB pDataOut);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr hMem);

    private static byte[] Dpapi(byte[] data, bool protect)
    {
        var input = new DATA_BLOB { cbData = data.Length, pbData = Marshal.AllocHGlobal(data.Length) };
        var output = new DATA_BLOB();
        try
        {
            Marshal.Copy(data, 0, input.pbData, data.Length);
            bool ok = protect
                ? CryptProtectData(ref input, "ARM Gmail", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, ref output)
                : CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, ref output);
            if (!ok) throw new InvalidOperationException(Loc.T("DPAPI non disponibile"));
            var result = new byte[output.cbData];
            Marshal.Copy(output.pbData, result, 0, output.cbData);
            return result;
        }
        finally
        {
            Marshal.FreeHGlobal(input.pbData);
            if (output.pbData != IntPtr.Zero) LocalFree(output.pbData);
        }
    }

    private static string Proteggi(string s) => Convert.ToBase64String(Dpapi(System.Text.Encoding.UTF8.GetBytes(s), true));
    private static string Svela(string s) => System.Text.Encoding.UTF8.GetString(Dpapi(Convert.FromBase64String(s), false));
#endif
}
