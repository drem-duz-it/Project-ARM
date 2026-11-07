using System.Globalization;
using ARM.Models;
using Microsoft.Maui.Controls.Shapes;
using Path = Microsoft.Maui.Controls.Shapes.Path;

namespace ARM;

// Sistema di design di ARM 3.0 ("Oggi" sobria e professionale):
// font Plus Jakarta Sans, spaziatura 4/8/12/16/24/32, card raggio 12 con bordo 1 (niente ombre), campi 52 raggio 10,
// pulsanti 44 raggio 10, icone lineari (tratto 1,5 su griglia 24), aree toccabili >= 44. Nessuna emoji.
// Tutti i colori arrivano dai token del tema attivo (Themes/*.xaml).
public static class Ui
{
    // Cultura per date e numeri: segue la lingua scelta (il nome "It" è rimasto per non toccare tutti i punti d'uso)
    public static CultureInfo It => Loc.Culture;

    // ---------- Font ----------
    public const string FontRegular = "JakartaMedium";     // 500
    public const string FontSemi = "JakartaSemiBold";      // 600
    public const string FontBold = "JakartaBold";          // 700
    public const string FontHeavy = "JakartaExtraBold";    // 800

    // ---------- Icone (Material Design filled, griglia 24×24) ----------
    public static class Icons
    {
        public const string Today = "M10 20v-6h4v6h5v-8h3L12 3 2 12h3v8z";
        public const string Note = "M14 2H6c-1.1 0-1.99.9-1.99 2L4 20c0 1.1.89 2 1.99 2H18c1.1 0 2-.9 2-2V8l-6-6zm2 16H8v-2h8v2zm0-4H8v-2h8v2zm-3-5V3.5L18.5 9H13z";
        public const string Task = "M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z";
        public const string Calendar = "M17 12h-5v5h5v-5zM16 1v2H8V1H6v2H5c-1.11 0-1.99.9-1.99 2L3 19c0 1.1.89 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2h-1V1h-2zm3 18H5V8h14v11z";
        public const string Music = "M12 3v10.55c-.59-.34-1.27-.55-2-.55-2.21 0-4 1.79-4 4s1.79 4 4 4 4-1.79 4-4V7h4V3h-6z";
        public const string Plus = "M19 13h-6v6h-2v-6H5v-2h6V5h2v6h6v2z";
        public const string Check = "M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z";
        public const string Pin = "M16 9V4h1c.55 0 1-.45 1-1s-.45-1-1-1H7c-.55 0-1 .45-1 1s.45 1 1 1h1v5c0 1.66-1.34 3-3 3v2h5.97v7l1 1 1-1v-7H19v-2c-1.66 0-3-1.34-3-3z";
        public const string Euro = "M15 18.5c-2.51 0-4.68-1.42-5.76-3.5H15l1-2H8.58c-.05-.33-.08-.66-.08-1s.03-.67.08-1H15l1-2H9.24C10.32 6.92 12.5 5.5 15 5.5c1.61 0 3.09.59 4.23 1.57L21 5.3C19.41 3.87 17.3 3 15 3c-3.92 0-7.24 2.51-8.48 6H3l-1 2h4.06c-.04.33-.06.66-.06 1s.02.67.06 1H3l-1 2h4.52c1.24 3.49 4.56 6 8.48 6 2.31 0 4.41-.87 6-2.3l-1.78-1.77c-1.13.98-2.6 1.57-4.22 1.57z";
        public const string Bell = "M12 22c1.1 0 2-.9 2-2h-4c0 1.1.89 2 2 2zm6-6v-5c0-3.07-1.64-5.64-4.5-6.32V4c0-.83-.67-1.5-1.5-1.5s-1.5.67-1.5 1.5v.68C7.63 5.36 6 7.92 6 11v5l-2 2v1h16v-1l-2-2z";
        public const string Bulb = "M9 21c0 .55.45 1 1 1h4c.55 0 1-.45 1-1v-1H9v1zm3-19C8.14 2 5 5.14 5 9c0 2.38 1.19 4.47 3 5.74V17c0 .55.45 1 1 1h6c.55 0 1-.45 1-1v-2.26c1.81-1.27 3-3.36 3-5.74 0-3.86-3.14-7-7-7z";
        public const string Left = "M15.41 7.41L14 6l-6 6 6 6 1.41-1.41L10.83 12z";
        public const string Right = "M10 6L8.59 7.41 13.17 12l-4.58 4.59L10 18l6-6z";
        public const string Down = "M16.59 8.59L12 13.17 7.41 8.59 6 10l6 6 6-6z";
        public const string Close = "M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z";
        public const string Clock = "M11.99 2C6.47 2 2 6.48 2 12s4.47 10 9.99 10C17.52 22 22 17.52 22 12S17.52 2 11.99 2zM12 20c-4.42 0-8-3.58-8-8s3.58-8 8-8 8 3.58 8 8-3.58 8-8 8zm.5-13H11v6l5.25 3.15.75-1.23-4.5-2.67z";
        public const string Search = "M15.5 14h-.79l-.28-.27C15.41 12.59 16 11.11 16 9.5 16 5.91 13.09 3 9.5 3S3 5.91 3 9.5 5.91 16 9.5 16c1.61 0 3.09-.59 4.23-1.57l.27.28v.79l5 4.99L20.49 19l-4.99-5zm-6 0C7.01 14 5 11.99 5 9.5S7.01 5 9.5 5 14 7.01 14 9.5 11.99 14 9.5 14z";
        public const string Settings = "M3 17v2h6v-2H3zM3 5v2h10V5H3zm10 16v-2h8v-2h-8v-2h-2v6h2zM7 9v2H3v2h4v2h2V9H7zm14 4v-2H11v2h10zm-6-4h2V7h4V5h-4V3h-2v6z";
        public const string Theme = "M12 3c-4.97 0-9 4.03-9 9s4.03 9 9 9c.83 0 1.5-.67 1.5-1.5 0-.39-.15-.74-.39-1.01-.23-.26-.38-.61-.38-.99 0-.83.67-1.5 1.5-1.5H16c2.76 0 5-2.24 5-5 0-4.42-4.03-8-9-8zm-5.5 9c-.83 0-1.5-.67-1.5-1.5S5.67 9 6.5 9 8 9.67 8 10.5 7.33 12 6.5 12zm3-4C8.67 8 8 7.33 8 6.5S8.67 5 9.5 5s1.5.67 1.5 1.5S10.33 8 9.5 8zm5 0c-.83 0-1.5-.67-1.5-1.5S13.67 5 14.5 5s1.5.67 1.5 1.5S15.33 8 14.5 8zm3 4c-.83 0-1.5-.67-1.5-1.5S16.67 9 17.5 9s1.5.67 1.5 1.5-.67 1.5-1.5 1.5z";
        public const string Info = "M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-6h2v6zm0-8h-2V7h2v2z";
        public const string Location = "M12 2C8.13 2 5 5.13 5 9c0 5.25 7 13 7 13s7-7.75 7-13c0-3.87-3.13-7-7-7zm0 9.5c-1.38 0-2.5-1.12-2.5-2.5s1.12-2.5 2.5-2.5 2.5 1.12 2.5 2.5-1.12 2.5-2.5 2.5z";
        public const string ArrowUp = "M4 12l1.41 1.41L11 7.83V20h2V7.83l5.58 5.59L20 12l-8-8-8 8z";
        public const string ArrowDown = "M20 12l-1.41-1.41L13 16.17V4h-2v12.17l-5.58-5.59L4 12l8 8 8-8z";
        public const string Repeat = "M7 7h10v3l4-4-4-4v3H5v6h2V7zm10 10H7v-3l-4 4 4 4v-3h12v-6h-2v4z";
        public const string Chart = "M22 21H2V3h2v16h2v-9h4v9h2V6h4v13h2v-5h4v7z";
        public const string Send = "M19 7v4H5.83l3.58-3.59L8 6l-6 6 6 6 1.41-1.41L5.83 13H21V7z";
        public const string Spark = "M19 9l1.25-2.75L23 5l-2.75-1.25L19 1l-1.25 2.75L15 5l2.75 1.25L19 9zm-7.5.5L9 4 6.5 9.5 1 12l5.5 2.5L9 20l2.5-5.5L17 12l-5.5-2.5zM19 15l-1.25 2.75L15 19l2.75 1.25L19 23l1.25-2.75L23 19l-2.75-1.25L19 15z";
        public const string Sync = "M12 4V1L8 5l4 4V6c3.31 0 6 2.69 6 6 0 1.01-.25 1.97-.7 2.8l1.46 1.46C19.54 15.03 20 13.57 20 12c0-4.42-3.58-8-8-8zm0 14c-3.31 0-6-2.69-6-6 0-1.01.25-1.97.7-2.8L5.24 7.74C4.46 8.97 4 10.43 4 12c0 4.42 3.58 8 8 8v3l4-4-4-4v3z";
        public const string Edit = "M3 17.25V21h3.75L17.81 9.94l-3.75-3.75L3 17.25zM20.71 7.04c.39-.39.39-1.02 0-1.41l-2.34-2.34c-.39-.39-1.02-.39-1.41 0l-1.83 1.83 3.75 3.75 1.83-1.83z";
        public const string Trash = "M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2V7H6v12zM19 4h-3.5l-1-1h-5l-1 1H5v2h14V4z";
        public const string Download = "M19 9h-4V3H9v6H5l7 7 7-7zM5 18v2h14v-2H5z";
        public const string Play = "M8 5v14l11-7z";
        public const string Pause = "M6 19h4V5H6v14zm8-14v14h4V5h-4z";
        public const string Next = "M6 18l8.5-6L6 6v12zM16 6v12h2V6h-2z";
        public const string Prev = "M6 6h2v12H6zm3.5 6l8.5 6V6z";
        public const string Shuffle = "M10.59 9.17L5.41 4 4 5.41l5.17 5.17 1.42-1.41zM14.5 4l2.04 2.04L4 18.59 5.41 20 17.96 7.46 20 9.5V4h-5.5zm.33 9.41l-1.41 1.41 3.13 3.13L14.5 20H20v-5.5l-2.04 2.04-3.13-3.13z";
        public const string Lyrics = "M14 17H4v2h10v-2zm6-8H4v2h16V9zM4 15h16v-2H4v2zM4 5v2h16V5H4z";
        public const string Library = "M20 2H8c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V4c0-1.1-.9-2-2-2zm-2 5h-3v5.5c0 1.38-1.12 2.5-2.5 2.5S10 13.88 10 12.5s1.12-2.5 2.5-2.5c.57 0 1.08.19 1.5.51V5h4v2zM4 6H2v14c0 1.1.9 2 2 2h14v-2H4V6z";
        public const string Explore = "M12 10.9c-.61 0-1.1.49-1.1 1.1s.49 1.1 1.1 1.1c.61 0 1.1-.49 1.1-1.1s-.49-1.1-1.1-1.1zM12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm2.19 12.19L6 18l3.81-8.19L18 6l-3.81 8.19z";
        public const string Link = "M3.9 12c0-1.71 1.39-3.1 3.1-3.1h4V7H7c-2.76 0-5 2.24-5 5s2.24 5 5 5h4v-1.9H7c-1.71 0-3.1-1.39-3.1-3.1zM8 13h8v-2H8v2zm9-6h-4v1.9h4c1.71 0 3.1 1.39 3.1 3.1s-1.39 3.1-3.1 3.1h-4V17h4c2.76 0 5-2.24 5-5s-2.24-5-5-5z";
        public const string Pc = "M21 2H3c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h7v2H8v2h8v-2h-2v-2h7c1.1 0 2-.9 2-2V4c0-1.1-.9-2-2-2zm0 14H3V4h18v12z";
        public const string Phone = "M17 1.01L7 1c-1.1 0-2 .9-2 2v18c0 1.1.9 2 2 2h10c1.1 0 2-.9 2-2V3c0-1.1-.9-1.99-2-1.99zM17 19H7V5h10v14z";
        public const string Wifi = "M1 9l2 2c4.97-4.97 13.03-4.97 18 0l2-2C16.93 2.93 7.08 2.93 1 9zm8 8l3 3 3-3c-1.65-1.66-4.34-1.66-6 0zm-4-4l2 2c2.76-2.76 7.24-2.76 10 0l2-2C15.14 9.14 8.87 9.14 5 13z";
        public const string More = "M12 8c1.1 0 2-.9 2-2s-.9-2-2-2-2 .9-2 2 .9 2 2 2zm0 2c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2zm0 6c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2z";
        public const string Warn = "M1 21h22L12 2 1 21zm12-3h-2v-2h2v2zm0-4h-2v-4h2v4z";
        public const string Week = "M6 5H3c-.55 0-1 .45-1 1v12c0 .55.45 1 1 1h3c.55 0 1-.45 1-1V6c0-.55-.45-1-1-1zm14 0h-3c-.55 0-1 .45-1 1v12c0 .55.45 1 1 1h3c.55 0 1-.45 1-1V6c0-.55-.45-1-1-1zm-7 0h-3c-.55 0-1 .45-1 1v12c0 .55.45 1 1 1h3c.55 0 1-.45 1-1V6c0-.55-.45-1-1-1z";
        public const string PlaylistAdd = "M14 10H2v2h12v-2zm0-4H2v2h12V6zm4 8v-4h-2v4h-4v2h4v4h2v-4h4v-2h-4zM2 16h8v-2H2v2z";

        // Solo lineari (3.0)
        public const string Mail = "M4 6h16v12H4Z M4 7l8 6 8-6";
        public const string Box = "M12 3l8 4.5v9L12 21l-8-4.5v-9L12 3Z M4 7.5l8 4.5 8-4.5 M12 12v9";
        public const string Refresh = "M20 12a8 8 0 1 1-2.3-5.7 M20 4v5h-5";
        public const string Conflict = "M7 4v10 M7 14a3 3 0 1 0 0 6a3 3 0 0 0 0-6Z M17 10a3 3 0 1 0 0-6a3 3 0 0 0 0 6Z M17 10c0 4-4 4-7 6";
        public const string Arrow = "M5 12h14 M13 6l6 6-6 6";
    }

    // Versione lineare (ARM 3.0, griglia 24, tratto 1,5) delle icone: Icon() la usa al posto di quella piena.
    private static readonly Dictionary<string, string> LineIcons = new()
    {
        [Icons.Today] = "M4 10.5 12 4l8 6.5V20a1 1 0 0 1-1 1h-4v-6h-6v6H5a1 1 0 0 1-1-1Z",
        [Icons.Note] = "M7 3h7l5 5v12a1 1 0 0 1-1 1H7a1 1 0 0 1-1-1V4a1 1 0 0 1 1-1Z M14 3v5h5 M9 13h6 M9 17h4",
        [Icons.Task] = "M12 21a9 9 0 1 0 0-18a9 9 0 0 0 0 18Z M8.5 12l2.5 2.5 4.5-5",
        [Icons.Calendar] = "M5 5h14a1 1 0 0 1 1 1v13a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V6a1 1 0 0 1 1-1Z M4 10h16 M8 3v4 M16 3v4",
        [Icons.Music] = "M9 18V6l11-2v12 M9 18a3 3 0 1 1-6 0a3 3 0 0 1 6 0Z M20 16a3 3 0 1 1-6 0a3 3 0 0 1 6 0Z",
        [Icons.Plus] = "M12 5v14 M5 12h14",
        [Icons.Check] = "M6 12.5l4 4 8-9",
        [Icons.Theme] = "M12 3a9 9 0 1 0 0 18a9 9 0 0 0 0-18Z M12 3v18",
        [Icons.Sync] = "M20 11a8 8 0 0 0-14.6-4.5L4 8 M4 4v4h4 M4 13a8 8 0 0 0 14.6 4.5L20 16 M20 20v-4h-4",
        [Icons.Spark] = "M6 9a6 6 0 0 1 12 0c0 6 2.5 7.5 2.5 7.5h-17S6 15 6 9Z M10 20a2 2 0 0 0 4 0",
        [Icons.Bell] = "M6 9a6 6 0 0 1 12 0c0 6 2.5 7.5 2.5 7.5h-17S6 15 6 9Z M10 20a2 2 0 0 0 4 0",
        [Icons.Edit] = "M4 20h4L19 9a2.8 2.8 0 0 0-4-4L4 16v4Z M13.5 6.5l4 4",
        [Icons.Warn] = "M12 4 2.5 20h19L12 4Z M12 10v4 M12 17h.01",
        [Icons.Pc] = "M5 6h14v10H5Z M3 19h18",
        [Icons.Phone] = "M8 3h8a1 1 0 0 1 1 1v16a1 1 0 0 1-1 1H8a1 1 0 0 1-1-1V4a1 1 0 0 1 1-1Z M11 18h2",
        [Icons.Prev] = "M18 6 10 12l8 6V6Z M6 6v12",
        [Icons.Next] = "M6 6l8 6-8 6V6Z M18 6v12",
        [Icons.Play] = "M8 5.5v13l11-6.5-11-6.5Z",
        [Icons.Pause] = "M8 5v14 M16 5v14",
        [Icons.Right] = "M9 6l6 6-6 6",
        [Icons.Left] = "M15 6l-6 6 6 6",
        [Icons.Down] = "M6 9l6 6 6-6",
        [Icons.Close] = "M6 6l12 12 M18 6 6 18",
        [Icons.Search] = "M11 18a7 7 0 1 0 0-14a7 7 0 0 0 0 14Z M20 20l-4-4",
        [Icons.Pin] = "M9 4h6 M10 4v6l-3 4h10l-3-4V4 M12 14v6",
        [Icons.Euro] = "M17 6.5A7 7 0 1 0 17 17.5 M4 10h9 M4 14h9",
        [Icons.Bulb] = "M9 18h6 M10 21h4 M12 3a6 6 0 0 0-3.5 10.9V16h7v-2.1A6 6 0 0 0 12 3Z",
        [Icons.Clock] = "M12 21a9 9 0 1 0 0-18a9 9 0 0 0 0 18Z M12 7v5l3 2",
        [Icons.Trash] = "M4 7h16 M9 7V4h6v3 M6 7l1 13h10l1-13",
        [Icons.Info] = "M12 21a9 9 0 1 0 0-18a9 9 0 0 0 0 18Z M12 11v5 M12 8h.01",
        [Icons.Location] = "M12 21s7-6.5 7-12a7 7 0 1 0-14 0c0 5.5 7 12 7 12Z M12 11.5a2.5 2.5 0 1 0 0-5a2.5 2.5 0 0 0 0 5Z",
        [Icons.ArrowUp] = "M12 19V5 M6 11l6-6 6 6",
        [Icons.ArrowDown] = "M12 5v14 M6 13l6 6 6-6",
        [Icons.Repeat] = "M17 3l3 3-3 3 M4 11V9a3 3 0 0 1 3-3h13 M7 21l-3-3 3-3 M20 13v2a3 3 0 0 1-3 3H4",
        [Icons.Chart] = "M4 20h16 M7 16v-5 M12 16V7 M17 16v-8",
        [Icons.Link] = "M10 14a4 4 0 0 0 5.7 0l3-3a4 4 0 0 0-5.7-5.7l-1 1 M14 10a4 4 0 0 0-5.7 0l-3 3a4 4 0 0 0 5.7 5.7l1-1",
        [Icons.More] = "M12 6h.01 M12 12h.01 M12 18h.01",
        [Icons.Wifi] = "M2 9a15 15 0 0 1 20 0 M5.5 12.5a10 10 0 0 1 13 0 M9 16a5 5 0 0 1 6 0 M12 19.5h.01",
        [Icons.Week] = "M4 5h16v14H4Z M9.3 5v14 M14.7 5v14",
        [Icons.Library] = "M4 4v16 M8 4v16 M12 4l5 16 M16 4h4",
        [Icons.Lyrics] = "M4 6h16 M4 10h16 M4 14h10 M4 18h7",
        [Icons.Shuffle] = "M16 4h4v4 M4 20 20 4 M20 16v4h-4 M14 14l6 6 M4 4l5 5",
        [Icons.Download] = "M12 4v11 M7 10l5 5 5-5 M5 20h14",
        [Icons.Send] = "M5 12h14 M11 6l-6 6 6 6",
        [Icons.Explore] = "M12 21a9 9 0 1 0 0-18a9 9 0 0 0 0 18Z M15.5 8.5l-2 5-5 2 2-5Z",
        [Icons.PlaylistAdd] = "M4 6h12 M4 11h12 M4 16h7 M18 13v6 M15 16h6",
        [Icons.Mail] = Icons.Mail,
        [Icons.Box] = Icons.Box,
        [Icons.Refresh] = Icons.Refresh,
        [Icons.Conflict] = Icons.Conflict,
        [Icons.Arrow] = Icons.Arrow,
    };

    // Forme che restano piene anche nella versione lineare (il triangolo del play)
    public static bool IsFilledShape(string data) => data == Icons.Play;

    // Pastelli delle note: si salva il colore "chiaro" (compatibile con le versioni precedenti),
    // a schermo si usa il pastello equivalente del tema (Pastel1..6).
    public static readonly string[] NoteColors = { "", "#FFF1C9", "#FFE1D9", "#D9F5E9", "#DDE9FF", "#EAE2FF", "#FFDDEE" };

    public static Color NoteBackground(string hex)
    {
        int i = Array.IndexOf(NoteColors, hex ?? "");
        return i > 0 ? C($"Pastel{i}") : C("TabBgColor");
    }

    // 3.0: il colore di una nota diventa un punto da 8 nella palette delle categorie (null = nessun colore)
    public static Color NoteDot(string hex)
    {
        int i = Array.IndexOf(NoteColors, hex ?? "");
        string[] cat = { null, "Cat2", "Cat5", "Cat4", "Cat1", "Cat3", "Cat5" };
        return i > 0 ? C(cat[i]) : null;
    }

    // Punto pieno (categorie, stato): diametro 8 di default
    public static BoxView Dot(Color color, double size = 8) => new()
    {
        WidthRequest = size, HeightRequest = size, CornerRadius = size / 2, Color = color,
        VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.Start
    };

    public static Color C(string key) =>
        Application.Current.Resources.TryGetValue(key, out var v) && v is Color c ? c : Colors.Gray;

    public static double D(string key, double fallback) =>
        Application.Current.Resources.TryGetValue(key, out var v) && v is double d ? d : fallback;

    public static Color Tint(string key, float alpha) => C(key).WithAlpha(alpha);

    // Fondo tenue di un colore ("Soft"): 12% Chiaro, 16% Classico (vedi SoftAlpha nei temi)
    public static Color Soft(string key) => C(key).WithAlpha((float)D("SoftAlpha", 0.13));

    // Famiglia di colore per tipo: fare = Accent · tempo = Accent5 · soldi = Accent2/3 · ricordare = Accent4
    public static string TypeColorKey(string type) => type switch
    {
        "Pagamento" => "Accent2Color",
        "Promemoria" => "Accent4Color",
        "Idea" => "Accent4Color",
        _ => "AccentColor"
    };

    public static string TypeIcon(string type) => type switch
    {
        "Pagamento" => Icons.Euro,
        "Promemoria" => Icons.Bell,
        "Idea" => Icons.Bulb,
        _ => Icons.Note
    };

    public static string PriorityColorKey(int p) => p switch { 2 => "Accent2Color", 0 => "Accent3Color", _ => "Accent4Color" };
    public static string PriorityText(int p) => p switch { 2 => Loc.T("Alta"), 0 => Loc.T("Bassa"), _ => Loc.T("Normale") };

    // ---------- Date ----------
    public static string RelativeDate(DateTime d)
    {
        var diff = (d.Date - DateTime.Today).Days;
        return diff switch
        {
            0 => Loc.T("Oggi"),
            1 => Loc.T("Domani"),
            -1 => Loc.T("Ieri"),
            > 1 and < 7 => Cap(d.ToString("dddd", It)),
            _ => d.ToString(d.Year == DateTime.Today.Year ? "d MMM" : "d MMM yyyy", It)
        };
    }

    public static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0], It) + s[1..];

    public static string Euro(double v, bool sign = false, bool decimals = true)
    {
        string s = "€ " + Math.Abs(v).ToString(decimals ? "#,##0.00" : "#,##0", It);
        return sign ? (v < 0 ? "− " : "+ ") + s : s;
    }

    // ---------- Testo ----------
    // Icona: se esiste la versione lineare la disegna a tratto (1,5 su 24, estremi arrotondati), altrimenti piena.
    // Le lineari restano sulla griglia 24 (scala size/24), così non si deformano.
    public static Path Icon(string data, Color color, double size = 20)
    {
        var p = new Path
        {
            Aspect = Stretch.Uniform,
            WidthRequest = size,
            HeightRequest = size,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
        if (LineIcons.ContainsKey(data))
        {
            p.StrokeThickness = Math.Max(1.1, 1.5 * size / 24);
            p.StrokeLineCap = PenLineCap.Round;
            p.StrokeLineJoin = PenLineJoin.Round;
            p.Stroke = new SolidColorBrush(color);
        }
        SetIconData(p, data, color);
        return p;
    }

    // Cambia forma a un'icona già a schermo (es. play/pausa), mantenendo il colore
    public static void SetIconData(Path p, string data, Color color = null)
    {
        color ??= (p.Stroke as SolidColorBrush)?.Color ?? (p.Fill as SolidColorBrush)?.Color ?? Colors.Gray;
        bool line = LineIcons.TryGetValue(data, out var lineData) && p.Stroke != null;
        p.Data = (Geometry)new PathGeometryConverter().ConvertFromInvariantString(line ? lineData : data);
        if (line)
        {
            // Griglia 24 fissa: niente adattamento ai bordi del disegno, solo la scala (il tratto non viene scalato)
            double k = (p.WidthRequest > 0 ? p.WidthRequest : 24) / 24.0;
            p.Aspect = Stretch.None;
            p.RenderTransform = new ScaleTransform { ScaleX = k, ScaleY = k };
        }
        else
        {
            p.Aspect = Stretch.Uniform;
            p.RenderTransform = null;
        }
        p.Fill = !line || IsFilledShape(data) ? new SolidColorBrush(color) : null;
    }

    // Cambia colore a un'icona già a schermo (piena o lineare)
    public static void SetIconColor(Path p, Color color)
    {
        if (p.Stroke != null) p.Stroke = new SolidColorBrush(color);
        if (p.Fill != null) p.Fill = new SolidColorBrush(color);
    }

    // Testo generico: "bold" sceglie ExtraBold per i titoli (≥ 18) e Bold per il resto
    public static Label Text(string text, double size = 14, string colorKey = "TextColor", bool bold = false, int maxLines = 0)
    {
        var l = new Label
        {
            Text = text,
            FontSize = size,
            TextColor = C(colorKey),
            FontFamily = bold ? (size >= 18 ? FontHeavy : FontBold) : (size <= 13 ? FontSemi : FontRegular),
        };
        if (maxLines > 0) { l.MaxLines = maxLines; l.LineBreakMode = LineBreakMode.TailTruncation; }
        else l.LineBreakMode = LineBreakMode.WordWrap;
        return l;
    }

    // Scala tipografica del design
    public static Label Display(string t) => Styled(t, 32, FontBold, -0.6, 38);
    public static Label PageTitle(string t) => Styled(t, 28, FontBold, -0.4, 34);
    public static Label SecTitle(string t) => Styled(t, 18, FontBold, -0.2, 24);
    public static Label CardTitle(string t, int maxLines = 2) { var l = Styled(t, 15, FontSemi, 0, 22); if (maxLines > 0) { l.MaxLines = maxLines; l.LineBreakMode = LineBreakMode.TailTruncation; } return l; }
    public static Label Body(string t, string colorKey = "TextColor") { var l = Styled(t, 15, FontRegular, 0, 22); l.TextColor = C(colorKey); return l; }
    public static Label Small(string t, string colorKey = "SubTextColor") { var l = Styled(t, 13, FontRegular, 0, 18); l.TextColor = C(colorKey); return l; }
    public static Label Amount(string t, string colorKey) { var l = Styled(t, 32, FontBold, -0.6, 38); l.TextColor = C(colorKey); return l; }

    // Riga singola troncata: titolo di riga 15 SemiBold, meta 13
    public static Label RowTitle(string t, string colorKey = "TextColor") => new() { Text = t, FontSize = 15, FontFamily = FontSemi, TextColor = C(colorKey), MaxLines = 1, LineBreakMode = LineBreakMode.TailTruncation };
    public static Label Meta(string t, string colorKey = "SubTextColor") => new() { Text = t, FontSize = 13, FontFamily = FontRegular, TextColor = C(colorKey), MaxLines = 1, LineBreakMode = LineBreakMode.TailTruncation };

    // Maiuscoletto: 12/16 Bold, spaziatura +1,2
    public static Label Caps(string t, string colorKey = "SubTextColor") => new()
    {
        Text = (t ?? "").ToUpper(It),
        FontSize = 12,
        FontFamily = FontBold,
        CharacterSpacing = 1.2,
        TextColor = C(colorKey)
    };

    private static Label Styled(string t, double size, string font, double spacing, double lineHeightPx) => new()
    {
        Text = t,
        FontSize = size,
        FontFamily = font,
        CharacterSpacing = spacing,
        LineHeight = lineHeightPx / size / 1.2,
        TextColor = C("TextColor"),
        LineBreakMode = LineBreakMode.WordWrap
    };

    // ---------- Superfici ----------
    // Ombra unica del design: solo tema Chiaro (ShadowColor al 7%, offset 0,4, raggio 16); nei temi scuri c'è il bordo
    public static Shadow CardShadow() => D("ShadowOpacity", 0) > 0
        ? new Shadow { Brush = new SolidColorBrush(C("ShadowColor")), Offset = new Point(0, 4), Radius = 16, Opacity = (float)D("ShadowOpacity", 0.07) }
        : null;

    public static Border Card(View content, Thickness? padding = null, Color background = null, double radius = 12)
    {
        return new Border
        {
            Content = content,
            Padding = padding ?? new Thickness(16),
            BackgroundColor = background ?? C("TabBgColor"),
            Stroke = new SolidColorBrush(C("CardBorderColor")),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = radius },
            Shadow = CardShadow()
        };
    }

    // Card a gradiente (giornata, hero): LinearGradientBrush 135°
    public static Brush HeroBrush() => new LinearGradientBrush(new GradientStopCollection
    {
        new GradientStop(C("Hero1Color"), 0f),
        new GradientStop(C("Hero2Color"), 1f)
    }, new Point(0, 0), new Point(1, 1));

    // ---------- Chip / filtro ----------
    // 36 alto, padding 14, raggio 18, bordo 1. Attivo = fondo TextColor, testo BgColor.
    public class ChipView : Border
    {
        private readonly Label _label;
        private readonly Path _icon;
        private readonly string _colorKey;

        public ChipView(string text, bool active, string colorKey, string icon = null)
        {
            _colorKey = colorKey;
            var row = new HorizontalStackLayout { Spacing = 6, VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.Center };
            if (icon != null) { _icon = Icon(icon, C(colorKey), 18); row.Children.Add(_icon); }
            _label = new Label { Text = text, FontSize = 14, FontFamily = FontSemi, VerticalOptions = LayoutOptions.Center };
            row.Children.Add(_label);

            Content = row;
            HeightRequest = 36;
            Padding = new Thickness(14, 0);
            StrokeThickness = 1;
            StrokeShape = new RoundRectangle { CornerRadius = 10 };
            SetActive(active);
        }

        public void SetActive(bool active)
        {
            BackgroundColor = active ? C("AccentColor") : Colors.Transparent;
            Stroke = new SolidColorBrush(active ? C("AccentColor") : C("BorderStrongColor"));
            _label.TextColor = active ? C("OnAccentColor") : C("TextColor");
            if (_icon != null) SetIconColor(_icon, active ? C("OnAccentColor") : C("TextColor"));
        }
    }

    public static ChipView Chip(string text, bool active, string colorKey, Action onTap, string icon = null)
    {
        var chip = new ChipView(text, active, colorKey, icon);
        Press(chip, onTap);
        return chip;
    }

    // Gruppo di chip a scelta singola: cambia selezione senza ricreare le viste
    // (ricreare o spostare viste mentre sono a schermo fa crashare WinUI/Android).
    public class ChipGroup : ContentView
    {
        private readonly Dictionary<string, ChipView> _chips = new();
        public string Selected { get; private set; }

        // columns = 0 → riga orizzontale scorrevole; > 0 → griglia con quel numero di colonne; -1 → vanno a capo
        public ChipGroup(IEnumerable<(string Key, string Label, string ColorKey, string Icon)> items, string selected, Action<string> onSelect, int columns = 0)
        {
            Selected = selected;
            if (columns < 0)
            {
                var flex = new FlexLayout
                {
                    Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap,
                    AlignItems = Microsoft.Maui.Layouts.FlexAlignItems.Start,
                    AlignContent = Microsoft.Maui.Layouts.FlexAlignContent.Start
                };
                foreach (var item in items)
                {
                    var key = item.Key;
                    var chip = new ChipView(item.Label, key == selected, item.ColorKey, item.Icon) { Margin = new Thickness(0, 0, 8, 8), MinimumHeightRequest = 36 };
                    Press(chip, () => { Select(key); onSelect(key); });
                    _chips[key] = chip;
                    flex.Add(chip);
                }
                Content = flex;
                return;
            }
            Layout layout = columns > 0 ? new Grid { ColumnSpacing = 8, RowSpacing = 8 } : new HorizontalStackLayout { Spacing = 8 };
            if (layout is Grid g) for (int c = 0; c < columns; c++) g.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

            int i = 0;
            foreach (var item in items)
            {
                var key = item.Key;
                var chip = new ChipView(item.Label, key == selected, item.ColorKey, item.Icon);
                if (columns > 0) chip.HeightRequest = 44;
                Press(chip, () => { Select(key); onSelect(key); });
                _chips[key] = chip;
                if (layout is Grid grid)
                {
                    if (i / columns >= grid.RowDefinitions.Count) grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                    grid.Add(chip, i % columns, i / columns);
                }
                else layout.Add(chip);
                i++;
            }

            Content = columns > 0 ? layout : new ScrollView { Orientation = ScrollOrientation.Horizontal, HorizontalScrollBarVisibility = ScrollBarVisibility.Never, Content = layout };
        }

        public void Select(string key)
        {
            Selected = key;
            foreach (var kv in _chips) kv.Value.SetActive(kv.Key == key);
        }
    }

    // Controllo segmentato (Da pagare / Da ricevere, priorità...): fondo FieldBg, voce attiva su card
    public class Segmented : ContentView
    {
        private readonly Dictionary<string, (Border Box, Label Label)> _items = new();
        public string Selected { get; private set; }

        public Segmented(IEnumerable<(string Key, string Label)> items, string selected, Action<string> onSelect)
        {
            var grid = new Grid { ColumnSpacing = 4 };
            int i = 0;
            foreach (var (key, label) in items)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                var l = new Label { Text = Loc.T(label), FontFamily = FontSemi, FontSize = 14, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
                var b = new Border { HeightRequest = 40, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 8 }, Content = l };
                var k = key;
                Press(b, () => { Select(k); onSelect(k); });
                _items[key] = (b, l);
                grid.Add(b, i++);
            }
            Content = new Border
            {
                Padding = 2,
                BackgroundColor = Colors.Transparent,
                Stroke = new SolidColorBrush(C("BorderColor")),
                StrokeThickness = 1,
                StrokeShape = new RoundRectangle { CornerRadius = 10 },
                Content = grid
            };
            Select(selected);
        }

        public void Select(string key)
        {
            Selected = key;
            foreach (var kv in _items)
            {
                bool on = kv.Key == key;
                kv.Value.Box.BackgroundColor = on ? C("AccentColor") : Colors.Transparent;
                kv.Value.Label.TextColor = on ? C("OnAccentColor") : C("SubTextColor");
            }
        }
    }

    // ---------- Badge ----------
    // 22 alto, raggio 8, testo 12 Bold, icona 14, fondo tenue del colore. colorKey "TextColor" = badge neutro (fondo FieldBg).
    public static Border Badge(string text, string colorKey, string icon = null)
    {
        bool neutral = colorKey is "TextColor" or "SubTextColor";
        var color = neutral ? C("TextColor") : C(colorKey);
        var row = new HorizontalStackLayout { Spacing = 4, VerticalOptions = LayoutOptions.Center };
        if (icon != null) row.Children.Add(Icon(icon, color, 14));
        row.Children.Add(new Label { Text = text, FontSize = 12, FontFamily = FontSemi, TextColor = color, VerticalOptions = LayoutOptions.Center });
        return new Border
        {
            Content = row,
            HeightRequest = 22,
            Padding = new Thickness(7, 0),
            BackgroundColor = Colors.Transparent,
            Stroke = new SolidColorBrush(neutral ? C("BorderStrongColor") : color.WithAlpha(0.5f)),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 6 },
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Center
        };
    }

    // ---------- Spunte ----------
    // Cerchio 20 (bordo 1,5 neutro; pieno nell'accento quando fatto) dentro un'area toccabile 44×44.
    // colorKey resta per compatibilità: la priorità si mostra col quadratino accanto.
    public static View CheckCircle(bool done, string colorKey, Action onTap, double size = 20)
    {
        var color = C("AccentColor");
        var b = new Border
        {
            WidthRequest = size,
            HeightRequest = size,
            StrokeShape = new RoundRectangle { CornerRadius = size / 2 },
            Stroke = new SolidColorBrush(done ? color : C("BorderStrongColor")),
            StrokeThickness = 1.5,
            BackgroundColor = done ? color : Colors.Transparent,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Content = done ? Icon(Icons.Check, C("OnAccentColor"), size * 0.8) : null
        };
        var area = new Grid { WidthRequest = 44, HeightRequest = 44, VerticalOptions = LayoutOptions.Center, BackgroundColor = Colors.Transparent };
        area.Add(b);
        OnTap(area, async () =>
        {
            await b.ScaleTo(0.9, 60);
            await b.ScaleTo(1, 90, Easing.SpringOut);
            onTap();
        });
        return area;
    }

    // Casella quadrata delle liste: 22×22 (20 nelle card), raggio 7, bordo 2. Cambia stato sul posto.
    public class CheckBox : Border
    {
        private readonly Path _tick;
        private readonly Color _color;
        public bool Checked { get; private set; }

        public CheckBox(bool done, string colorKey, Action<bool> onChanged, double size = 22)
        {
            _color = C("AccentColor");
            WidthRequest = size;
            HeightRequest = size;
            StrokeShape = new RoundRectangle { CornerRadius = size * 0.25 };
            StrokeThickness = 1.5;
            VerticalOptions = LayoutOptions.Center;
            _tick = Icon(Icons.Check, C("OnAccentColor"), size * 0.72);
            Content = _tick;
            Set(done);
            OnTap(this, () => { Set(!Checked); onChanged(Checked); });
        }

        public void Set(bool done)
        {
            Checked = done;
            BackgroundColor = done ? _color : Colors.Transparent;
            Stroke = new SolidColorBrush(done ? _color : C("BorderStrongColor"));
            _tick.IsVisible = done;
        }
    }

    // Icona lineare in un quadrato con bordo (raggio 6-8). size personalizzabile.
    public static Border IconTile(string icon, string colorKey, double size = 32)
    {
        return new Border
        {
            WidthRequest = size,
            HeightRequest = size,
            BackgroundColor = Colors.Transparent,
            Stroke = new SolidColorBrush(C("BorderStrongColor")),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = size >= 36 ? 8 : 6 },
            Content = Icon(icon, C(colorKey is "AccentColor" or "Accent3Color" ? "TextColor" : colorKey), Math.Round(size * 0.56)),
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Start
        };
    }

    // Bottone quadrato 44 (raggio 10, bordo) con icona lineare 20 (intestazioni)
    public static Grid IconButton(string icon, Action onTap, string dotColorKey = null, bool flat = false)
    {
        var b = new Border
        {
            WidthRequest = 44,
            HeightRequest = 44,
            BackgroundColor = flat ? Colors.Transparent : C("TabBgColor"),
            Stroke = new SolidColorBrush(flat ? Colors.Transparent : C("BorderColor")),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 10 },
            Content = Icon(icon, C("TextColor"), 20)
        };
        var cell = new Grid { WidthRequest = 44, HeightRequest = 44, VerticalOptions = LayoutOptions.Center };
        cell.Add(b);
        if (dotColorKey != null)
            cell.Add(new Border
            {
                WidthRequest = 10, HeightRequest = 10,
                BackgroundColor = C(dotColorKey),
                Stroke = new SolidColorBrush(C("TabBgColor")), StrokeThickness = 2,
                StrokeShape = new RoundRectangle { CornerRadius = 5 },
                HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Start,
                Margin = new Thickness(0, 8, 8, 0)
            });
        Press(cell, onTap);
        return cell;
    }

    // Titolo di sezione: 13 Bold maiuscolo +1,2, conteggio opzionale, link a destra (13 SemiBold + freccia)
    public static Grid SectionTitle(string text, string actionText = null, Action onAction = null, int? count = null)
    {
        var g = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)), MinimumHeightRequest = 44 };
        var left = new HorizontalStackLayout { Spacing = 8, VerticalOptions = LayoutOptions.Center };
        var t = Caps(text, "TextColor");
        t.FontSize = 13;
        t.VerticalOptions = LayoutOptions.Center;
        left.Add(t);
        if (count.HasValue) left.Add(new Label { Text = count.Value.ToString(), FontSize = 13, FontFamily = FontSemi, TextColor = C("SubTextColor"), VerticalOptions = LayoutOptions.Center });
        g.Add(left);
        if (actionText != null)
        {
            if (onAction == null) g.Add(new Label { Text = actionText, FontSize = 13, FontFamily = FontRegular, TextColor = C("SubTextColor"), VerticalOptions = LayoutOptions.Center }, 1);
            else g.Add(LinkLabel(actionText, onAction), 1);
        }
        return g;
    }

    // Link testuale (13 SemiBold, colore del testo) con freccia a destra; icon = icona a sinistra al posto della freccia
    public static View LinkLabel(string text, Action onTap, string icon = null)
    {
        var row = new HorizontalStackLayout { Spacing = 4, MinimumHeightRequest = 44, BackgroundColor = Colors.Transparent };
        if (icon != null) row.Add(Icon(icon, C("TextColor"), 16));
        row.Add(new Label { Text = text, FontSize = 13, FontFamily = FontSemi, TextColor = C("TextColor"), VerticalOptions = LayoutOptions.Center });
        if (icon == null) row.Add(Icon(Icons.Right, C("TextColor"), 16));
        OnTap(row, onTap);
        return row;
    }

    // Pulsante: 44 alto (48 quello a tutta larghezza), raggio 10, testo 14/15 SemiBold.
    // Principale = pieno nell'accento (uno per vista); ghost = secondario trasparente con bordo forte.
    public static Border Button(string text, Action onTap, string icon = null, bool ghost = false, string colorKey = "AccentColor", bool small = false)
    {
        bool neutralGhost = ghost && (colorKey == "AccentColor" || colorKey == "TextColor");
        var fg = ghost ? C(neutralGhost ? "TextColor" : colorKey) : C("OnAccentColor");
        var row = new HorizontalStackLayout { Spacing = 8, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        if (icon != null) row.Add(Icon(icon, fg, 18));
        row.Add(new Label { Text = text, FontSize = small ? 14 : 15, FontFamily = FontSemi, TextColor = fg, VerticalOptions = LayoutOptions.Center });
        var b = new Border
        {
            HeightRequest = small ? 44 : 48,
            Padding = new Thickness(16, 0),
            BackgroundColor = ghost ? Colors.Transparent : C(colorKey),
            Stroke = new SolidColorBrush(ghost ? (neutralGhost ? C("BorderStrongColor") : C(colorKey).WithAlpha(0.5f)) : Colors.Transparent),
            StrokeThickness = ghost ? 1 : 0,
            StrokeShape = new RoundRectangle { CornerRadius = 10 },
            Content = row
        };
        Press(b, onTap);
        return b;
    }

    // Bottone tondo fluttuante "+": 56×56, icona 28
    public static Border Fab(Action onTap, string colorKey = "AccentColor")
    {
        var fab = new Border
        {
            WidthRequest = 56,
            HeightRequest = 56,
            BackgroundColor = C(colorKey),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 14 },
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.End,
            Margin = new Thickness(0, 0, 20, 16),
            Shadow = new Shadow { Brush = new SolidColorBrush(C("FabShadowColor")), Offset = new Point(0, 8), Radius = 20, Opacity = (float)D("FabShadowOpacity", 0.35) },
            Content = Icon(Icons.Plus, C("OnAccentColor"), 24)
        };
        Press(fab, onTap, 0.92);
        return fab;
    }

    // Stato vuoto: icona lineare in un quadrato 44 con bordo, titolo e spiegazione
    public static View Empty(string text, string icon = null, string title = "Ancora niente qui")
    {
        var v = new VerticalStackLayout { Spacing = 6, Padding = new Thickness(24, 28), HorizontalOptions = LayoutOptions.Fill };
        v.Add(new Border
        {
            WidthRequest = 44, HeightRequest = 44,
            BackgroundColor = Colors.Transparent,
            Stroke = new SolidColorBrush(C("BorderStrongColor")),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 10 },
            Content = Icon(icon ?? Icons.Info, C("SubTextColor"), 20),
            HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 0, 0, 6)
        });
        var t = CardTitle(Loc.T(title), 0);
        t.HorizontalTextAlignment = TextAlignment.Center;
        v.Add(t);
        var s = Small(text);
        s.FontFamily = FontRegular;
        s.HorizontalTextAlignment = TextAlignment.Center;
        v.Add(s);
        return v;
    }

    public static Border EmptyCard(string text, string icon = null) => Card(Empty(text, icon), new Thickness(0));

    // Separatore 1 px con margine 16
    public static BoxView Divider() => new() { HeightRequest = 1, Color = C("BorderColor"), Margin = new Thickness(16, 0) };

    // Stacca una vista dal suo contenitore, per poterla riusare dopo una ricostruzione della pagina
    public static void Detach(View v)
    {
        switch (v.Parent)
        {
            case Layout l: l.Remove(v); break;
            case ContentView c: c.Content = null; break;
            case ScrollView s: s.Content = null; break;
            case Border b: b.Content = null; break;
        }
    }

    public static void OnTap(View v, Action action)
    {
        if (action == null) return;
        var t = new TapGestureRecognizer();
        t.Tapped += (s, e) => action();
        v.GestureRecognizers.Add(t);
    }

    // Tocco con il piccolo "rimbalzo" del design (scala 0,96 per 120 ms)
    public static void Press(View v, Action action, double scale = 0.96)
    {
        if (action == null) return;
        OnTap(v, async () =>
        {
            await v.ScaleTo(scale, 60, Easing.CubicOut);
            await v.ScaleTo(1, 60, Easing.CubicIn);
            action();
        });
    }

    // Campo di testo: 52 alto, raggio 10, superficie rialzata con bordo forte (accento quando ha il cursore)
    public static Border Field(View input)
    {
        var b = new Border
        {
            Content = input,
            MinimumHeightRequest = 52,
            Padding = new Thickness(16, 2),
            BackgroundColor = C("FieldBgColor"),
            Stroke = new SolidColorBrush(C("BorderStrongColor")),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 10 }
        };
        // Bordo colorato quando un campo dentro ha il cursore
        void Hook(View v)
        {
            if (v is InputView iv)
            {
                iv.Focused += (s, e) => b.Stroke = new SolidColorBrush(C("AccentColor"));
                iv.Unfocused += (s, e) => b.Stroke = new SolidColorBrush(C("BorderStrongColor"));
            }
            else if (v is Layout l) foreach (var c in l.Children.OfType<View>()) Hook(c);
        }
        Hook(input);
        return b;
    }

    // Entry senza decorazioni (va dentro Field)
    public static Entry Entry(string placeholder, string text = null, double size = 16, Keyboard keyboard = null) => new()
    {
        Placeholder = placeholder,
        Text = text,
        FontSize = size,
        FontFamily = FontRegular,
        TextColor = C("TextColor"),
        PlaceholderColor = C("SubTextColor"),
        BackgroundColor = Colors.Transparent,
        Keyboard = keyboard ?? Keyboard.Text,
        VerticalOptions = LayoutOptions.Center
    };

    // Riga impostazione: testo + interruttore (56 alta)
    public static Border SettingRow(string text, bool value, Action<bool> onChanged, out Switch sw, out Label label)
    {
        sw = new Switch { IsToggled = value, OnColor = C("AccentColor"), ThumbColor = Colors.White, VerticalOptions = LayoutOptions.Center };
        sw.Toggled += (s, e) => onChanged(e.Value);
        var g = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)), MinimumHeightRequest = 56, Padding = new Thickness(16, 4) };
        label = Body("");
        label.Text = text;
        label.FontFamily = FontSemi;
        label.VerticalOptions = LayoutOptions.Center;
        g.Add(label);
        g.Add(sw, 1);
        var card = Card(g, new Thickness(0), radius: 12);
        card.Shadow = null;
        card.Stroke = new SolidColorBrush(C("BorderColor"));
        return card;
    }

    // ---------- Agenda unificata ----------
    public class AgendaItem
    {
        public DateTime Date;
        public TimeSpan? Time;
        public bool AllDay;
        public string Title;
        public string Subtitle;
        public string ColorKey;
        public string Icon;
        public string Amount;     // es. "−85 €" (solo pagamenti)
        public object Source;
        public bool Done;
    }

    public static List<AgendaItem> BuildAgenda(List<Note> notes, List<TaskItem> tasks, List<CalendarEvent> events, DateTime from, DateTime to, List<Ordine> ordini = null)
    {
        var list = new List<AgendaItem>();

        // 3.0: pacchi nel giorno previsto (quelli consegnati restano nel giorno della consegna)
        foreach (var o in (ordini ?? new()).Where(o => (o.ConsegnatoIl ?? o.ConsegnaPrevista) is DateTime d && d.Date >= from.Date && d.Date <= to.Date))
            list.Add(new AgendaItem
            {
                Date = (o.ConsegnatoIl ?? o.ConsegnaPrevista).Value.Date, AllDay = true, Title = o.Prodotto,
                Subtitle = $"{o.Negozio} · {o.StatoTesto}" + (string.IsNullOrWhiteSpace(o.FasciaOraria) || o.Consegnato ? "" : $" · {o.FasciaOraria}"),
                ColorKey = o.InRitardo ? "WarnColor" : "Cat1", Icon = Icons.Box, Source = o, Done = o.Consegnato
            });

        foreach (var e in events.Where(e => e.Date.Date >= from.Date && e.Date.Date <= to.Date))
            list.Add(new AgendaItem
            {
                Date = e.Date.Date, Time = e.AllDay ? null : e.StartTime, AllDay = e.AllDay, Title = e.Title,
                Subtitle = e.AllDay ? (string.IsNullOrWhiteSpace(e.Location) ? Loc.T("Evento") : e.Location)
                                    : $"{e.StartTime:hh\\:mm}–{e.EndTime:hh\\:mm}" + (string.IsNullOrWhiteSpace(e.Location) ? "" : $" · {e.Location}"),
                ColorKey = e.ColorKey ?? "Accent5Color", Icon = Icons.Calendar, Source = e
            });

        foreach (var t in tasks.Where(t => t.DueDate.HasValue && t.DueDate.Value.Date >= from.Date && t.DueDate.Value.Date <= to.Date))
            list.Add(new AgendaItem
            {
                Date = t.DueDate.Value.Date, Title = t.Title, Subtitle = Loc.F("Task · priorità {0}", PriorityText(t.Priority).ToLower()),
                ColorKey = "AccentColor", Icon = Icons.Task, Source = t, Done = t.IsDone
            });

        foreach (var n in notes.Where(n => n.DueDate.HasValue && n.DueDate.Value.Date >= from.Date && n.DueDate.Value.Date <= to.Date))
        {
            if (n.IsPayment && n.IsPaid) continue;
            string sub = Loc.T(n.Type);
            if (n.IsPayment)
            {
                var cat = Category.Of(n);
                sub = cat.Label + (n.IsMonthly ? Loc.T(" · Mensile") : "");
            }
            list.Add(new AgendaItem
            {
                Date = n.DueDate.Value.Date,
                Time = !n.IsPayment && n.DueDate.Value.TimeOfDay != TimeSpan.Zero ? n.DueDate.Value.TimeOfDay : null,
                Title = n.Title,
                Subtitle = sub,
                ColorKey = n.IsPayment ? n.PaymentColorKey : TypeColorKey(n.Type),
                Icon = TypeIcon(n.Type),
                Amount = n.IsPayment ? Euro(n.IsIncome ? n.Amount : -n.Amount, true) : null,
                Source = n
            });
        }

        // Ordine nel giorno: eventi per orario, poi pagamenti, poi task
        int Rank(AgendaItem i) => i.Source is CalendarEvent ? 0 : i.Source is Ordine ? 1 : i.Source is Note { IsPayment: false } ? 0 : i.Source is Note ? 1 : 2;
        return list.OrderBy(i => i.Date).ThenBy(Rank).ThenBy(i => i.Time ?? TimeSpan.Zero).ToList();
    }

    // Riga dell'agenda (3.0): orario 44 · barra 3 nel colore del tipo · titolo 15 e meta 13 · importo a destra
    public static View AgendaRow(AgendaItem item, Action onTap)
    {
        var g = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(44), new ColumnDefinition(3), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)),
            ColumnSpacing = 12,
            Padding = new Thickness(16, 8),
            MinimumHeightRequest = 52,
            BackgroundColor = Colors.Transparent,
            Opacity = item.Done ? 0.6 : 1
        };
        string time = item.Time.HasValue ? item.Time.Value.ToString(@"hh\:mm") : item.AllDay ? Loc.T("Tutto il giorno") : "";
        var tm = new Label { Text = time, FontSize = item.AllDay ? 11 : 14, FontFamily = FontSemi, TextColor = C("SubTextColor"), VerticalOptions = LayoutOptions.Center, LineBreakMode = LineBreakMode.WordWrap };
        g.Add(tm);
        string barKey = item.ColorKey is "AccentColor" or "Accent3Color" or "TextColor" ? "BorderStrongColor" : item.ColorKey;
        g.Add(new BoxView { Color = C(barKey), CornerRadius = 1.5, WidthRequest = 3, VerticalOptions = LayoutOptions.Fill }, 1);

        var stack = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };
        var title = RowTitle(item.Title);
        if (item.Done) title.TextDecorations = TextDecorations.Strikethrough;
        stack.Add(title);
        stack.Add(Meta(item.Subtitle));
        g.Add(stack, 2);

        if (item.Amount != null)
            g.Add(new Label { Text = item.Amount, FontSize = 15, FontFamily = FontBold, TextColor = C(item.ColorKey == "Accent3Color" ? "Accent3Color" : "TextColor"), VerticalOptions = LayoutOptions.Center }, 3);
        Press(g, onTap, 0.98);
        return g;
    }

    // Lista di righe in una card, separate da linee sottili
    public static Border ListCard(IEnumerable<View> rows, double vPad = 4)
    {
        var v = new VerticalStackLayout();
        bool first = true;
        foreach (var r in rows)
        {
            if (!first) v.Add(Divider());
            v.Add(r);
            first = false;
        }
        return Card(v, new Thickness(0, vPad));
    }
}
