using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Videos.Streams;
using ARM.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ARM.Services
{
    public class YouTubeService
    {
        private readonly YoutubeClient _youtube;

        public YouTubeService()
        {
            // 1. Creiamo un HttpClient personalizzato
            var httpClient = new HttpClient();

            // 2. Gli mettiamo la "maschera" di Google Chrome per Windows
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

            // 3. Inizializziamo YoutubeExplode passandogli il nostro client truccato
            _youtube = new YoutubeClient(httpClient);
        }

        // Messaggio comprensibile quando YouTube non fornisce l'audio
        public static string SpiegaErrore(Exception ex)
        {
            if (ex is YoutubeExplode.Exceptions.YoutubeExplodeException && ex.Message.Contains("cipher", StringComparison.OrdinalIgnoreCase))
                return Loc.T("YouTube ha cambiato il modo in cui fornisce l'audio e al momento non è possibile riprodurre o scaricare i brani. " +
                       "Si risolve aggiornando ARM appena esce la correzione della libreria YoutubeExplode.");
            if (ex is HttpRequestException)
                return Loc.T("Connessione a YouTube non riuscita. Controlla la rete e riprova.");
            return ex.Message;
        }

        // 1. Ricerca dei brani come su YouTube
        public async Task<List<Song>> SearchSongsAsync(string query)
        {
            // Ottiene i primi 15 risultati della ricerca
            var searchResults = await _youtube.Search.GetVideosAsync(query).CollectAsync(15);
            var songs = new List<Song>();

            foreach (var video in searchResults)
            {
                songs.Add(new Song
                {
                    Id = video.Id.Value,
                    Title = video.Title,
                    Artist = video.Author.ChannelTitle, // Usa il nome del canale come Artista
                    ThumbnailUrl = video.Thumbnails.GetWithHighestResolution().Url
                });
            }

            return songs;
        }

        // 2. Download del file audio (l'ascolto avviene solo da file scaricato)
        // Metodo per scaricare direttamente il file superando il blocco 403
        public async Task DownloadAudioAsync(string videoId, string filePath)
        {
            var streamManifest = await _youtube.Videos.Streams.GetManifestAsync(videoId);
            var streamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();

            if (streamInfo != null)
            {
                // Usa il downloader interno di YoutubeExplode!
                await _youtube.Videos.Streams.DownloadAsync(streamInfo, filePath);
            }
        }
    }
}