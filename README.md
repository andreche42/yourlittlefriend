# yourlittlefriend

un piccolo amico nel notch del pc (windows). si apre quando ci passi sopra col mouse.

- **home**: saluto + meteo; se parte una canzone mostra copertina, titolo, controlli e i colori seguono la copertina
- **chat**: scrivi e risponde un'ia veloce
- **+**: trascina un file, scrivi cosa farne e ti risponde
- tasto destro (o ⚙) per uscire

## avvio

serve il [.net 8 sdk](https://dotnet.microsoft.com/download) su windows 10/11.

    dotnet run

per la parte ia imposta la chiave prima di avviare:

    setx ANTHROPIC_API_KEY "la-tua-chiave"

(poi riapri il terminale). senza chiave tutto funziona tranne chat e file.

## da cambiare

in `MainWindow.xaml.cs`, in alto: coordinate del meteo (`Lat`, `Lon`) e modello (`Model`).
