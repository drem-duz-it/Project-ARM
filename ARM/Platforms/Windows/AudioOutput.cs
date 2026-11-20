using Windows.Devices.Enumeration;
using Windows.Media.Devices;

namespace ARM.Services;

// Windows: pausa quando sparisce il dispositivo di uscita in uso (cuffie Bluetooth spente, cuffie USB staccate).
// Se invece cambia perché se ne è collegato uno nuovo, la musica continua e si segna il collegamento.
public partial class AudioService
{
    private bool _controlloUscita;
    private string _uscitaAttuale;

    partial void AvviaControlloUscitaAudio()
    {
        if (_controlloUscita) return;
        _controlloUscita = true;
        try
        {
            _uscitaAttuale = MediaDevice.GetDefaultAudioRenderId(AudioDeviceRole.Default);
            MediaDevice.DefaultAudioRenderDeviceChanged += OnUscitaCambiata;
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Controllo uscita audio: {ex.Message}"); }
    }

    private async void OnUscitaCambiata(object sender, DefaultAudioRenderDeviceChangedEventArgs e)
    {
        if (e.Role != AudioDeviceRole.Default) return;
        var precedente = _uscitaAttuale;
        _uscitaAttuale = e.Id;
        try
        {
            // Elenco delle uscite attive: se la precedente non c'è più, è stata scollegata
            var attive = await DeviceInformation.FindAllAsync(MediaDevice.GetAudioRenderSelector());
            bool ancoraPresente = string.IsNullOrEmpty(precedente) || attive.Any(d => d.Id == precedente);
            if (ancoraPresente) UscitaAudioCollegata();
            else UscitaAudioPersa();
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Uscita audio: {ex.Message}"); }
    }
}
