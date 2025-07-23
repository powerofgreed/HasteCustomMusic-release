using System;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class WavLoader
{
    public static AudioClip LoadWavFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                Debug.LogError($"File not found: {filePath}");
                return null;
            }

            byte[] fileData = File.ReadAllBytes(filePath);
            AudioClip clip = WavToAudioClip(fileData, Path.GetFileNameWithoutExtension(filePath));

            if (clip == null)
            {
                Debug.LogError($"Failed to convert WAV file: {filePath}");
                return null;
            }

            return clip;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error loading WAV file {Path.GetFileName(filePath)}: {e.Message}");
            return null;
        }
    }

    public static AudioClip WavToAudioClip(byte[] wavBytes, string clipName)
    {
        // Basic WAV file parsing (supports standard PCM format)
        int channels = BitConverter.ToInt16(wavBytes, 22);
        int sampleRate = BitConverter.ToInt32(wavBytes, 24);
        int dataSize = BitConverter.ToInt32(wavBytes, 40);

        // Find the start of the data chunk
        int dataStart = 44; // Standard WAV header size
        for (int i = 0; i < wavBytes.Length - 4; i++)
        {
            if (wavBytes[i] == 'd' && wavBytes[i + 1] == 'a' && wavBytes[i + 2] == 't' && wavBytes[i + 3] == 'a')
            {
                dataStart = i + 8;
                dataSize = BitConverter.ToInt32(wavBytes, i + 4);
                break;
            }
        }

        // Create float array from 16-bit PCM data
        float[] audioData = new float[dataSize / 2];
        for (int i = 0; i < audioData.Length; i++)
        {
            short sample = (short)(wavBytes[dataStart + i * 2] | (wavBytes[dataStart + i * 2 + 1] << 8));
            audioData[i] = sample / 32768f;
        }

        // Create AudioClip
        AudioClip clip = AudioClip.Create(clipName, audioData.Length / channels, channels, sampleRate, false);
        clip.SetData(audioData, 0);
        return clip;
    }
}