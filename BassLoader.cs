using UnityEngine;
using System.IO;
using System.Runtime.InteropServices;
using Un4seen.Bass;
using System;



public static class BassLoader
{

    private static bool _isInitialized = false;
    public static AudioClip LoadWithBass(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogError($"File not found: {filePath}");
            return null;
        }

        // Initialize BASS only once
        if (!_isInitialized)
        {
            // Suppress registration warning
            Bass.BASS_SetConfig(BASSConfig.BASS_CONFIG_UNICODE, 1);

            if (!Bass.BASS_Init(-1, 44100, BASSInit.BASS_DEVICE_DEFAULT, IntPtr.Zero))
            {
                // Don't treat initialization failure as critical
                Debug.LogWarning($"BASS initialization warning: {Bass.BASS_ErrorGetCode()}");
                _isInitialized = true; // Prevent repeated attempts
                return null;
            }
            _isInitialized = true;
        }

        // Check file format support before attempting to load
        string ext = Path.GetExtension(filePath).ToLower();
        if (!IsFormatSupported(ext))
        {
            return null;
        }

        // Load the audio file
        int stream = Bass.BASS_StreamCreateFile(filePath, 0, 0, BASSFlag.BASS_STREAM_DECODE);
        if (stream == 0)
        {
            Debug.LogWarning($"BASS warning loading {filePath}: {Bass.BASS_ErrorGetCode()}");
            return null;
        }

        // Get audio information
        BASS_CHANNELINFO info = Bass.BASS_ChannelGetInfo(stream);

        // Read audio data
        long lengthBytes = Bass.BASS_ChannelGetLength(stream);
        float[] samples = new float[lengthBytes / 4]; // 4 bytes per float
        GCHandle handle = GCHandle.Alloc(samples, GCHandleType.Pinned);
        int bytesRead = Bass.BASS_ChannelGetData(stream, handle.AddrOfPinnedObject(), (int)lengthBytes);
        handle.Free();

        if (bytesRead <= 0)
        {
            Debug.LogError($"Failed to read audio data: {Bass.BASS_ErrorGetCode()}");
            Bass.BASS_StreamFree(stream);
            return null;
        }

        // Create AudioClip
        AudioClip clip = AudioClip.Create(
            Path.GetFileNameWithoutExtension(filePath),
            samples.Length / info.chans,
            info.chans,
            info.freq,
            false
        );
        clip.SetData(samples, 0);

        // Cleanup
        Bass.BASS_StreamFree(stream);
        Bass.BASS_Free();

        return clip;
    }
    private static bool IsFormatSupported(string extension)
    {
        // Only attempt formats that work without plugins
        switch (extension)
        {
            case ".wav":
            case ".mp3":
            case ".aiff":
            case ".aif":
                return true;
            default:
                return false;
        }
    }
}