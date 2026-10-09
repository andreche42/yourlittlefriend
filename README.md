# yourlittlefriend
![Project Logo](https://i.ibb.co/pB2J6tTk/Group-1.png%22)

![AGPLV3 License](https://img.shields.io/badge/License-AGPL_v3-blue.svg) ![Spotify](https://img.shields.io/badge/Spotify-%231ED760.svg?style=for-the-badge&logo=spotify&logoColor=white) ![Ollama](https://img.shields.io/badge/ollama-%23000000.svg?style=for-the-badge&logo=ollama&logoColor=white) ![C#](https://img.shields.io/badge/C%23-239120?style=flat&logo=unity&logoColor=purple)

un piccolo amico nel notch del pc (windows). si apre quando ci passi sopra col mouse.

- **home**: saluto + meteo; se parte una canzone mostra copertina, titolo, controlli e i colori seguono la copertina
- **chat**: parli con un llm locale (ollama) che può anche controllare il pc
- **+**: trascina un file, scrivi cosa farne e ti risponde
- tasto destro (o ⚙) per uscire

## avvio
se vuoi scaricare i binaries e avviarti il programma da solo:
serve il [.net 8 sdk](https://dotnet.microsoft.com/download) su windows 10/11.

    dotnet run

in alternativa, puoi prendere le build stabili dalle release. le build stabili hanno installer e partono al avvio del pc. sono fatte in modo set-and-forget. ricordati: puoi disinstallare l'app quando vuoi o disabilitare l'avvio automatico da task manager!

## avviso ⚠️
l'app è ancora in sviluppo e non è ancora in uno stato utilizzabile, ovvero può fare poche cose utili che puoi tranquillamente fare come faresti. nei prossimi aggiornamenti l'app (dovrebbe) migliorare!
hai suggerimenti o consigli? sbizzarrisciti! apri un issue su questa repo e dicci tutto. faremmo il possibile per portare le tue idee in vita. se sai programmare (anche usando agenti AI) apri una pull request (PR)


## roadmap
### holder
holder ti permetterà di trascinare dei file sulla notch in alto. i file rimangono li e puoi ri-trascinarli dove vuoi. si integra con le api di sistema per garantire che funzioni su tutte le app.

### menu impostazioni migliorato
gestisci città per il meteo, cambia nome, cambia lingua, e personalizza l'app a tuo piacere, anche i colori!

### byollm
porta la tua LLM su yourlittlefriend: dal menu impostazioni trovi l'autoinstaller di ollama per scaricare LLM da usare per yourlittlefriend. in alternativa, collegati alle API di qualche AI (vercel, openai, anthropic) e usa modelli premium a pagamento.

### animazioni del omino
yourlittlefriend ancora più carino :)

---------
guide:

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
