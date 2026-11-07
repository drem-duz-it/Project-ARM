using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace ARM;

// Traduzioni dell'interfaccia, in stile gettext: nel codice resta il testo italiano, che fa da chiave.
//   Loc.T("Salva")                      -> "Save" se la lingua è l'inglese
//   Loc.F("{0} task in ritardo", n)     -> "{0} overdue tasks" (con la cultura giusta per date e numeri)
// Le traduzioni stanno in Localization/<lingua>.json (incorporate nell'app). Una chiave mancante non dà errore:
// si vede il testo italiano. Per aggiungere una lingua: nuovo file .json + una riga in Languages (vedi docs/LOCALIZATION.md).
public static class Loc
{
    public const string PrefKey = "Language";

    // (codice, nome mostrato nella lingua stessa, cultura per date e numeri)
    public static readonly (string Code, string Name, string Culture)[] Languages =
    {
        ("it", "Italiano", "it-IT"),
        ("en", "English", "en-US"),
    };

    // Avvisa chi deve ricostruire l'interfaccia (vedi App.RicostruisciInterfaccia)
    public static event Action LanguageChanged;

    private static Dictionary<string, string> _table;
    private static string _loaded;

    // Lingua scelta; se non è stata scelta, quella del dispositivo (italiano, altrimenti inglese)
    private static string _language;

    public static string Language
    {
        get
        {
            if (_language != null) return _language;
            var saved = Preferences.Default.Get(PrefKey, "");
            if (Languages.Any(l => l.Code == saved)) return _language = saved;
            var device = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            return _language = Languages.Any(l => l.Code == device) ? device : "en";
        }
    }

    private static CultureInfo _culture;
    private static string _cultureLang;

    public static CultureInfo Culture
    {
        get
        {
            var lang = Language;
            if (_culture == null || _cultureLang != lang)
            {
                _culture = new CultureInfo(Languages.First(l => l.Code == lang).Culture);
                _cultureLang = lang;
            }
            return _culture;
        }
    }

    public static void Apply()
    {
        var c = Culture;
        CultureInfo.DefaultThreadCurrentCulture = c;
        CultureInfo.DefaultThreadCurrentUICulture = c;
        CultureInfo.CurrentCulture = c;
        CultureInfo.CurrentUICulture = c;
    }

    public static void SetLanguage(string code)
    {
        if (code == Language) return;
        Preferences.Default.Set(PrefKey, code);
        _language = code;
        Apply();
        Refresh();
        LanguageChanged?.Invoke();
    }

    public static string T(string italian)
    {
        if (string.IsNullOrEmpty(italian)) return italian;
        var lang = Language;
        if (lang == "it") return italian;
        return Table(lang).TryGetValue(italian, out var s) && !string.IsNullOrEmpty(s) ? s : italian;
    }

    public static string F(string italianFormat, params object[] args) =>
        string.Format(Culture, T(italianFormat), args);

    private static Dictionary<string, string> Table(string lang)
    {
        if (_table != null && _loaded == lang) return _table;
        var table = new Dictionary<string, string>();
        try
        {
            using var stream = typeof(Loc).Assembly.GetManifestResourceStream(lang + ".json");
            if (stream != null)
                table = JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? table;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Traduzioni '{lang}' non caricate: {ex.Message}");
        }
        _loaded = lang;
        return _table = table;
    }

    // ---------- Testi scritti nei file XAML ----------
    // Le pagine XAML hanno i testi fissi nel markup: dopo InitializeComponent() la pagina si registra qui con
    // Loc.Register(this). Il testo italiano originale viene ricordato, così al cambio lingua si può ritradurre.
    private sealed class Original { public string Text, Placeholder, Title, LastText, LastPlaceholder, LastTitle; }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Element, Original> _originals = new();
    private static readonly List<WeakReference<Element>> _roots = new();

    public static void Register(Element root)
    {
        _roots.Add(new WeakReference<Element>(root));
        Walk(root, true);
    }

    public static void Refresh()
    {
        _roots.RemoveAll(r => !r.TryGetTarget(out _));
        foreach (var r in _roots.ToList())
            if (r.TryGetTarget(out var root)) Walk(root, false);
    }

    // initial = true: registrazione della pagina (si ricordano i testi fissi del XAML).
    // initial = false: cambio lingua (si ritraducono SOLO gli elementi già registrati: mai testi arrivati dopo, come i dati dell'utente).
    private static void Walk(Element e, bool initial)
    {
        Original o = null;
        if (initial) o = _originals.GetOrCreateValue(e);
        else _originals.TryGetValue(e, out o);
        if (o != null) switch (e)
        {
            case Label l when !string.IsNullOrEmpty(l.Text) && l.FormattedText == null:
                l.Text = Retranslate(initial, l.Text, ref o.Text, ref o.LastText); break;
            case Button b when !string.IsNullOrEmpty(b.Text):
                b.Text = Retranslate(initial, b.Text, ref o.Text, ref o.LastText); break;
            case Entry en when !string.IsNullOrEmpty(en.Placeholder):
                en.Placeholder = Retranslate(initial, en.Placeholder, ref o.Placeholder, ref o.LastPlaceholder); break;
            case Editor ed when !string.IsNullOrEmpty(ed.Placeholder):
                ed.Placeholder = Retranslate(initial, ed.Placeholder, ref o.Placeholder, ref o.LastPlaceholder); break;
            case SearchBar sb when !string.IsNullOrEmpty(sb.Placeholder):
                sb.Placeholder = Retranslate(initial, sb.Placeholder, ref o.Placeholder, ref o.LastPlaceholder); break;
            case Page pg when !string.IsNullOrEmpty(pg.Title):
                pg.Title = Retranslate(initial, pg.Title, ref o.Title, ref o.LastTitle); break;
        }
        if (e is IVisualTreeElement v)
            foreach (var child in v.GetVisualChildren())
                if (child is Element ce) Walk(ce, initial);
    }

    // original = testo italiano ricordato; last = ultimo testo che abbiamo messo noi.
    // Se nel frattempo il codice ha cambiato il testo (es. titolo del brano), non lo tocchiamo.
    private static string Retranslate(bool initial, string current, ref string original, ref string last)
    {
        if (original == null)
        {
            if (!initial) return current;                            // testo arrivato dopo la registrazione (dati): mai tradurlo
            original = current;
        }                    // prima volta: è l'italiano scritto nel XAML
        else if (current != last) return current;                    // cambiato da altro codice
        if (original.Contains('{')) return current;                  // binding o testo dinamico
        last = T(original);
        return last;
    }
}
