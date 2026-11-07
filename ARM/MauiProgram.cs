using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;

namespace ARM
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .UseMauiCommunityToolkitMediaElement(true)
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    // Font di ARM 2.5 (Plus Jakarta Sans, licenza OFL): un file per peso
                    fonts.AddFont("PlusJakartaSans-Medium.ttf", "JakartaMedium");
                    fonts.AddFont("PlusJakartaSans-SemiBold.ttf", "JakartaSemiBold");
                    fonts.AddFont("PlusJakartaSans-Bold.ttf", "JakartaBold");
                    fonts.AddFont("PlusJakartaSans-ExtraBold.ttf", "JakartaExtraBold");
                });

            // Campi di testo senza la cornice/linea di sistema: il loro aspetto lo dà il contenitore (Ui.Field)
            Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("SenzaBordo", (h, v) => TogliBordo(h.PlatformView));
            Microsoft.Maui.Handlers.EditorHandler.Mapper.AppendToMapping("SenzaBordo", (h, v) => TogliBordo(h.PlatformView));

            // --- REGISTRAZIONI GLOBALI (Devono funzionare sia in Debug che in Release!) ---
            builder.Services.AddSingleton<ARM.Services.YouTubeService>();
            builder.Services.AddSingleton<ARM.Services.AudioService>();
            builder.Services.AddSingleton<ARM.Data.DatabaseService>();
            builder.Services.AddSingleton<ARM.Services.ReminderService>();
            builder.Services.AddSingleton<ARM.Services.Sync.SyncService>();
            builder.Services.AddSingleton<ARM.Services.SpotifyService>();
            builder.Services.AddSingleton<ARM.Services.LyricsService>();
            builder.Services.AddSingleton<HttpClient>();

            builder.Services.AddSingleton<App>();
            builder.Services.AddSingleton<ARM.Views.MainHostPage>();

            builder.Services.AddTransient<ARM.Views.SearchPage>();
            builder.Services.AddTransient<ARM.Views.PlayerPage>();

            // --- LOGGING (Solo in Debug) ---
#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }

        private static void TogliBordo(object view)
        {
#if WINDOWS
            if (view is Microsoft.UI.Xaml.Controls.TextBox tb)
            {
                tb.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
                tb.Padding = new Microsoft.UI.Xaml.Thickness(0, 6, 0, 6);
                var transparent = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
                // Anche negli stati "passaggio del mouse" e "attivo" il fondo resta quello del contenitore
                foreach (var key in new[] { "TextControlBackground", "TextControlBackgroundPointerOver", "TextControlBackgroundFocused",
                                            "TextControlBorderBrush", "TextControlBorderBrushPointerOver", "TextControlBorderBrushFocused" })
                    tb.Resources[key] = transparent;
            }
#elif ANDROID
            if (view is Android.Widget.EditText et)
            {
                et.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
                et.SetPadding(0, et.PaddingTop, 0, et.PaddingBottom);
            }
#endif
        }
    }
}
