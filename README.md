### 🎵HasteModPlaylist🎵

<img width="406" height="463" alt="image" src="https://github.com/user-attachments/assets/131250fa-fc23-4a65-9f0b-c6bc2354b341" />


## ✨ Features

- **Instant Playback** - Stream local or remote tracks without preloading
- **Universal Format Support** - MP3, FLAC, OGG, AAC, HLS, and more via ManagedBass
- **Playlist Management** - Save, load, and manage custom playlists
- **Streaming Support** - HTTP/HTTPS/FTP streams and radio with ICY metadata
- **HLS Support** - .m3u8 live stream playback
- **In-Game UI** - Full player interface with volume control and seeking

## 🚀Installation
1. Download [BepInEx (x64)](https://github.com/BepInEx/BepInEx/releases)  **BepInEx_win_x64_5.4.23.3.zip**

2. To make Haste hook from BepInEx need to open *\Haste\BepInEx\core* folder and  **delete each file** which starts with **MONO***

3. After first game launch, open *\Haste\BepInEx\config\BepInEx.cfg* and change **HideManagerGameObject = false** to **HideManagerGameObject = true**

4. Download  [**HasteCustomMusic-full.zip**](https://github.com/powerofgreed/HasteCustomMusic-release/releases/tag/0.0.2) and extract in game directory.

 **--(Optional) [BepInEx.ConfigurationManager](https://github.com/BepInEx/BepInEx.ConfigurationManager)** - an additional plugin for much easier plugins configuration

## Usage
1. Place music files in *`BepInEx/CustomMusic/`* or any choosen path in **.cfg**
2. In-game:
   - **F2** or **DPAD-UP**: Toggle UI
   - **F3** or **DAP-RIGHT**: Next track
   - **Load**: Load audiofiles from path. **A non-existent directory will be created. RAM heavy operation**
   - **UI Buttons**: Play order, Shuffle, Next track
   - **Lock Custom**: Prevent playlist from changing back to default
   - **Bottom right** button to switch between in-game and custom playlists.
3. Fluid playlist control:
   - **Shift + double click** - Adds track to Hybrid playlist
   - **CTRL + Double Click** - Removes track from any playlists

## 🎵 Supported Formats

| Format | Native Plugin | Managed Package | Notes |
|--------|---------------|-----------------|-------|
| **MP3** | (core) | ManagedBass | Built-in support |
| **WAV** | (core) | - | Unity native |
| **OGG/Opus** | bassopus.dll | ManagedBass.Opus | - |
| **FLAC** | bassflac.dll | ManagedBass.Flac | Lossless audio |
| **AAC/M4A** | bass_aac.dll | ManagedBass.Aac | - |
| **ALAC** | bassalac.dll | - | Apple Lossless |
| **HLS** | basshls.dll | ManagedBass.Hls | .m3u8 streams |
| **Speex** | bass_spx.dll | - | Voice codec |
| **AC3** | bass_ac3.dll | - | Dolby Digital |

## 📦 Native Dependencies

ManagedBass requires these native libraries in your plugin folder:
- `bass.dll` (core audio engine)
- Format-specific plugins (e.g., `bassflac.dll` for FLAC support)
- Platform variants for Linux/macOS

## Troubleshooting
- **No music playing?** Check file paths and formats
- **UI not showing?** Press F2 to toggle
- **Crashing?** Install [.NET 4.8 Runtime](https://dotnet.microsoft.com/download/dotnet-framework/net48)
- **Format Not Working?** Install required native plugin DLL's add-on  

###🔧 Build Steps
1. Clone repo: `git clone https://github.com/yourname/Haste-Custom-Music-Mod.git`
2. Open in **Visual Studio 2022**
3. Restore **NuGet** packages
4. Build solution (targets **netstandard2.1**)
5. Copy output **HasteCustomMusic.dll** to `BepInEx/plugins`
6. **ManagedBass x64** .dll's provided via NuGet
7. **Bass add-ons x64** via their official site http://www.un4seen.com

### Project Structure
```
Haste/
├── BepInEx/
│   └── core/                    # BepInEx core DLLs
├── Haste_Data/
│   └── Managed/                # Game managed assemblies
└── HasteCustomMusic-release/   # Solution directory
    ├── HasteCustomMusic/       # Project directory
    │   └── HasteCustomMusic.csproj
    └── HasteModPlaylist.sln
```

## Credits
- [BASS](http://www.un4seen.com) - Audio playback
- [HarmonyX](https://github.com/BepInEx/HarmonyX) - Patching library

## 📄 Licensing

- **Plugin:** MIT License
- **ManagedBass:** MIT License  
- **BASS Audio:** © Un4seen Developments - Include license with distribution
