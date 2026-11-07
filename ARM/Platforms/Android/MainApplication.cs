using Android.App;
using Android.Runtime;

namespace ARM
{
    [Application]
    public class MainApplication : MauiApplication
    {
        public MainApplication(IntPtr handle, JniHandleOwnership ownership)
            : base(handle, ownership)
        {
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

        public override void OnCreate()
        {
            base.OnCreate();

            // Ogni modifica ai dati (dall'app, dalla sincronizzazione, da una notifica) ridisegna il widget sulla home.
            // Le modifiche ravvicinate si raggruppano in un solo aggiornamento.
            try
            {
                var db = IPlatformApplication.Current?.Services.GetService<Data.DatabaseService>();
                if (db != null)
                {
                    CancellationTokenSource debounce = null;
                    db.DataChanged += () =>
                    {
                        debounce?.Cancel();
                        var cts = debounce = new CancellationTokenSource();
                        _ = Task.Delay(400, cts.Token).ContinueWith(t =>
                        {
                            if (!t.IsCanceled) Platforms.Android.OggiWidget.UpdateAll(this);
                        });
                    };
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Widget: {ex}"); }
        }
    }
}
