using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace ARM.Services;

// Copertine delle playlist ridotte per gli elenchi.
// Le foto scelte dalla galleria sono spesso di diversi MB: decodificarle a ogni riga rallenta lo scorrimento.
// La versione ridotta (lato massimo 384 px) sta nella cache dell'app e si crea una volta sola;
// l'originale resta com'è (è quello che passa agli altri dispositivi).
public static class CoverThumbnails
{
    private const int MaxSize = 384;
    private static readonly ConcurrentDictionary<string, string> _cache = new();

    // Percorso da mostrare: la versione ridotta se c'è (o si riesce a creare), altrimenti l'originale. null = nessuna copertina.
    public static string Get(string original)
    {
        if (string.IsNullOrEmpty(original)) return null;
        if (_cache.TryGetValue(original, out var cached)) return cached;
        if (!File.Exists(original)) return null;   // non in cache: potrebbe arrivare più tardi (sincronizzazione)

        var result = original;
#if ANDROID
        try
        {
            var thumb = ThumbPath(original);
            if (File.Exists(thumb) && File.GetLastWriteTimeUtc(thumb) >= File.GetLastWriteTimeUtc(original)) result = thumb;
            else if (CreaAndroid(original, thumb)) result = thumb;
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Copertina ridotta: {ex.Message}"); }
#endif
        _cache[original] = result;
        return result;
    }

    // Prepara le copertine fuori dal thread grafico, prima di mostrare un elenco
    public static Task PrepareAsync(IEnumerable<string> originals) =>
        Task.Run(() => { foreach (var p in originals.Where(p => !string.IsNullOrEmpty(p)).Distinct()) Get(p); });

    private static string ThumbPath(string original)
    {
        var hash = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(original)));
        return Path.Combine(FileSystem.CacheDirectory, "cover-thumbs", hash + ".jpg");
    }

#if ANDROID
    private static bool CreaAndroid(string original, string thumb)
    {
        // 1. Solo le dimensioni, per decodificare già ridotta (InSampleSize) senza caricare la foto intera
        var bounds = new Android.Graphics.BitmapFactory.Options { InJustDecodeBounds = true };
        Android.Graphics.BitmapFactory.DecodeFile(original, bounds);
        int lato = Math.Max(bounds.OutWidth, bounds.OutHeight);
        if (lato <= 0) return false;
        int sample = 1;
        while (lato / (sample * 2) >= MaxSize) sample *= 2;

        using var bmp = Android.Graphics.BitmapFactory.DecodeFile(original, new Android.Graphics.BitmapFactory.Options { InSampleSize = sample });
        if (bmp == null) return false;

        // 2. Scala al lato massimo e applica la rotazione delle foto scattate col telefono (EXIF)
        float scala = Math.Min(1f, (float)MaxSize / Math.Max(bmp.Width, bmp.Height));
        var matrice = new Android.Graphics.Matrix();
        matrice.PostScale(scala, scala);
        int rotazione = new Android.Media.ExifInterface(original).GetAttributeInt(Android.Media.ExifInterface.TagOrientation, 1) switch
        {
            3 => 180,
            6 => 90,
            8 => 270,
            _ => 0
        };
        if (rotazione != 0) matrice.PostRotate(rotazione);
        using var ridotta = Android.Graphics.Bitmap.CreateBitmap(bmp, 0, 0, bmp.Width, bmp.Height, matrice, true);

        // 3. Salva (prima in un file temporaneo, così una copertina a metà non viene mai mostrata)
        Directory.CreateDirectory(Path.GetDirectoryName(thumb));
        var temp = thumb + ".tmp";
        using (var fs = File.Create(temp))
            ridotta.Compress(Android.Graphics.Bitmap.CompressFormat.Jpeg, 85, fs);
        File.Move(temp, thumb, overwrite: true);
        return true;
    }
#endif
}
