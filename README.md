# yourlittlefriend
![Project Logo](https://i.ibb.co/pB2J6tTk/Group-1.png%22)

![AGPLV3 License](https://img.shields.io/badge/License-AGPL_v3-blue.svg) ![Spotify](https://img.shields.io/badge/Spotify-%231ED760.svg?style=for-the-badge&logo=spotify&logoColor=white) ![Ollama](https://img.shields.io/badge/ollama-%23000000.svg?style=for-the-badge&logo=ollama&logoColor=white) ![C#](https://img.shields.io/badge/C%23-239120?style=flat&logo=unity&logoColor=purple)

un piccolo amico nel notch del pc (windows). si apre quando ci passi sopra col mouse.

- **home**: saluto + meteo; se parte una canzone mostra copertina, titolo, controlli e i colori seguono la copertina
- **chat**: parli con un llm locale (ollama) che può anche controllare il pc
- **+**: trascina un file, scrivi cosa farne e ti risponde
- **portafile** (il "portabicchieri", la scheda con la vaschetta): trascina sul notch file o testo e restano lì, ordinati in caselle (icona o miniatura per i file, un pezzo di testo per il testo). li puoi ritrascinare dove vuoi, anche dopo aver riavviato il pc. doppio click apre il file o copia il testo, ✕ toglie un elemento, 🗑 svuota tutto. i file non vengono copiati: il portafile ricorda solo dove stanno
- le risposte arrivano in tempo reale, parola per parola, con un effetto "fumo" che si dirada (stile imessage)
- l'omino è animato: sbatte gli occhi, va a spasso nel notch, lavora (puntini e occhi che leggono) mentre l'ia risponde, sorride quando ha finito (o quando apri il notch) e balla se c'è musica
- **ricerca sul web**: se chiedi una notizia o qualcosa che non sa, l'ia cerca da sola su internet (duckduckgo, con wikipedia come riserva) e risponde in base ai risultati, senza chiederti conferma. google non permette di leggere i suoi risultati in modo affidabile, per questo non si usa. si può spegnere dalle impostazioni
- **aspetto**: dalle impostazioni scegli un tema (notte, giorno, oceano, tramonto, foresta, caramella), il colore principale, il colore dell'omino (anche in esadecimale), la trasparenza e un contorno colorato per il notch. cambia tutto al volo e se annulli torna com'era
- tasto destro (o ⚙) per uscire

## installazione (consigliata)
scarica `yourlittlefriend-setup-x.y.z.exe` dalle [release](../../releases) e avvialo. non servono permessi di amministratore e non devi installare .net.

1. scegli la lingua (italiano / english)
2. scegli la cartella (di base `AppData\Roaming\yourlittlefriend`)
3. scegli se farlo partire all'avvio del pc

**aggiornare**: scarica il nuovo installer e avvialo, senza disinstallare niente. chiude da solo l'app aperta, si installa sopra la versione vecchia (stessa cartella), ricorda se avevi l'avvio automatico e non tocca le tue impostazioni né il portafile. se per sbaglio provi a installare una versione più vecchia di quella che hai, ti avvisa.

al primo avvio l'app ti chiede:

1. il tuo **nome**
2. la tua **città** per il meteo (puoi saltare)
3. l'**ia**: l'installazione guidata scarica da sola ollama (senza lasciare finestre aperte) e il modello. puoi usare il modello consigliato oppure scriverne uno tuo: l'app controlla se il tuo pc (ram e spazio su disco) ce la fa. puoi anche saltare e farlo dopo

tutto si cambia dalle **impostazioni** (⚙ → impostazioni): nome, città, lingua, modello, avvio automatico e il pulsante per **disinstallare**. i dati stanno in `%APPDATA%\yourlittlefriend\settings.json`.

## avvio da sorgente
serve il [.net 8 sdk](https://dotnet.microsoft.com/download) su windows 10/11.

    dotnet run

## creare una release (per chi mantiene il progetto)
l'installer si costruisce con github actions (`.github/workflows/build.yml`, usa [inno setup](https://jrsoftware.org/isinfo.php) con `installer/yourlittlefriend.iss`).
ogni pull request produce l'installer come artifact per provarlo. per pubblicare una release basta un tag:

    git tag v1.0.0
    git push origin v1.0.0

## avviso ⚠️
l'app è ancora in sviluppo e non è ancora in uno stato utilizzabile, ovvero può fare poche cose utili che puoi tranquillamente fare come faresti. nei prossimi aggiornamenti l'app (dovrebbe) migliorare!
hai suggerimenti o consigli? sbizzarrisciti! apri un issue su questa repo e dicci tutto. faremmo il possibile per portare le tue idee in vita. se sai programmare (anche usando agenti AI) apri una pull request (PR)


## roadmap
### byollm
porta la tua LLM su yourlittlefriend: l'autoinstaller di ollama c'è già (impostazioni). in arrivo: collegati alle API di qualche AI (vercel, openai, anthropic) e usa modelli premium a pagamento.

### più animazioni del omino
nuove espressioni e reazioni per rendere yourlittlefriend ancora più carino :)

### guide
## llm locale (ollama)

1. installa ollama da https://ollama.com/download
2. nel terminale: `ollama pull qwen3.5:4b` (circa 3 gb, gira bene anche senza scheda video)
3. lascia ollama acceso in background

l'installazione di ollama e del modello è guidata dall'app (primo avvio o impostazioni). se vuoi farlo a mano: installa ollama e poi `ollama pull <modello>`; il modello si sceglie dalle impostazioni.

## cosa può fare dalla chat

- volume (imposta, alza/abbassa, muta), play/pausa/avanti/indietro
- cerca una canzone su spotify e la riproduce
- apre app della lista (`Apps` in `Actions.cs`), cerca su google, blocca/sospende/spegne/riavvia il pc

le azioni sensibili (aprire app, browser, spegnimento ecc.) chiedono prima conferma con un popup che mostra i passaggi.
il modello non può eseguire comandi liberi: solo le azioni scritte in `Actions.cs`.
i file trascinati nella scheda + vengono letti ma non possono far partire azioni.

## spotify (riproduzione automatica)

serve un account premium. senza questi passaggi la chat apre solo la ricerca nell'app.

1. vai su https://developer.spotify.com/dashboard e crea un'app
2. come redirect uri metti `http://127.0.0.1:8888/`
3. copia il client id e impostalo: `setx SPOTIFY_CLIENT_ID "il-tuo-client-id"` (poi riapri il terminale)
4. la prima volta si apre il browser per il login, poi ricorda l'accesso

## ia online (solo immagini)

per le immagini trascinate nella scheda + serve `ANTHROPIC_API_KEY` (il modello locale legge solo testo).
