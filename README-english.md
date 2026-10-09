YourLittleFriend is a little buddy in your pc's notch or in a bubble (windows). it opens when you hover over it with the mouse.

- **home**: greeting + weather; if a song starts playing it shows the cover, title and controls, and the colors follow the cover
- **chat**: you talk to a local llm (ollama) that can also control the pc
- **+**: drag in a file, write what to do with it and it answers you
- **file holder** (the "cup holder", the tab with the little tray): drag files or text onto the notch and they stay there, sorted into slots (icon or thumbnail for files, a snippet for text). you can drag them around wherever you want, even after restarting the pc. double click opens the file or copies the text, ✕ removes one item, 🗑 clears everything. files are not copied: the file holder only remembers where they are
- answers arrive in real time, word by word, with a "smoke" effect that fades away (imessage style)
- the little guy is animated: he blinks, wanders around the notch, works (dots and reading eyes) while the ai answers, smiles when it's done (or when you open the notch) and dances if there's music
- **calculator** with history (it's saved): type the expression and see the result as you type, Enter saves it. it knows + − × ÷ ^ % !, parentheses, pi, e, ans (the last result), sqrt, sin, cos, tan, ln, log, abs, round... and degrees/radians. if the result is 67, 69 or 420 the little guy puts on sunglasses and gives a thumbs up 😎👍
- **translator** with DeepL and/or LibreTranslate: put in the key (or the server address) from the settings, pick the languages and press Enter
- **wikipedia**: type something and you get the title, description, summary and photo; scroll down to read the whole article
- **bubble shape**: from the settings you can replace the notch with a floating bubble that bobs around, swells when you hover over it and makes a wave while the ai is working. one click opens it and with Ctrl + wheel you resize it
- **expandable panel**: with the ⤢ button at the top the panel becomes much bigger (with the same animation as the notch) and you adjust it by dragging the bottom right corner: it remembers the size. the calculator and Wikipedia expand on their own; the chat you expand yourself
- **move it wherever you want**: hold down on an empty spot (even on the open panel or on the little guy) and drag: notch, panel and bubble move and remember where you left them, even after turning the pc back on. the notch snaps by itself to the center and top edge of the screen; if you detach it, it becomes a pill with its own outline. to put it back: right click → *Reset position* (or from the settings)
- **magnifying glass**: double click on the little guy (or on the bubble) and it becomes a lens that follows the cursor and magnifies the app, which stays open and visible, along with whatever is behind it (wheel = zoom). holding the button down you underline in yellow, double click or right click to remove it. color, size and zoom can be changed from the settings. on protected screens (windows permission prompts) the lens can't see
- **wikipedia** with gallery: besides the main photo you see the other images from the article
- **calculator** with a real keyboard (digits, operations, scientific functions, DEG/RAD) when the panel is expanded
- **every feature can be turned on and off** from the settings, except the home
- **weather**: in the home you'll find a widget with an animated icon (spinning sun, rain, snow, lightning...), temperature, description, min and max. the little guy reacts to the weather: with sun he puts on sunglasses, with rain he opens an umbrella (and with strong wind he flies away hanging from the umbrella), with a thunderstorm he shakes with fear, when it's cloudy he shrugs ("dunno"), with snow or cold he puts on a scarf, at night he sleeps
- **web search**: if you ask for news or something it doesn't know, the ai searches the internet on its own (duckduckgo, with wikipedia as a fallback) and answers based on the results, without asking you for confirmation. google doesn't allow reading its results reliably, which is why it's not used. it can be turned off from the settings
- **look**: from the settings you pick a theme (night, day, ocean, sunset, forest, candy), the main color, the little guy's color (even in hex), the transparency and a colored outline for the notch. everything changes on the fly and if you cancel it goes back to how it was
- right click (or ⚙) to quit

## installation (recommended)
download `yourlittlefriend-setup-x.y.z.exe` from the [releases](../../releases) and run it. you don't need administrator permissions and you don't have to install .net.

1. choose the language (italiano / english)
2. choose the folder (by default `AppData\Roaming\yourlittlefriend`)
3. choose whether to start it when the pc starts
4. choose whether to create the desktop icon (it's also in the start menu)

**updating**: download the new installer and run it, without uninstalling anything. it closes the open app by itself, installs over the old version (same folder), remembers if you had autostart and doesn't touch your settings or the file holder. if you accidentally try to install a version older than the one you have, it warns you.

on first launch the app asks you for:

1. your **name**
2. your **city** for the weather (you can skip)
3. the **ai**: the guided setup immediately checks whether ollama is already installed and which models you already have: if it finds one you like, you use it right away, without downloading anything. otherwise it downloads ollama by itself (without leaving windows open) and the model. you can use the recommended model or type your own: the app checks whether your pc (ram and disk space) can handle it. you can also skip and do it later

in the settings there's also the **Ollama** section: you see whether it's on, you start or stop it (with a progress bar while it starts), you see the status of the model in use (installed / in memory), you load the model into memory to get the first answer right away and you unload the active models from memory. it refreshes by itself every two seconds.

everything is changed from the **settings** (⚙ → settings): name, city, language, model, autostart and the button to **uninstall**. the data lives in `%APPDATA%\yourlittlefriend\settings.json`.

## running from source
you need the [.net 8 sdk](https://dotnet.microsoft.com/download) on windows 10/11.

    dotnet run

## creating a release (for whoever maintains the project)
the installer is built with github actions (`.github/workflows/build.yml`, it uses [inno setup](https://jrsoftware.org/isinfo.php) with `installer/yourlittlefriend.iss`).
every pull request produces the installer as an artifact so you can try it. to publish a release, a tag is enough:

    git tag 1.1.3
    git push origin 1.1.3

the tag may or may not have the "v" in front. if you create the release by hand from github, the build attaches the installer by itself (with the right version) without touching the title and notes. a tag with a hyphen (`-beta`) created by the build comes out as a **pre-release**. the version shows up at the bottom of the settings.

## ❤️‍🩹 Help us
got suggestions or tips? go wild! open an issue on this repo and tell us everything. we'll do our best to bring your ideas to life. if you can code (even using AI agents) open a pull request (PR)


## roadmap
### byollm
bring your own LLM to yourlittlefriend: the ollama auto-installer is already there (settings). coming soon: connect to the APIs of some AIs (vercel, openai, anthropic) and use paid premium models.

### more animations for the little guy
new expressions and reactions to make yourlittlefriend even cuter :)

### guides
## api key deepl
you can follow this guide: [deepl api key guide](https://support.deepl.com/hc/en-us/articles/360020695820-API-key-for-DeepL-API),
when it asks you for the plan you can click on the free one, it will ask you name, surname and address but you can put it fake information,
then go to the notch > right click > settings > scroll all the way down and then find "DeepL API Key" and put it there, then restart the app.

## local llm (ollama)

1. install ollama from https://ollama.com/download
2. in the terminal: `ollama pull qwen3.5:4b` (about 3 gb, runs well even without a graphics card)
3. leave ollama running in the background

the installation of ollama and of the model is guided by the app (first launch or settings). if you want to do it by hand: install ollama and then `ollama pull <model>`; the model is chosen from the settings.

## what it can do from the chat

- volume (set, raise/lower, mute), play/pause/next/previous
- searches for a song on spotify and plays it
- opens apps from the list (`Apps` in `Actions.cs`), searches on google, locks/suspends/shuts down/restarts the pc

sensitive actions (opening apps, browser, shutdown etc.) first ask for confirmation with a popup that shows the steps.
the model can't run free-form commands: only the actions written in `Actions.cs`.
files dragged into the + tab are read but can't trigger actions.

## spotify (automatic playback)

you need a premium account. without these steps the chat only opens the search in the app.

1. go to https://developer.spotify.com/dashboard and create an app
2. as redirect uri put `http://127.0.0.1:8888/`
3. copy the client id and set it: `setx SPOTIFY_CLIENT_ID "your-client-id"` (then reopen the terminal)
4. the first time the browser opens for the login, then it remembers the access
