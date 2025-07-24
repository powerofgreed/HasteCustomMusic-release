### 🎵HasteModPlaylist🎵

<img width="410" height="354" alt="image" src="https://github.com/user-attachments/assets/4ff4cc18-99f0-40fb-ba2d-8497f4d49df3" />


## Features
- Play custom music during gameplay
- Supports **MP3**, **WAV**, **OGG**, **FLAC**, **AAC**, **WMA**, **AIFF**
- Intuitive in-game UI with playlist management
- Volume control and playback modes

## Installation
1.Download [BepInEx (x64)](https://github.com/BepInEx/BepInEx)  **BepInEx_win_x64_5.4.23.3.zip**

2.To make Haste hook from BepInEx need to open *\Haste\BepInEx\core* folder and  **delete each file** which start with **MONO***

3.After first game launch, open *\Haste\BepInEx\config\BepInEx.cfg* and change **HideManagerGameObject = false** to **HideManagerGameObject = true**

4.Download **HasteModPlaylist-full-v0.0.1.7z** and extract in game directory.

 **--(Optional) [BepInEx.ConfigurationManager](https://github.com/BepInEx/BepInEx.ConfigurationManager)** - an additional plugin for much easier plugins configuration

## Usage
1. Place music files in *`BepInEx/CustomMusic/`* or any choosen path in **.cfg**
2. In-game:
   - **F2**: Toggle UI
   - **F3**: Next track
   - **Load**: Load audiofiles from path. **A non-existent directory will be created**
   - UI Buttons: Load/play/shuffle music

## Supported Formats
| Format | Library       | Notes                      |
|--------|---------------|----------------------------|
| MP3    | NAudio/BASS   | Best quality with NAudio   |
| WAV    | Unity         | Native support             |
| OGG    | Unity         | Native support             |
| FLAC   | NAudio        | High-quality audio         |
| AAC    | NAudio        | Apple audio format         |
| WMA    | NAudio        | Windows Media Audio        |
| AIFF   | Unity/NAudio  | Mac audio format           |

## Troubleshooting
- **No music playing?** Check file paths and formats
- **UI not showing?** Press F2 to toggle
- **Crashing?** Install [.NET 4.8 Runtime](https://dotnet.microsoft.com/download/dotnet-framework/net48)
- Do not load too much audio files. **Whole playlist is stored in RAM**. (till future updates at least...)

## Building from Source
1. Clone repo: `git clone https://github.com/yourname/Haste-Custom-Music-Mod.git`
2. Open in Visual Studio 2022
3. Build solution (Requires .NET 4.8 SDK)

## Credits
- [NAudio](https://github.com/naudio/NAudio) - Audio processing
- [BASS](http://www.un4seen.com) - Audio playback
- [HarmonyX](https://github.com/BepInEx/HarmonyX) - Patching library

## License
"BASS audio library © Un4seen Developments - un4seen.com"
