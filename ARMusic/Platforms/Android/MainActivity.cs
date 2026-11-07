using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace ARMusic;

// SingleTop: toccando il widget con ARM già aperto arriva OnNewIntent, senza creare una seconda copia dell'app
[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, Exported = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Forza i tasti del volume del telefono a controllare la musica, non la suoneria!
        VolumeControlStream = Android.Media.Stream.Music;

        HandleIntent(Intent);
    }

    protected override void OnNewIntent(Intent intent)
    {
        base.OnNewIntent(intent);
        HandleIntent(intent);
    }

    // Richieste dal widget (es. "scrivi" = apri la scrittura libera)
    private static void HandleIntent(Intent intent)
    {
        var azione = intent?.GetStringExtra(Platforms.Android.OggiWidget.ExtraAzione);
        if (string.IsNullOrEmpty(azione)) return;
        intent.RemoveExtra(Platforms.Android.OggiWidget.ExtraAzione);   // non ripeterla se l'activity viene ricreata
        Services.RichiesteEsterne.Request(azione);
    }
}
