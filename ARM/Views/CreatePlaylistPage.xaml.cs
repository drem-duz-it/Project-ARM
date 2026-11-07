using ARM.Data;
using ARM.Models;
using YoutubeExplode.Playlists;
using Playlist = ARM.Models.Playlist;

namespace ARM.Views;

public partial class CreatePlaylistPage : ContentPage
{
    private readonly DatabaseService _databaseService;
    private string _imagePath = "";
    private Playlist _playlistToEdit;

    // Nel costruttore, chiamiamo il parametro "existingPlaylist"
    public CreatePlaylistPage(DatabaseService dbService, Playlist existingPlaylist = null)
    {
        InitializeComponent();
        Loc.Register(this);
        _databaseService = dbService;
        _playlistToEdit = existingPlaylist; 

        if (_playlistToEdit != null)
        {
            Title = Loc.T("Modifica Playlist");
            SaveButton.Text = Loc.T("Aggiorna playlist");
            HeaderTitle.Text = Loc.T("Modifica playlist");
            DeleteButton.IsVisible = true;

            NameEntry.Text = _playlistToEdit.Name;
            DescriptionEditor.Text = _playlistToEdit.Description;
            _imagePath = _playlistToEdit.ImagePath;

            if (!string.IsNullOrEmpty(_imagePath))
            {
                { var p = _imagePath; PlaylistImage.Source = ImageSource.FromStream(() => File.OpenRead(p)); }
            }
        }
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameEntry.Text))
        {
            await DisplayAlert(Loc.T("Attenzione"), Loc.T("Inserisci un nome per la playlist."), Loc.T("OK"));
            return;
        }

        // Chiamiamo la variabile locale "playlistToSave" per non creare conflitti
        var playlistToSave = _playlistToEdit ?? new Playlist();

        if (string.IsNullOrEmpty(playlistToSave.Id))
            playlistToSave.Id = Guid.NewGuid().ToString();

        playlistToSave.Name = NameEntry.Text;
        playlistToSave.Description = DescriptionEditor.Text;
        // Copertina nuova o cambiata: le diamo un id e la salviamo tra le copertine, così passa agli altri dispositivi
        if (!string.IsNullOrEmpty(_imagePath) && _imagePath != playlistToSave.ImagePath && File.Exists(_imagePath))
        {
            var imageId = Guid.NewGuid().ToString("N");
            var dest = DatabaseService.CoverPath(imageId);
            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            File.Copy(_imagePath, dest, overwrite: true);
            playlistToSave.ImageId = imageId;
            _imagePath = dest;
        }
        playlistToSave.ImagePath = _imagePath;

        await _databaseService.SavePlaylistAsync(playlistToSave);
        await Navigation.PopModalAsync();
    }

    // Apre la galleria del dispositivo
    private async void OnSelectImageClicked(object sender, EventArgs e)
    {
        try
        {
            var result = await MediaPicker.Default.PickPhotoAsync();
            if (result != null)
            {
                // Copia l'immagine nella cartella sicura dell'app
                string localFilePath = Path.Combine(FileSystem.AppDataDirectory, result.FileName);
                using Stream sourceStream = await result.OpenReadAsync();
                using FileStream localFileStream = File.OpenWrite(localFilePath);
                await sourceStream.CopyToAsync(localFileStream);

                _imagePath = localFilePath;
                { var p = _imagePath; PlaylistImage.Source = ImageSource.FromStream(() => File.OpenRead(p)); }
            }
        }
        catch (Exception)
        {
            await DisplayAlert(Loc.T("Errore"), Loc.T("Impossibile caricare l'immagine."), Loc.T("OK"));
        }
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }

    private async void OnDeleteClicked(object sender, EventArgs e)
    {
        // Apre un popup che ti chiede se sei sicuro (Restituisce True se clicchi Sì)
        bool conferma = await DisplayAlert(Loc.T("Attenzione"), Loc.T("Vuoi davvero eliminare questa playlist?"), Loc.T("Sì, elimina"), Loc.T("No, annulla"));

        if (conferma)
        {
            // Solo se hai cliccato Sì, distrugge la playlist
            await _databaseService.DeletePlaylistAsync(_playlistToEdit);
            await Navigation.PopModalAsync();
        }
    }
}