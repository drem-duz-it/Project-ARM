using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using ARM.Models;

namespace ARM.Services;

// Testo di un brano: righe con i tempi (se il servizio li ha) oppure testo semplice.
public class Lyrics
{
    public bool Trovato { get; set; }
    public bool Strumentale { get; set; }
    public string Semplice { get; set; } = "";
    public List<RigaTesto> Righe { get; set; } = new();
    public long CercatoIl { get; set; }   // tick UTC: un «non trovato» si riprova dopo qualche giorno
}

public class RigaTesto
{
    public double Secondi { get; set; }
    public string Testo { get; set; } = "";
}

// Cerca i testi su LRCLIB (https://lrclib.net): gratuito, senza chiave, con i testi sincronizzati riga per riga.
// Viene mandato solo artista, titolo e durata del brano. Le risposte restano in una cache sul dispositivo.
public class LyricsService
{
    private static readonly HttpClient Http = CreaClient();
    private static readonly TimeSpan RiprovaNonTrovato = TimeSpan.FromDays(3);

    private readonly string _cartella = Path.Combine(FileSystem.AppDataDirectory, "testi");

    private static HttpClient CreaClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("ARM-Music");
        return c;
    }

    public async Task<Lyrics> CercaAsync(Song song, TimeSpan durata, CancellationToken ct = default)
    {
        var file = FileCache(song.Id);
        var salvato = Leggi(file);
        if (salvato != null && (salvato.Trovato || DateTime.UtcNow.Ticks - salvato.CercatoIl < RiprovaNonTrovato.Ticks))
            return salvato;

        var (artista, titolo) = Pulisci(song.Artist, song.Title);
        Lyrics risultato = null;
        bool errore = false;
        try
        {
            risultato = await DaRicercaAsync(artista, titolo, durata, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { errore = true; }   // niente rete o servizio non raggiungibile: non salvare «non trovato»

        if (errore)   // non è «testo assente»: non lo si salva, così al prossimo tentativo si ricerca
            return salvato ?? throw new HttpRequestException("Servizio dei testi non raggiungibile");
        risultato ??= new Lyrics { Trovato = false, CercatoIl = DateTime.UtcNow.Ticks };
        Scrivi(file, risultato);
        return risultato;
    }

    // Il servizio gratuito ogni tanto è lento o risponde 5xx: una seconda possibilità prima di arrendersi
    private static async Task<HttpResponseMessage> RichiediAsync(string url, CancellationToken ct)
    {
        for (int tentativo = 0; ; tentativo++)
        {
            try
            {
                var r = await Http.GetAsync(url, ct);
                if ((int)r.StatusCode < 500 || tentativo >= 2) return r;
                r.Dispose();
            }
            catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !ct.IsCancellationRequested))
            {
                if (tentativo >= 2) throw;
            }
            await Task.Delay(700 * (tentativo + 1), ct);
        }
    }

    private static async Task<Lyrics> DaRicercaAsync(string artista, string titolo, TimeSpan durata, CancellationToken ct)
    {
        bool fallita = false;   // almeno una richiesta è andata male per colpa del servizio (non «testo assente»)

        // 1. Ricerca esatta (se si conosce la durata è la più precisa)
        if (durata.TotalSeconds > 10)
        {
            try
            {
                var url = $"https://lrclib.net/api/get?artist_name={Uri.EscapeDataString(artista)}&track_name={Uri.EscapeDataString(titolo)}&duration={(int)Math.Round(durata.TotalSeconds)}";
                using var r = await RichiediAsync(url, ct);
                if (r.IsSuccessStatusCode)
                {
                    var l = Leggi(JsonDocument.Parse(await r.Content.ReadAsStringAsync(ct)).RootElement);
                    if (l != null) return l;
                }
                else if (r.StatusCode != HttpStatusCode.NotFound) fallita = true;
            }
            catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !ct.IsCancellationRequested)) { fallita = true; }
        }

        // 2. Ricerca larga: sceglie il risultato migliore (sincronizzato, durata vicina)
        foreach (var url in new[]
        {
            $"https://lrclib.net/api/search?artist_name={Uri.EscapeDataString(artista)}&track_name={Uri.EscapeDataString(titolo)}",
            $"https://lrclib.net/api/search?q={Uri.EscapeDataString(artista + " " + titolo)}",
        })
        {
            HttpResponseMessage r;
            try { r = await RichiediAsync(url, ct); }
            catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !ct.IsCancellationRequested)) { fallita = true; continue; }
            using var _ = r;
            if (!r.IsSuccessStatusCode) { fallita = true; continue; }
            using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync(ct));
            Lyrics migliore = null;
            double punteggioMigliore = double.MinValue;
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var l = Leggi(el);
                if (l == null) continue;
                double d = el.TryGetProperty("duration", out var dj) && dj.ValueKind == JsonValueKind.Number ? dj.GetDouble() : 0;
                double scarto = durata.TotalSeconds > 10 && d > 0 ? Math.Abs(d - durata.TotalSeconds) : 0;
                if (durata.TotalSeconds > 10 && scarto > 8) continue;   // altra versione del brano
                double punti = (l.Righe.Count > 0 ? 100 : 0) - scarto;
                if (punti > punteggioMigliore) { punteggioMigliore = punti; migliore = l; }
            }
            if (migliore != null) return migliore;
        }
        if (fallita) throw new HttpRequestException("Servizio dei testi non raggiungibile");   // non è «nessun testo»: non lo si segna come tale
        return null;
    }

    // Una voce di LRCLIB -> Lyrics (null se non contiene testo)
    private static Lyrics Leggi(JsonElement el)
    {
        string Str(string nome) => el.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        bool strumentale = el.TryGetProperty("instrumental", out var i) && i.ValueKind == JsonValueKind.True;
        var sincronizzato = Str("syncedLyrics");
        var semplice = Str("plainLyrics");

        var l = new Lyrics { Trovato = true, Strumentale = strumentale, CercatoIl = DateTime.UtcNow.Ticks };
        if (!string.IsNullOrWhiteSpace(sincronizzato)) l.Righe = ParsaLrc(sincronizzato);
        if (!string.IsNullOrWhiteSpace(semplice)) l.Semplice = semplice.Trim();
        else if (l.Righe.Count > 0) l.Semplice = string.Join("\n", l.Righe.Select(r => r.Testo));

        return strumentale || l.Righe.Count > 0 || l.Semplice.Length > 0 ? l : null;
    }

    private static readonly Regex Tempo = new(@"\[(\d+):(\d+(?:[.,]\d+)?)\]", RegexOptions.Compiled);

    // «[01:23.45] testo» -> righe ordinate per tempo (una riga può avere più tempi)
    private static List<RigaTesto> ParsaLrc(string lrc)
    {
        var righe = new List<RigaTesto>();
        foreach (var linea in lrc.Split('\n'))
        {
            var tempi = Tempo.Matches(linea);
            if (tempi.Count == 0) continue;
            string testo = Tempo.Replace(linea, "").Trim();
            if (testo.Length == 0) continue;
            foreach (Match m in tempi)
            {
                double s = int.Parse(m.Groups[1].Value) * 60 + double.Parse(m.Groups[2].Value.Replace(',', '.'), System.Globalization.CultureInfo.InvariantCulture);
                righe.Add(new RigaTesto { Secondi = s, Testo = testo });
            }
        }
        return righe.OrderBy(r => r.Secondi).ToList();
    }

    private static readonly Regex Rumore = new(
        @"[\(\[][^\)\]]*(official|video|lyric|audio|visuali[sz]er|hd|hq|4k|remaster|clip|testo|ufficiale)[^\)\]]*[\)\]]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // I titoli presi da YouTube hanno «(Official Video)», «Artista - Titolo», «- Topic»...: li ripulisce per la ricerca.
    private static (string artista, string titolo) Pulisci(string artista, string titolo)
    {
        artista = (artista ?? "").Replace(" - Topic", "", StringComparison.OrdinalIgnoreCase).Trim();
        titolo = Rumore.Replace(titolo ?? "", "").Trim();

        // «Artista - Titolo» dentro il titolo
        int trattino = titolo.IndexOf(" - ", StringComparison.Ordinal);
        if (trattino > 0)
        {
            var prima = titolo[..trattino].Trim();
            var dopo = titolo[(trattino + 3)..].Trim();
            int altro = dopo.IndexOf(" - ", StringComparison.Ordinal);   // «Artista - Titolo - Sanremo 2021»: il resto non è il titolo
            if (altro > 0) dopo = dopo[..altro].Trim();
            if (artista.Length == 0 || prima.Contains(artista, StringComparison.OrdinalIgnoreCase) || artista.Contains(prima, StringComparison.OrdinalIgnoreCase))
            {
                if (artista.Length == 0) artista = prima;
                titolo = dopo;
            }
        }

        // solo il primo artista («A, B» / «A feat. B» / «A & B»)
        var primo = Regex.Split(artista, @",|\bfeat\.?\b|\bft\.?\b| & | x ", RegexOptions.IgnoreCase)[0].Trim();
        if (primo.Length > 0) artista = primo;
        titolo = Regex.Replace(titolo, @"\s*(feat\.?|ft\.?)\s.*$", "", RegexOptions.IgnoreCase).Trim();
        return (artista, titolo);
    }

    private string FileCache(string id)
    {
        Directory.CreateDirectory(_cartella);
        var nome = new string((id ?? "x").Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_').ToArray());
        return Path.Combine(_cartella, nome + ".json");
    }

    private static Lyrics Leggi(string file)
    {
        try { return File.Exists(file) ? JsonSerializer.Deserialize<Lyrics>(File.ReadAllText(file)) : null; }
        catch { return null; }
    }

    private static void Scrivi(string file, Lyrics l)
    {
        try { File.WriteAllText(file, JsonSerializer.Serialize(l)); } catch { /* la cache è solo un di più */ }
    }
}
