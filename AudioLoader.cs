using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using static MusicDisplayPlugin;

public static class AudioLoader
{
    private static bool? _bassAvailable;
    private static bool? _naudioAvailable;
    private static bool _dependenciesLogged = false;
    public static AudioClip LoadAudioFile(string filePath)
    {

        // Log dependency status once per session
        if (!_dependenciesLogged)
        {
            LogLoaderAvailability();
            LogDependencyStatus();
            _dependenciesLogged = true;
        }

        if (!File.Exists(filePath))
        {
            Debug.LogError($"File not found: {filePath}");
            return null;
        }

        string extension = Path.GetExtension(filePath).ToLower();
        AudioClip clip = null;

        // Try loaders in optimized order based on file format
        switch (extension)
        {
            case ".ogg":
                // OGG files work best with Unity's loader
                if (TryUnityLoader(filePath, extension, out clip)) return clip;
                if (IsBassAvailable() && TryBassLoader(filePath, out clip)) return clip;
                if (IsNAudioAvailable() && TryNAudioLoader(filePath, extension, out clip)) return clip;
                break;

            case ".mp3":
                // MP3 works best with NAudio or BASS
                if (IsNAudioAvailable() && TryNAudioLoader(filePath, extension, out clip)) return clip;
                if (IsBassAvailable() && TryBassLoader(filePath, out clip)) return clip;
                if (TryUnityLoader(filePath, extension, out clip)) return clip;
                break;

            case ".flac":
            case ".aac":
            case ".m4a":
                // Modern formats prefer NAudio
                if (IsNAudioAvailable() && TryNAudioLoader(filePath, extension, out clip)) return clip;
                if (IsBassAvailable() && TryBassLoader(filePath, out clip)) return clip;
                if (TryUnityLoader(filePath, extension, out clip)) return clip;
                break;

            case ".wma":
            case ".aif":
            case ".aiff":
                // Windows/media formats work best with NAudio
                if (IsNAudioAvailable() && TryNAudioLoader(filePath, extension, out clip)) return clip;
                if (TryUnityLoader(filePath, extension, out clip)) return clip;
                if (IsBassAvailable() && TryBassLoader(filePath, out clip)) return clip;
                break;

            case ".wav":
                // WAV files have dedicated optimized loader
                try
                {
                    clip = WavLoader.LoadWavFile(filePath);
                    if (clip != null) return clip;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"WAV loader failed: {e.Message}");
                }
                // Fall through to general loaders if specialized fails
                goto default;

            default:
                // Generic fallback with priority system
                LoaderPriority priority = GetLoaderPriority();
                switch (priority)
                {
                    case LoaderPriority.BassFirst:
                        if (IsBassAvailable() && TryBassLoader(filePath, out clip)) return clip;
                        if (TryUnityLoader(filePath, extension, out clip)) return clip;
                        if (IsNAudioAvailable() && TryNAudioLoader(filePath, extension, out clip)) return clip;
                        break;

                    case LoaderPriority.UnityFirst:
                        if (TryUnityLoader(filePath, extension, out clip)) return clip;
                        if (IsBassAvailable() && TryBassLoader(filePath, out clip)) return clip;
                        if (IsNAudioAvailable() && TryNAudioLoader(filePath, extension, out clip)) return clip;
                        break;

                    case LoaderPriority.NAudioFirst:
                        if (IsNAudioAvailable() && TryNAudioLoader(filePath, extension, out clip)) return clip;
                        if (IsBassAvailable() && TryBassLoader(filePath, out clip)) return clip;
                        if (TryUnityLoader(filePath, extension, out clip)) return clip;
                        break;
                }
                break;
        }

        // Final emergency fallback to Unity loader
        if (clip == null)
        {
            Debug.LogWarning($"All loaders failed, attempting Unity fallback for {filePath}");
            if (TryUnityLoader(filePath, extension, out clip)) return clip;
        }


        if (clip == null)
        {
            Debug.LogError($"All audio loading attempts failed for: {filePath}");
        }

        return clip;
    }

    private static void LogDependencyStatus()
    {
        string status = "Audio Loader Status:\n";

        if (IsBassAvailable())
        {
            status += "- BASS.dll: Loaded\n";
        }
        else
        {
            status += "- BASS.dll: Not found (MP3/WAV support limited)\n";
        }

        if (IsNAudioAvailable())
        {
            status += "- NAudio.dll: Loaded\n";
        }
        else
        {
            status += "- NAudio.dll: Not found (AAC/WMA support limited)\n";
        }

        Debug.Log(status);
    }

    private static void LogLoaderAvailability()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("===== Audio Loader Dependencies =====");

        // BASS check
        bool bassAvailable = IsBassAvailable();
        sb.AppendLine($"BASS: {(bassAvailable ? "AVAILABLE" : "MISSING")}");
        if (!bassAvailable)
        {
            sb.AppendLine("  - MP3/WAV/AIFF loading may be limited");
            sb.AppendLine("  - Install bass.dll and Bass.Net.dll to enable");
        }

        // NAudio check
        bool naudioAvailable = IsNAudioAvailable();
        sb.AppendLine($"NAudio: {(naudioAvailable ? "AVAILABLE" : "MISSING")}");
        if (!naudioAvailable)
        {
            sb.AppendLine("  - AAC/WMA/FLAC loading may be limited");
            sb.AppendLine("  - Install NAudio.dll to enable");
        }

        // Unity capabilities
        sb.AppendLine("Unity supports: WAV, OGG, MP3, AIFF, ACC");
        sb.AppendLine($"Current priority: {GetLoaderPriority()}");
        sb.AppendLine("====================================");

        Debug.Log(sb.ToString());
    }

    public static AudioClip LoadOgg(string filePath)
    {
        try
        {
            // Use Unity's loader for OGG instead of Bass
            return LoadWithUnityWebRequest(filePath, ".ogg");
        }
        catch (Exception e)
        {
            Debug.LogError($"OGG load exception: {e}");
            return null;
        }
    }

    private static bool TryBassLoader(string filePath, out AudioClip clip)
    {
        clip = null;
        try
        {
            clip = BassLoader.LoadWithBass(filePath);
            return clip != null;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"BASS loader failed: {e.Message}");
            return false;
        }
    }

    private static bool TryUnityLoader(string filePath, string extension, out AudioClip clip)
    {
        clip = null;
        try
        {
             clip = LoadWithUnityWebRequest(filePath, extension);
            return clip != null;
        }
        catch (Exception e)
        {
            // Add file-specific info
            Debug.LogWarning($"Unity loader failed for {Path.GetFileName(filePath)}: {e.Message}");
            return false;
        }
    }

    private static bool TryNAudioLoader(string filePath, string extension, out AudioClip clip)
    {
        clip = null;
        try
        {
            switch (extension)
            {
                case ".mp3":
                    clip = NAudioLoader.LoadMp3(filePath);
                    break;
                case ".wma":
                    clip = NAudioLoader.LoadWma(filePath);
                    break;
                case ".aif":
                case ".aiff":
                    clip = NAudioLoader.LoadAiff(filePath);
                    break;
                case ".flac":
                    clip = NAudioLoader.LoadFlac(filePath);
                    break;
                case ".m4a":
                case ".aac":
                    clip = NAudioLoader.LoadAac(filePath);
                    break;
                default:  
                    return false;
            }
            return clip != null;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"NAudio loader failed: {e.Message}");
            return false;
        }
    }

    private static AudioClip LoadWithUnityWebRequest(string filePath, string extension)
    {
        // Use file:// for local paths
        string url = "file://" + filePath;
        AudioType audioType = AudioType.UNKNOWN;

        switch (extension)
        {
            case ".ogg": audioType = AudioType.OGGVORBIS; break;
            case ".mp3": audioType = AudioType.MPEG; break;
            case ".aif":
            case ".aiff": audioType = AudioType.AIFF; break;
            case ".wav": audioType = AudioType.WAV; break;
            case ".m4a":
            case ".aac": audioType = AudioType.ACC; break;
            default: return null; // Skip unsupported formats
        }

        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(url, audioType))
        {
            www.SendWebRequest();
            while (!www.isDone) { }

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"UnityWebRequest error: {www.error}");
                return null;
            }

            return DownloadHandlerAudioClip.GetContent(www);
        }
    }

    private static LoaderPriority GetLoaderPriority()
    {
        // Implement your priority configuration logic here
        return MusicDisplayPlugin.CurrentLoaderPriority;
    }

    private static bool IsBassAvailable()
    {
        if (_bassAvailable == null)
        {
            try
            {
                Type bassType = Type.GetType("Un4seen.Bass.Bass, Bass.Net");
                _bassAvailable = bassType != null;
            }
            catch
            {
                _bassAvailable = false;
            }
        }
        return _bassAvailable.Value;
    }

    private static bool IsNAudioAvailable()
    {
        if (_naudioAvailable == null)
        {
            try
            {
                Type mp3Type = Type.GetType("NAudio.Wave.Mp3FileReader, NAudio");
                _naudioAvailable = mp3Type != null;
            }
            catch
            {
                _naudioAvailable = false;
            }
        }
        return _naudioAvailable.Value;
    }

}