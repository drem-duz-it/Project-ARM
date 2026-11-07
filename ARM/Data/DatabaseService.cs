using SQLite;
using ARM.Models;
using System.IO;

namespace ARM.Data
{
    public class DatabaseService
    {
        private SQLiteAsyncConnection _database;

        public DatabaseService()
        {
            // Qui stiamo decidendo dove salvare il singolo file del database.
            // FileSystem.AppDataDirectory punta in automatico a una cartella sicura e isolata sia su Windows che su Android.
            string dbPath = Path.Combine(FileSystem.AppDataDirectory, "armusic.db3");
#if DEBUG
            // Solo in debug: permette di provare l'app con un database di prova separato
            var dbDiProva = Environment.GetEnvironmentVariable("ARMUSIC_DB_PATH");
            if (!string.IsNullOrEmpty(dbDiProva)) dbPath = dbDiProva;
#endif

            _database = new SQLiteAsyncConnection(dbPath);

            // Inizializza il file creando le tabelle se è la prima volta che apri l'app
            _database.CreateTableAsync<Song>().Wait();
            _database.CreateTableAsync<Playlist>().Wait();
            _database.CreateTableAsync<PlaylistSong>().Wait();

            // Tabelle dello spazio di lavoro
            _database.CreateTableAsync<Note>().Wait();
            _database.CreateTableAsync<TaskItem>().Wait();
            _database.CreateTableAsync<CalendarEvent>().Wait();
            _database.CreateTableAsync<Ordine>().Wait();         // 3.0: ordini in arrivo (da Gmail)

            // Elementi eliminati, da comunicare all'altro dispositivo
            _database.CreateTableAsync<SyncTombstone>().Wait();

            // Copertine create prima della 2.1.1: assegna loro un id per poterle sincronizzare
            Task.Run(AssegnaIdCopertineAsync).Wait();   // Task.Run: evita il blocco del thread grafico
        }

        // Avvisa chi è interessato (notifiche, sincronizzazione) che i dati sono cambiati
        public event Action DataChanged;

        // Istante "adesso" usato per segnare le modifiche locali (tick UTC)
        public static long Now => DateTime.UtcNow.Ticks;

        private async Task Changed(Task operation)
        {
            await operation;
            DataChanged?.Invoke();
        }

        private Task Tombstone(string table, string id) =>
            _database.InsertOrReplaceAsync(new SyncTombstone { Key = $"{table}:{id}", Table = table, ItemId = id, DeletedAt = Now });

        // ---------- NOTE ----------
        public Task<List<Note>> GetAllNotesAsync() => _database.Table<Note>().ToListAsync();

        public Task SaveNoteAsync(Note note)
        {
            note.UpdatedAt = DateTime.Now;
            note.ChangedAt = Now;
            return Changed(_database.InsertOrReplaceAsync(note));
        }

        public async Task DeleteNoteAsync(Note note)
        {
            await _database.DeleteAsync(note);
            await Changed(Tombstone(nameof(Note), note.Id));
        }

        // Segna un pagamento come pagato. Se è mensile, lo sposta direttamente al mese successivo.
        public async Task TogglePaymentAsync(Note note)
        {
            if (note.IsMonthly && !note.IsPaid)
            {
                note.LastPaidAt = DateTime.Now;
                note.DueDate = (note.DueDate ?? DateTime.Today).AddMonths(1);
            }
            else
            {
                note.IsPaid = !note.IsPaid;
                note.LastPaidAt = note.IsPaid ? DateTime.Now : null;
            }
            await SaveNoteAsync(note);
        }

        // Spunta / toglie la spunta a una voce della lista dentro una nota
        public Task ToggleChecklistItemAsync(Note note, int index)
        {
            var items = note.ChecklistItems;
            if (index < 0 || index >= items.Count) return Task.CompletedTask;
            items[index].Done = !items[index].Done;
            note.Checklist = ChecklistItem.Serialize(items);
            return SaveNoteAsync(note);
        }

        // ---------- TASK ----------
        public Task<List<TaskItem>> GetAllTasksAsync() => _database.Table<TaskItem>().ToListAsync();

        public Task SaveTaskAsync(TaskItem task)
        {
            task.ChangedAt = Now;
            return Changed(_database.InsertOrReplaceAsync(task));
        }

        public async Task DeleteTaskAsync(TaskItem task)
        {
            await _database.DeleteAsync(task);
            await Changed(Tombstone(nameof(TaskItem), task.Id));
        }

        public Task ToggleTaskAsync(TaskItem task)
        {
            task.IsDone = !task.IsDone;
            task.CompletedAt = task.IsDone ? DateTime.Now : null;
            return SaveTaskAsync(task);
        }

        // ---------- EVENTI ----------
        public Task<List<CalendarEvent>> GetAllEventsAsync() => _database.Table<CalendarEvent>().ToListAsync();

        public Task SaveEventAsync(CalendarEvent ev)
        {
            ev.ChangedAt = Now;
            return Changed(_database.InsertOrReplaceAsync(ev));
        }

        public async Task DeleteEventAsync(CalendarEvent ev)
        {
            await _database.DeleteAsync(ev);
            await Changed(Tombstone(nameof(CalendarEvent), ev.Id));
        }

        // ---------- ORDINI (3.0) ----------
        public Task<List<Ordine>> GetAllOrdiniAsync() => _database.Table<Ordine>().ToListAsync();

        public Task SaveOrdineAsync(Ordine o)
        {
            o.ChangedAt = Now;
            return Changed(_database.InsertOrReplaceAsync(o));
        }

        // Salva più ordini insieme (lettura della posta) con un solo avviso di "dati cambiati"
        public async Task SaveOrdiniAsync(IEnumerable<Ordine> ordini)
        {
            bool any = false;
            foreach (var o in ordini) { o.ChangedAt = Now; await _database.InsertOrReplaceAsync(o); any = true; }
            if (any) DataChanged?.Invoke();
        }

        public async Task DeleteOrdineAsync(Ordine o)
        {
            await _database.DeleteAsync(o);
            await Changed(Tombstone(nameof(Ordine), o.Id));
        }

        // Segnato a mano come ricevuto (dalla dashboard o dalla notifica)
        public Task SegnaConsegnatoAsync(Ordine o)
        {
            o.Stato = StatoSpedizione.Consegnato;
            o.ConsegnatoIl = DateTime.Now;
            o.Aggiornato = DateTime.Now;
            return SaveOrdineAsync(o);
        }

        // ---------- MUSICA ----------
        // Esempio: Metodo in C# per salvare una canzone senza sapere il codice SQL!
        public Task SaveSongAsync(Song song)
        {
            song.ChangedAt = Now;
            return Changed(_database.InsertOrReplaceAsync(song));
        }

        // Esempio: Metodo per prendere tutte le canzoni
        public async Task<List<Song>> GetAllSongsAsync()
        {
            return await _database.Table<Song>().ToListAsync();
        }

        // Aggiorna solo il percorso del file su questo dispositivo (non è un dato da sincronizzare)
        public async Task SetSongFileAsync(string songId, string path)
        {
            var song = await _database.FindAsync<Song>(songId);
            if (song == null) return;
            song.LocalFilePath = path;
            await _database.UpdateAsync(song);
        }

        // Dove sta la copertina con un certo id su questo dispositivo
        public static string CoverPath(string imageId) => Path.Combine(FileSystem.AppDataDirectory, "covers", $"{imageId}.jpg");

        private async Task AssegnaIdCopertineAsync()
        {
            foreach (var pl in await _database.Table<Playlist>().ToListAsync())
            {
                if (!string.IsNullOrEmpty(pl.ImageId) || string.IsNullOrEmpty(pl.ImagePath) || !File.Exists(pl.ImagePath)) continue;
                pl.ImageId = Guid.NewGuid().ToString("N");
                pl.ChangedAt = Now;
                await _database.UpdateAsync(pl);
            }
        }

        // Copertina ricevuta da un altro dispositivo: collega il file a tutte le playlist che la usano
        public async Task SetCoverFileAsync(string imageId, string path)
        {
            foreach (var pl in (await _database.Table<Playlist>().ToListAsync()).Where(p => p.ImageId == imageId))
            {
                pl.ImagePath = path;
                await _database.UpdateAsync(pl);
            }
        }

        // Salva o aggiorna una playlist
        public Task SavePlaylistAsync(Playlist playlist)
        {
            playlist.ChangedAt = Now;
            return Changed(_database.InsertOrReplaceAsync(playlist));
        }

        // Ottieni tutte le playlist create
        public async Task<List<Playlist>> GetAllPlaylistsAsync()
        {
            return await _database.Table<Playlist>().ToListAsync();
        }

        // Associa un brano a una playlist specifica
        public Task AddSongToPlaylistAsync(string playlistId, string songId)
        {
            var playlistSong = new PlaylistSong
            {
                PlaylistId = playlistId,
                SongId = songId,
                ChangedAt = Now
            };
            return Changed(_database.InsertAsync(playlistSong));
        }

        // Recupera tutti i brani contenuti in una playlist
        public async Task<List<Song>> GetSongsForPlaylistAsync(string playlistId)
        {
            // Cerca tutti i collegamenti per l'ID della playlist
            var links = await _database.Table<PlaylistSong>()
                                     .Where(ps => ps.PlaylistId == playlistId)
                                     .ToListAsync();

            var songs = new List<Song>();
            foreach (var link in links)
            {
                // Recupera i dettagli di ogni singolo brano
                var song = await _database.Table<Song>()
                                 .Where(s => s.Id == link.SongId)
                                 .FirstOrDefaultAsync();
                if (song != null)
                {
                    songs.Add(song);
                }
            }
            return songs;
        }

        public async Task DeletePlaylistAsync(Playlist playlist)
        {
            // 1. Elimina la playlist
            await _database.DeleteAsync(playlist);

            // 2. Trova e distruggi tutti i collegamenti (PlaylistSong) associati
            await DeleteLinksOfPlaylist(playlist.Id);

            await Changed(Tombstone(nameof(Playlist), playlist.Id));
        }

        private async Task DeleteLinksOfPlaylist(string playlistId)
        {
            var links = await _database.Table<PlaylistSong>()
                                     .Where(ps => ps.PlaylistId == playlistId)
                                     .ToListAsync();
            foreach (var link in links)
            {
                await _database.DeleteAsync(link);
            }
        }

        // Elimina il collegamento tra la playlist e la canzone
        public async Task RemoveSongFromPlaylistAsync(string songId, string playlistId)
        {
            var mapping = await _database.Table<PlaylistSong>()
                                         .Where(x => x.SongId == songId && x.PlaylistId == playlistId)
                                         .FirstOrDefaultAsync();
            if (mapping != null)
            {
                await _database.DeleteAsync(mapping);
                await Changed(Tombstone(nameof(PlaylistSong), LinkKey(mapping)));
            }
        }

        // Salva le modifiche globali (nome/artista) di un brano
        public Task UpdateSongAsync(Song song)
        {
            song.ChangedAt = Now;
            return Changed(_database.UpdateAsync(song));
        }

        // ================= SINCRONIZZAZIONE =================

        // Un collegamento playlist-brano si riconosce dalla coppia di id (l'Id numerico cambia tra dispositivi)
        public static string LinkKey(PlaylistSong l) => $"{l.PlaylistId}|{l.SongId}";

        // Tutto ciò che è cambiato su questo dispositivo dopo "since" (null = tutto, per la prima sincronizzazione)
        public async Task<SyncPayload> GetChangesAsync(long? since)
        {
            bool all = since == null;
            long s = since ?? 0;
            bool IsNew(long changedAt) => all || changedAt > s;

            var payload = new SyncPayload
            {
                Notes = (await GetAllNotesAsync()).Where(x => IsNew(x.ChangedAt)).ToList(),
                Tasks = (await GetAllTasksAsync()).Where(x => IsNew(x.ChangedAt)).ToList(),
                Events = (await GetAllEventsAsync()).Where(x => IsNew(x.ChangedAt)).ToList(),
                Ordini = (await GetAllOrdiniAsync()).Where(x => IsNew(x.ChangedAt)).ToList(),
                Playlists = (await GetAllPlaylistsAsync()).Where(x => IsNew(x.ChangedAt)).ToList(),
                Songs = (await GetAllSongsAsync()).Where(x => IsNew(x.ChangedAt)).ToList(),
                Links = (await _database.Table<PlaylistSong>().ToListAsync()).Where(x => IsNew(x.ChangedAt)).ToList(),
                Deletions = all ? new() : (await _database.Table<SyncTombstone>().ToListAsync()).Where(x => x.DeletedAt > s).ToList()
            };

            // Percorsi di file e copertine valgono solo su questo dispositivo
            foreach (var song in payload.Songs) song.LocalFilePath = null;
            foreach (var pl in payload.Playlists) pl.ImagePath = null;
            return payload;
        }

        // Applica le modifiche arrivate da un altro dispositivo.
        // propagate = false (dispositivo Android): non sono modifiche locali, non vanno rimandate indietro.
        // propagate = true (PC): vanno passate anche agli ALTRI dispositivi associati, quindi si segnano come modifiche.
        public async Task ApplyAsync(SyncPayload p, bool propagate = false)
        {
            long stamp = propagate ? Now : 0;
            foreach (var n in p.Notes) { n.ChangedAt = stamp; await _database.InsertOrReplaceAsync(n); }
            foreach (var t in p.Tasks) { t.ChangedAt = stamp; await _database.InsertOrReplaceAsync(t); }
            foreach (var e in p.Events) { e.ChangedAt = stamp; await _database.InsertOrReplaceAsync(e); }
            foreach (var o in p.Ordini ?? new()) { o.ChangedAt = stamp; await _database.InsertOrReplaceAsync(o); }

            foreach (var pl in p.Playlists)
            {
                // La copertina resta quella locale se è la stessa immagine, altrimenti arriva col trasferimento dei file
                var existing = await _database.FindAsync<Playlist>(pl.Id);
                if (existing != null && existing.ImageId == pl.ImageId) pl.ImagePath = existing.ImagePath;
                else pl.ImagePath = !string.IsNullOrEmpty(pl.ImageId) && File.Exists(CoverPath(pl.ImageId)) ? CoverPath(pl.ImageId) : null;
                pl.ChangedAt = stamp;
                await _database.InsertOrReplaceAsync(pl);
            }

            foreach (var song in p.Songs)
            {
                var existing = await _database.FindAsync<Song>(song.Id);
                song.LocalFilePath = existing?.LocalFilePath;
                song.ChangedAt = stamp;
                await _database.InsertOrReplaceAsync(song);
            }

            foreach (var link in p.Links)
            {
                var exists = await _database.Table<PlaylistSong>()
                                            .Where(x => x.PlaylistId == link.PlaylistId && x.SongId == link.SongId)
                                            .FirstOrDefaultAsync();
                if (exists == null)
                    await _database.InsertAsync(new PlaylistSong { PlaylistId = link.PlaylistId, SongId = link.SongId, ChangedAt = stamp });
            }

            foreach (var d in p.Deletions)
            {
                // Sul PC l'eliminazione va ricordata per comunicarla anche agli altri dispositivi
                if (propagate) await Tombstone(d.Table, d.ItemId);
                switch (d.Table)
                {
                    case nameof(Note): await _database.DeleteAsync<Note>(d.ItemId); break;
                    case nameof(TaskItem): await _database.DeleteAsync<TaskItem>(d.ItemId); break;
                    case nameof(CalendarEvent): await _database.DeleteAsync<CalendarEvent>(d.ItemId); break;
                    case nameof(Ordine): await _database.DeleteAsync<Ordine>(d.ItemId); break;
                    case nameof(Playlist):
                        await _database.DeleteAsync<Playlist>(d.ItemId);
                        await DeleteLinksOfPlaylist(d.ItemId);
                        break;
                    case nameof(PlaylistSong):
                        var parts = d.ItemId.Split('|');
                        if (parts.Length == 2)
                        {
                            var links = await _database.Table<PlaylistSong>()
                                                       .Where(x => x.PlaylistId == parts[0] && x.SongId == parts[1])
                                                       .ToListAsync();
                            foreach (var l in links) await _database.DeleteAsync(l);
                        }
                        break;
                }
            }

            if (p.Count > 0) DataChanged?.Invoke();
        }
    }
}
