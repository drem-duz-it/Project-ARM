namespace ARMusic.Views;

// Elenco delle versioni mostrato nel foglio "Novità" (il primo elemento è la versione attuale)
public static class Novita
{
    public record Versione(string Titolo, string[] Punti);

    public static readonly Versione[] Versioni =
    {
        new("ARM 3.0 · Ordini in arrivo", new[] { "Nuova pagina Oggi: quattro indicatori in alto, poi ordini, agenda, task, pagamenti, musica e note fissate, disposti su griglia.", "Ordini Amazon letti da Gmail sul PC: prodotto, stato della spedizione in 4 tappe, consegna prevista e numero d'ordine. Arrivano anche su telefono e tablet con la sincronizzazione.", "I pacchi compaiono nel Calendario nel giorno previsto, nel widget e in una notifica alle 9:00 con «Ricevuto».", "Stile più sobrio: colori neutri con un solo accento, icone lineari, niente emoji.", "Barra laterale con «Scrivi al volo» (Ctrl K) e mini player con la barra di avanzamento." }),
        new("ARM 2.5 · Sempre a portata di mano", new[] { "Nuova estetica in tutta l'app: font Plus Jakarta Sans, card più morbide, colori con un significato preciso e i temi Chiaro e Classico rifatti da capo. Su Windows, a finestra larga, i tab diventano una barra laterale.", "Scrivi al volo: in cima a Oggi scrivi una frase normale («dentista domani alle 15:30», «bollo auto 85€ venerdì», «Marco mi deve 20 euro entro il 15») e ARM capisce da solo se è un task, un evento, un pagamento o un promemoria, con data, ora, importo e categoria.", "Widget «ARM · Oggi» per la home del telefono: le cose di oggi, i task da spuntare con un tocco e il campo per scrivere al volo.", "Pulsanti nelle notifiche: Fatto, Pagato, Ricevuto, Domani, Tra 30 minuti… senza aprire l'app.", "Liste con le spunte dentro le note, spuntabili anche direttamente dalla card.", "Categorie dei pagamenti (casa, bollette, spesa, abbonamenti…) e il grafico «Soldi del mese»: uscite per categoria e andamento degli ultimi 6 mesi." }),
        new("ARM 2.4 · Testi e playlist casuali", new[] { "Nel player il nuovo pulsante «Testo» mostra le parole della canzone al posto della copertina: la riga che si canta si illumina e scorre da sola, e toccando una riga si salta lì. Se non ci sono i tempi vedi il testo intero.", "I testi vengono cercati online (servizio LRCLIB: gli arrivano solo artista, titolo e durata) e restano salvati sul dispositivo.", "Nelle playlist c'è il pulsante «Casuale»: parte da un brano a caso, non più dal primo.", "Il casuale ora ha memoria: non ti ripropone i brani sentiti da poco e, andando avanti, gira sempre meno a caso. «Precedente» in casuale torna al brano di prima.", "Su Windows, più stabilità nella pagina Oggi." }),
        new("ARM 2.3.1 · La musica si abbassa", new[] { "Su Windows, quando un assistente esterno parla la musica scende di volume e poi torna com'era.", "Il telefono non cambia." }),
        new("ARM 2.3 · Assistente e musica", new[] { "Su Windows l'assistente esterno può anche far suonare le tue playlist e i brani scaricati, metterli in pausa, cambiare brano e regolare il volume.", "L'assistente ritrova le note cercando nel titolo e nel testo.", "Il telefono non cambia: le modifiche arrivano con la sincronizzazione." }),
        new("ARM 2.2 · Assistente esterno", new[] { "Su Windows ARM ora si lascia usare da un assistente esterno tramite un'API locale: eventi, task, note e pagamenti si gestiscono anche a voce o a testo.", "Il collegamento è solo locale (127.0.0.1) e protetto da una chiave; ciò che l'assistente scrive si sincronizza con il telefono come ogni altra modifica." }),
        new("ARM 2.1.2", new[] { "Streaming da YouTube temporaneamente disattivato: YouTube ha cambiato il modo in cui fornisce l'audio e lo stiamo aggiustando. I brani scaricati si ascoltano normalmente.", "Importazione da Spotify già collegata, senza inserire codici.", "Messaggio chiaro quando Spotify richiede l'abbonamento Premium." }),
        new("ARM 2.1.1", new[] { "Pagamenti: debiti (da pagare) e crediti (da ricevere, con la data entro cui arriveranno), con saldo previsto.", "Più telefoni e tablet collegati allo stesso PC.", "Le copertine delle playlist passano tra i dispositivi (e ora si vedono anche su Windows).", "ARMusic: importazione di playlist da Spotify.", "Android: pulsanti della musica sempre allineati a ciò che sta suonando e notifica con controlli funzionante." }),
        new("ARM 2.1 · Sincronizzazione", new[] { "L'app diventa ARM, con ARMusic come sezione dedicata alla musica.", "Sincronizzazione via Wi-Fi tra telefono e PC, con unione automatica delle modifiche.", "Se la stessa cosa cambia su entrambi i dispositivi, scegli tu quale versione tenere.", "I brani scaricati passano da un dispositivo all'altro via Wi-Fi.", "Sul telefono i brani si salvano nella cartella Musica (Music/ARM).", "Notifiche per eventi, task, pagamenti e promemoria.", "Corretto il crash cambiando tipo di nota." }),
        new("Versione 2.0 · Spazio di lavoro", new[] { "Nuova pagina Oggi con riepilogo della giornata.", "Note colorate con pagamenti, promemoria e idee.", "Pagamenti mensili ricorrenti con totale da pagare.", "Task con priorità, scadenze e aggiunta rapida.", "Calendario mensile che unisce eventi, task e scadenze.", "Nuovo tema Chiaro e musica raccolta in un'unica sezione." }),
        new("Versione 1.2", new[] { "Aggiunto pannello Aggiornamenti (Changelog) nella Home.", "Corretto bug del player nelle notifiche Android.", "Aggiunto sistema sperimentale di Cross-Save (Sincronizzazione Cloud)." }),
        new("Versione 1.1", new[] { "Introdotto effetto header dinamico a comparsa (stile Spotify) nelle playlist.", "Aggiunto menu contestuale (3 puntini) per ogni brano.", "Possibilità di rimuovere o rinominare globalmente i brani." }),
        new("Versione 1.0", new[] { "Rilascio iniziale di A.R. Music (oggi ARMusic, la sezione musica di ARM).", "Player audio a tutto schermo.", "Ricerca, download da YouTube e salvataggio locale." }),
    };

    // Contenuto del foglio: per ogni versione titolo (con «Nuova» sulla prima) ed elenco puntato
    public static View Build()
    {
        var v = new VerticalStackLayout { Spacing = 18 };
        for (int i = 0; i < Versioni.Length; i++)
        {
            var ver = Versioni[i];
            var block = new VerticalStackLayout { Spacing = 8 };
            var head = new HorizontalStackLayout { Spacing = 8 };
            var t = Ui.CardTitle(Loc.T(ver.Titolo), 0);
            t.VerticalOptions = LayoutOptions.Center;
            head.Add(t);
            if (i == 0)
            {
                var badge = Ui.Badge(Loc.T("Nuova"), "AccentColor");
                badge.BackgroundColor = Ui.C("AccentColor");
                ((Label)((HorizontalStackLayout)badge.Content).Children.Last()).TextColor = Ui.C("OnAccentColor");
                head.Add(badge);
            }
            block.Add(head);
            foreach (var p in ver.Punti)
            {
                var row = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(14), new ColumnDefinition(GridLength.Star)) };
                row.Add(Ui.Small("•", "TextColor"));
                var l = Ui.Small(Loc.T(p), "TextColor");
                l.FontFamily = Ui.FontRegular;
                l.LineHeight = 1.25;
                row.Add(l, 1);
                block.Add(row);
            }
            v.Add(block);
        }
        return v;
    }
}
