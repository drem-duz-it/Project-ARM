using Android.Content;
using Android.Media;

namespace ARM.Services;

// Android: pausa quando si staccano le cuffie o si disconnette il Bluetooth.
// - ACTION_AUDIO_BECOMING_NOISY: lo manda il sistema prima di passare l'audio all'altoparlante
// - AudioDeviceCallback: riserva per i dispositivi che non lo mandano, e segnala i collegamenti
public partial class AudioService
{
    private RicevitoreUscita _ricevitore;
    private ControlloDispositivi _dispositivi;

    partial void AvviaControlloUscitaAudio()
    {
        if (_ricevitore != null) return;
        try
        {
            var ctx = global::Android.App.Application.Context;
            _ricevitore = new RicevitoreUscita(this);
            var filtro = new IntentFilter(AudioManager.ActionAudioBecomingNoisy);
            if (OperatingSystem.IsAndroidVersionAtLeast(33)) ctx.RegisterReceiver(_ricevitore, filtro, ReceiverFlags.NotExported);
            else ctx.RegisterReceiver(_ricevitore, filtro);

            if (ctx.GetSystemService(Context.AudioService) is AudioManager audio)
            {
                _dispositivi = new ControlloDispositivi(this);
                audio.RegisterAudioDeviceCallback(_dispositivi, null);
            }
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Controllo uscita audio: {ex.Message}"); }
    }

    private class RicevitoreUscita : BroadcastReceiver
    {
        private readonly AudioService _audio;
        public RicevitoreUscita(AudioService audio) => _audio = audio;

        public override void OnReceive(Context context, Intent intent)
        {
            if (intent?.Action == AudioManager.ActionAudioBecomingNoisy) _audio.UscitaAudioPersa();
        }
    }

    private class ControlloDispositivi : AudioDeviceCallback
    {
        private readonly AudioService _audio;
        private bool _elencoIniziale = true;
        public ControlloDispositivi(AudioService audio) => _audio = audio;

        // Cuffie e casse esterne (non l'altoparlante del telefono né il Bluetooth delle chiamate)
        private static bool IsCuffia(AudioDeviceInfo d) => d.IsSink && d.Type is
            AudioDeviceType.BluetoothA2dp or AudioDeviceType.BleHeadset or AudioDeviceType.BleSpeaker or
            AudioDeviceType.WiredHeadphones or AudioDeviceType.WiredHeadset or AudioDeviceType.UsbHeadset or
            AudioDeviceType.HearingAid;

        public override void OnAudioDevicesAdded(AudioDeviceInfo[] addedDevices)
        {
            // Alla registrazione arriva l'elenco dei dispositivi già presenti: non è un collegamento
            if (_elencoIniziale) { _elencoIniziale = false; return; }
            if (addedDevices?.Any(IsCuffia) == true) _audio.UscitaAudioCollegata();
        }

        public override void OnAudioDevicesRemoved(AudioDeviceInfo[] removedDevices)
        {
            if (removedDevices?.Any(IsCuffia) == true) _audio.UscitaAudioPersa();
        }
    }
}
