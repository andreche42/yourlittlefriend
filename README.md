# yourlittlefriend

un piccolo amico nel notch del pc (windows). si apre quando ci passi sopra col mouse.

- **home**: saluto + meteo; se parte una canzone mostra copertina, titolo, controlli e i colori seguono la copertina
- **chat**: parli con un llm locale (ollama) che può anche controllare il pc
- **+**: trascina un file, scrivi cosa farne e ti risponde
- tasto destro (o ⚙) per uscire

## avvio

serve il [.net 8 sdk](https://dotnet.microsoft.com/download) su windows 10/11.

    dotnet run

## llm locale (ollama)

1. installa ollama da https://ollama.com/download
2. nel terminale: `ollama pull qwen3.5:4b` (circa 3 gb, gira bene anche senza scheda video)
3. lascia ollama acceso in background

il modello si cambia in `Llm.cs` (`Model`).

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
