# YourLittleFriend 🤖
![Project Logo](https://i.postimg.cc/nLwcdMWd/210751.jpg)

![AGPLV3 License](https://img.shields.io/badge/License-AGPL_v3-blue.svg) ![Windows 10/11](https://camo.githubusercontent.com/d3b3df0bdfd988f32acfbcc61353fed15852a6051287f0ea9ec1a3ad6d30b6ba/68747470733a2f2f696d672e736869656c64732e696f2f62616467652f57696e646f77732d313025324631312d3030373844343f6c6f676f3d77696e646f7773266c6f676f436f6c6f723d7768697465) ![C#](https://img.shields.io/badge/C%23-239120?style=flat&logo=unity&logoColor=purple) ![Ollama](https://img.shields.io/badge/ollama-%23000000.svg?style=for-the-badge&logo=ollama&logoColor=white)

YourLittleFriend is a little buddy in your pc's notch or in a bubble (windows). it opens when you hover over it with the mouse.

Italian version: [README.md](https://github.com/andreche42/yourlittlefriend/blob/main/README.md)

- **home**: greeting + weather; if a song starts playing it shows the cover, title and controls, and the colors follow the cover
- **chat**: you talk to an llm on your pc (ollama) or to the service you want, using your own api key (openai, gemini, claude, nvidia, groq...): it can also control the pc
- **+**: drag in a file, write what to do with it and it answers you
- **attached text files**: in the **+** tab you attach a text file, write what you want to know and the ai answers by reading it. (for now only through online ai)
- **file holder** (the "cup holder", the tab with the little tray): drag files or text onto the notch and they stay there, sorted into slots (icon or thumbnail for files, a snippet for text). you can drag them around wherever you want, even after restarting the pc. double click opens the file or copies the text, ✕ removes an item, 🗑 clears everything. files are not copied: the file holder only remembers where they are
- answers arrive in real time, word by word, with a "smoke" effect that fades away (imessage style)
- the little guy is animated: he blinks, wanders around the notch, works (dots and reading eyes) while the ai answers, smiles when it's done (or when you open the notch) and dances if there's music
- **calculator** with history (it's saved): type the expression and see the result as you type, Enter saves it. it knows + − × ÷ ^ % !, parentheses, pi, e, ans (the last result), sqrt, sin, cos, tan, ln, log, abs, round... and degrees/radians. if the result is 67 or 69 the little guy puts on sunglasses and gives a thumbs up 😎👍
- **translator** with DeepL and/or LibreTranslate: put in the key (or the server address) from the settings, pick the languages and press Enter
- **wikipedia**: type something and you get the title, description, summary and photo; scroll down to read the whole article
- **bubble shape**: from the settings you can replace the notch with a floating bubble that bobs around, swells when you hover over it and makes a wave while the ai is working. one click opens it and with Ctrl + wheel you resize it
- **expandable panel**: with the ⤢ button at the top the panel becomes much bigger (with the same animation as the notch) and you adjust it by dragging the bottom right corner: it remembers the size. the calculator and Wikipedia expand on their own; the chat you expand yourself
- **move it wherever you want**: hold down on an empty space (even on the open panel or on the little guy) and drag: notch, panel and bubble move and remember where you left them, even after turning the pc back on. the notch snaps by itself to the center and to the top edge of the screen; if you detach it, it becomes a pill with its own outline. to put it back: right click → *Reset position* (or from the settings)
- **magnifying glass**: double click the little guy (or the bubble) and it becomes a lens that follows the cursor and magnifies the app, which stays open and visible, along with whatever is behind it (wheel = magnification). holding the button down you underline in yellow, double click or right click to remove it. color, size and zoom can be changed from the settings. on protected screens (windows permission prompts) the lens can't see
- **wikipedia** with gallery: besides the main photo you see the other images of the article
- **every feature can be turned on and off** from the settings, except the home
- **weather**: in the home you find a widget with an animated icon (spinning sun, rain, snow, lightning...), temperature, description, min and max. the little guy reacts to the weather: with sun he puts on sunglasses, with rain he opens an umbrella (and with strong wind he flies away hanging on to the umbrella), with a thunderstorm he shakes with fear, when it's cloudy he shrugs ("dunno"), with snow or cold he puts on a scarf, at night he sleeps
- **web search**: if you ask about a news item or something it doesn't know, the ai searches the internet by itself (duckduckgo, with wikipedia as a backup) and answers based on the results, without asking you for confirmation. google doesn't allow its results to be read reliably, which is why it isn't used. it can be turned off from the settings
- **appearance**: from the settings you pick a theme (night, day, ocean, sunset, forest, candy), the main color, the little guy's color (also in hexadecimal), the transparency and a colored outline for the notch. everything changes on the fly and if you cancel it goes back to how it was
- **updates**: a few minutes after launch the app checks by itself whether there's a new version (red dot on the little guy, and it asks you whether to update). it downloads, verifies the file and updates silently, then restarts. it can be turned off from the settings
- **what's new**: when the app restarts after an update, a little window opens by itself with everything that changed in this version, taken from the release notes and shown only in the language you chose
- **do not disturb**: from the right click menu (or from the ⚙) you turn on "do not disturb": no dancing, no weather emotions, no reactions and no listening to music. the little guy stays still and only blinks; when you turn it off everything goes back to how it was
- **system reactions**: the little guy notices if the cpu gets too hot or the battery is low. with the notch closed you only see a small symbol (🔥 or 🔋); when you open it he does the animation for a couple of seconds and then goes back to normal. the battery is read by windows without permissions; for the **cpu temperature** the app first tries the pc's sensors and then the windows thermal zone (which can be read without permissions); if neither responds it uses the **cpu load**: the reaction still happens, but it's an estimate. the sensor library is already inside the installer, you don't need to download anything and you don't need to be administrator
- **microphone**: next to the writing boxes (chat, +, wikipedia, translator) there's a little microphone: press it, speak, the text appears in the field. it uses windows speech recognition (the language must be installed) and it never keeps listening on its own
- **clipboard in the file holder**: whatever you copy ends up in the file holder as a slot: text as it is, images as a thumbnail. double click copies it again. copies that password managers ask not to record are left out
- **file holder expiry**: from the settings you choose whether the file holder and clipboard are emptied after 24 hours, 7 days or 30 days
- **reminders**: for anything at all, not just phone calls: "remind me at 6 pm to call mom", "remind me to take my medicine at 8", "remind me in 20 minutes to take out the trash". at the right time the little guy gets all excited, opens himself on the chat and tells you with the windows sound; if that time already passed today he sets it for tomorrow. they stay even if you close and reopen the app
- **more actions from the chat**: wifi (status, on, off), bluetooth and airplane mode, a screenshot that ends up in the file holder, opening a folder or a file even just by name (it looks among the recent ones)
- right click (or ⚙) to quit

## what it can do from the chat

- volume (set, raise/lower, mute), play/pause/next/previous
- opens **any installed app**: the name it has in the start menu is enough (calculator, steam, visual studio code, whatsapp...), it looks for it in the shortcuts and in the windows registry. it also searches on google, locks/suspends/hibernates/shuts down/restarts the pc
- reminders for anything: "remind me at 6 pm to call mom", "remind me to take my medicine at 8", "remind me in 20 minutes to take out the trash"
- cancel a reminder: "cancel the call mom reminder", "remove all reminders" (it asks for confirmation in the popup and updates the bell)
- wifi (status, on, off), bluetooth and airplane mode
- a screenshot (goes in the file holder) and opening a folder or a file, even just by name: it looks among the recent ones

sensitive actions (opening apps, browser, shutdown etc.) first ask for confirmation with a popup that shows the steps.
files dragged into the + tab are read but can't trigger actions.


## installation (recommended)
download `YourLittleFriend-setup-x.y.z.exe` from the [releases](../../releases) and run it. no administrator permissions are needed and you don't have to install .net.

1. choose the language (italiano / english)
2. choose the folder (by default `AppData\Roaming\yourlittlefriend`)
3. choose whether to make it start when the pc starts
4. choose whether to create the desktop icon (it's also in the start menu)

**updating**: you don't have to do anything. a few minutes after launch the app checks whether there's a new version and, if there is, puts a red dot on the little guy: open the notch and it asks if you want to update. if you say yes it downloads, checks the file's integrity, installs silently and restarts by itself, keeping settings and file holder. you can also download the new installer and run it by hand, without uninstalling anything: it closes the open app by itself, installs over the old version (same folder), remembers whether you had autostart and doesn't touch your data. if you try by mistake to install a version older than the one you have, it warns you.

on first launch the app asks you for:

1. your **name**
2. your **city** for the weather (you can skip)
3. the **ai**: the guided setup immediately checks whether ollama is already installed and which models you already have: if it finds one you like you use it right away, without downloading anything. otherwise it downloads ollama by itself (without leaving windows open) and the model. you can use the recommended model or write your own: the app checks whether your pc (ram and disk space) can handle it. you can also skip and do it later

in the settings there's also the **Ollama** section: you see whether it's on, you start or stop it (with a progress bar while it starts), you see the status of the model in use (installed / in memory), you load the model into memory to get the first answer right away and you unload the active models from memory. it updates by itself every two seconds.

everything is changed from the **settings** (⚙ → settings): name, city, language, model, autostart and the button to **uninstall**. the data is in `%APPDATA%\yourlittlefriend\settings.json`.

## running from source (experts only)
download the full zip from github, extract it into a folder of your choice. open powershell in that folder. then run:
```    dotnet run```
*you need the [.net 8 sdk](https://dotnet.microsoft.com/download) on windows 10/11.

## creating a release (for those who maintain the project)
the installer is built with github actions (`.github/workflows/build.yml`, it uses [inno setup](https://jrsoftware.org/isinfo.php) with `installer/yourlittlefriend.iss`).
every pull request produces the installer as an artifact to try it out. to publish a release a tag is enough:

    git tag 1.1.3
    git push origin 1.1.3

the tag may or may not have the "v" in front. if you create the release by hand from github, the build attaches the installer by itself (with the right version) without touching the title and notes. a tag with a hyphen (`-beta`) created by the build comes out as a **pre-release**. the version appears at the bottom of the settings.

## ❤️‍🩹 Help us
have any suggestions or tips? go wild! open an issue on this repo and tell us everything. we'll do our best to bring your ideas to life. if you can code (even using ai agents) open a pull request (PR)


## roadmap

### reading images and OCR with a local llm
paste an image and the local LLM will read the image for OCR or look at the image and its details. (in development) (for now it only works with online ai)

### hats, costumes and shop
the little guy will be able to wear hats and costumes (also themed ones, like christmas or halloween). you unlock them from a **shop** that opens in a separate window: it's a game, it costs nothing.

how it works: add an item to the cart and a quick animation plays, with the cart rolling across the window with the item inside. when you "buy", the bag flies to the **fitting room** section (in the same window). the little guy is in the fitting room, with a light coming down from below: you put the clothes on him, there's a "poof!" and he reacts.

### windows timer
when a windows timer is running the little guy shows it and lets you know when it goes off, on top of the windows sound.

---

## guide
### deepl api key
follow this guide: [deepl api key guide](https://support.deepl.com/hc/en-us/articles/360020695820-API-key-for-DeepL-API),
when it asks for the plan, press the free one, it will ask for name, surname and address but you can also enter fake information,
then go to the notch (or bubble) > right click > settings > scroll all the way down and then find "DeepL key", put it there, then restart the app.

### local llm (ollama)
the installation of ollama and of the model is guided by the app (first launch or settings). if you want to do it by hand: install ollama and then `ollama pull <model>`; the model is chosen from the settings. you can also stop it when you're not using it from the settings

### your own ai with your own key (byollm)
from the settings → **artificial intelligence** choose *online ai with my key* and paste the key of the service you use: openai, claude, gemini, nvidia, groq, mistral, deepseek, grok, openrouter, together, cerebras, fireworks. with **other** you enter the address of any service that speaks openai's language (another program on your pc also works, like lm studio). **load models** asks the service which ones are available with your key, **test** asks a tiny question and immediately tells you whether it works. the key stays in the settings file on your pc and only the app uses it, to talk to the service you chose.
