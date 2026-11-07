namespace ARMusic;

// Versione minima di Loc per il test da riga di comando: restituisce il testo italiano così com'è.
public static class Loc
{
    public static string T(string s) => s;
    public static string F(string s, params object[] a) => string.Format(s, a);
}
