using ARMusic.Data;
using ARMusic.Models;

namespace ARMusic.Services;

// Dove finiscono i brani scaricati:
// - telefono: cartella Musica/ARM (visibile alle altre app, resta anche disinstallando ARM)
// - PC: cartella dati dell'app, come prima
public static class MusicStorage
{
    // Cartella temporanea dove scaricare prima di spostare il brano al suo posto
    public static string TempPath(string songId) => Path.Combine(FileSystem.CacheDirectory, $"{songId}.m4a");

    // Sposta il file appena scaricato/ricevuto nella sua posizione definitiva e ne restituisce il percorso
    public static async Task<string> PublishAsync(string sourcePath, Song song)
    {
#if ANDROID
        try
        {
            var published = await PublishAndroidAsync(sourcePath, song);
            if (published != null) return published;
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Cartella Musica non disponibile: {ex.Message}"); }
#endif
        // PC (o ripiego): cartella dati dell'app
        var dest = Path.Combine(FileSystem.AppDataDirectory, $"{song.Id}.m4a");
        if (!string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(dest), StringComparison.OrdinalIgnoreCase))
        {
            await Task.Run(() => File.Move(sourcePath, dest, overwrite: true));
        }
        return dest;
    }

    // Una sola volta: sposta nella cartella Musica i brani già scaricati nella cartella privata
    public static async Task MigraBraniAsync(DatabaseService db)
    {
#if ANDROID
        if (Preferences.Default.Get("BraniInCartellaMusica", false)) return;
        try
        {
            foreach (var song in await db.GetAllSongsAsync())
            {
                var path = song.LocalFilePath;
                if (string.IsNullOrEmpty(path) || !File.Exists(path) || !path.StartsWith(FileSystem.AppDataDirectory)) continue;
                var nuovo = await PublishAndroidAsync(path, song);
                if (nuovo != null) await db.SetSongFileAsync(song.Id, nuovo);
            }
            Preferences.Default.Set("BraniInCartellaMusica", true);
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Spostamento brani: {ex.Message}"); }
#else
        await Task.CompletedTask;
#endif
    }

    // Nome leggibile: "Artista - Titolo.m4a"
    private static string FileName(Song song)
    {
        // Se il titolo contiene già l'artista (succede spesso su YouTube) non lo ripetiamo
        var name = string.IsNullOrWhiteSpace(song.Artist) || (song.Title ?? "").Contains(song.Artist, StringComparison.OrdinalIgnoreCase)
            ? song.Title
            : $"{song.Artist} - {song.Title}";
        if (string.IsNullOrWhiteSpace(name)) name = song.Id;
        foreach (var c in Path.GetInvalidFileNameChars().Concat(new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|' }))
            name = name.Replace(c, '_');
        return (name.Length > 100 ? name[..100] : name).Trim() + ".m4a";
    }

#if ANDROID
    private static async Task<string> PublishAndroidAsync(string sourcePath, Song song)
    {
        var ctx = Android.App.Application.Context;
        var resolver = ctx.ContentResolver;

        if (OperatingSystem.IsAndroidVersionAtLeast(29))
        {
            // Android 10+: si passa dal MediaStore, nessun permesso necessario
            var values = new Android.Content.ContentValues();
            values.Put(Android.Provider.MediaStore.IMediaColumns.DisplayName, FileName(song));
            values.Put(Android.Provider.MediaStore.IMediaColumns.MimeType, "audio/mp4");
            values.Put(Android.Provider.MediaStore.IMediaColumns.RelativePath, "Music/ARM");
            values.Put(Android.Provider.MediaStore.IMediaColumns.Title, song.Title);
            values.Put(Android.Provider.MediaStore.Audio.IAudioColumns.Artist, song.Artist);
            values.Put(Android.Provider.MediaStore.IMediaColumns.IsPending, 1);

            var collection = Android.Provider.MediaStore.Audio.Media.GetContentUri(Android.Provider.MediaStore.VolumeExternalPrimary);
            var uri = resolver.Insert(collection, values);
            if (uri == null) return null;

            using (var output = resolver.OpenOutputStream(uri))
            using (var input = File.OpenRead(sourcePath))
                await input.CopyToAsync(output);

            values.Clear();
            values.Put(Android.Provider.MediaStore.IMediaColumns.IsPending, 0);
            resolver.Update(uri, values, null, null);

            // Percorso reale del file, usato dal lettore
            string path = null;
            using (var cursor = resolver.Query(uri, new[] { Android.Provider.MediaStore.IMediaColumns.Data }, null, null, null))
            {
                if (cursor != null && cursor.MoveToFirst()) path = cursor.GetString(0);
            }
            if (path == null || !File.Exists(path)) return null;

            File.Delete(sourcePath);
            return path;
        }
        else
        {
            // Android 8-9: serve il permesso di scrittura
            var status = await MainThread.InvokeOnMainThreadAsync(() => Permissions.RequestAsync<Permissions.StorageWrite>());
            if (status != PermissionStatus.Granted) return null;

            var dir = Path.Combine(Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryMusic).AbsolutePath, "ARM");
            Directory.CreateDirectory(dir);
            var dest = Path.Combine(dir, FileName(song));
            if (File.Exists(dest)) dest = Path.Combine(dir, Path.GetFileNameWithoutExtension(dest) + $" ({song.Id}).m4a");
            File.Move(sourcePath, dest);
            Android.Media.MediaScannerConnection.ScanFile(ctx, new[] { dest }, null, null);
            return dest;
        }
    }
#endif
}
