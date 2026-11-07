namespace ARMusic.Services;

// Richieste arrivate da fuori dall'app (widget, notifiche): ad esempio "scrivi" apre la scrittura libera.
// Se la pagina principale non è ancora pronta, la richiesta resta in attesa e viene eseguita appena si apre.
public static class RichiesteEsterne
{
    private static string _pending;
    private static Action<string> _handler;

    public static void Request(string action)
    {
        if (string.IsNullOrEmpty(action)) return;
        if (_handler != null) MainThread.BeginInvokeOnMainThread(() => _handler(action));
        else _pending = action;
    }

    public static void SetHandler(Action<string> handler)
    {
        _handler = handler;
        if (_pending != null)
        {
            var a = _pending;
            _pending = null;
            MainThread.BeginInvokeOnMainThread(() => handler(a));
        }
    }
}
