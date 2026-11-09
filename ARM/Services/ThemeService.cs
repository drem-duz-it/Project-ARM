namespace ARM.Services;

// Gestisce i temi dell'app in un unico punto (avvio + cambio tema)
public static class ThemeService
{
    public static readonly string[] Temi = { "Chiaro", "Classico" };

    // Avvisa le pagine costruite da codice che devono ridisegnarsi con i nuovi colori
    public static event Action ThemeChanged;

    public static string TemaAttuale
    {
        get
        {
            var t = Preferences.Default.Get("AppTheme", "Chiaro");
            return Temi.Contains(t) ? t : "Chiaro";
        }
    }

    public static void ApplicaTemaSalvato()
    {
        // Aggiornamento alla 2.0: si parte una volta dal nuovo tema Chiaro (poi resta quello scelto)
        if (!Preferences.Default.Get("TemaV2", false))
        {
            Preferences.Default.Set("TemaV2", true);
            Preferences.Default.Set("AppTheme", "Chiaro");
        }
        Applica(TemaAttuale);
    }

    public static void Applica(string tema)
    {
        Preferences.Default.Set("AppTheme", tema);

        var dizionari = Application.Current.Resources.MergedDictionaries;
        dizionari.Clear();
        dizionari.Add(tema switch
        {
            "Classico" => new Themes.ClassicTheme(),
            _ => new Themes.LightTheme()
        });

#if ANDROID
        // Barra di stato coerente con il tema
        var activity = Platform.CurrentActivity;
        if (activity?.Window != null)
        {
            var bg = (Color)Application.Current.Resources["BgColor"];
            activity.Window.SetStatusBarColor(Android.Graphics.Color.ParseColor(bg.ToArgbHex()));
            var controller = AndroidX.Core.View.WindowCompat.GetInsetsController(activity.Window, activity.Window.DecorView);
            controller.AppearanceLightStatusBars = tema == "Chiaro";
        }
#endif
    }

    // Passa al tema successivo e restituisce il suo nome
    public static string ProssimoTema()
    {
        int i = Array.IndexOf(Temi, TemaAttuale);
        string nuovo = Temi[(i + 1) % Temi.Length];
        Applica(nuovo);
        ThemeChanged?.Invoke();
        return nuovo;
    }

    // Sceglie un tema preciso (dalle Impostazioni) e ridisegna tutto
    public static void Imposta(string tema)
    {
        if (!Temi.Contains(tema) || tema == TemaAttuale) return;
        Applica(tema);
        ThemeChanged?.Invoke();
    }
}
