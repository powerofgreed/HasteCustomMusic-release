using NAudio.Wave;
using NAudio.Flac;
using System;
using System.IO;
using UnityEngine;


public static class NAudioLoader
{

    public static AudioClip LoadMp3(string filePath)
    {
        try
        {
            // NAudio-specific implementation
            using (var reader = new NAudio.Wave.Mp3FileReader(filePath))
            {
                var ms = new MemoryStream();
                WaveFileWriter.WriteWavFileToStream(ms, reader);
                return WavLoader.WavToAudioClip(ms.ToArray(), Path.GetFileNameWithoutExtension(filePath));
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"MP3 load exception: {e}");
            return null;
        }
    }

    public static AudioClip LoadAac(string filePath)
    {
        try
        {
            using (var reader = new MediaFoundationReader(filePath))
            {
                return ConvertReaderToClip(reader, Path.GetFileNameWithoutExtension(filePath));
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"AAC load exception: {e}");
            return null;
        }
    }

    public static AudioClip LoadWma(string filePath)
    {
        try
        {
            using (var reader = new NAudio.WindowsMediaFormat.WMAFileReader(filePath))
            {
                return ConvertReaderToClip(reader, Path.GetFileNameWithoutExtension(filePath));
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"WMA load exception: {e}");
            return null;
        }
    }

    public static AudioClip LoadAiff(string filePath)
    {
        try
        {
            using (var reader = new NAudio.Wave.AiffFileReader(filePath))
            {
                return ConvertReaderToClip(reader, Path.GetFileNameWithoutExtension(filePath));
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"AIFF load exception: {e}");
            return null;
        }
    }

    public static AudioClip LoadFlac(string filePath)
    {
        try
        {
            using (var reader = new FlacReader(filePath))
            {
                return ConvertReaderToClip(reader, Path.GetFileNameWithoutExtension(filePath));
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"FLAC load exception: {e}");
            return null;
        }
    }

    private static AudioClip ConvertReaderToClip(WaveStream reader, string clipName)
    {
        var ms = new MemoryStream();
        WaveFileWriter.WriteWavFileToStream(ms, reader);
        return WavLoader.WavToAudioClip(ms.ToArray(), clipName);
    }
}

