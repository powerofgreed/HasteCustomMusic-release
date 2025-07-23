using HarmonyLib;
using Landfall.Haste.Music;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zorro.Settings;
public class CustomMusicManager : MonoBehaviour
{

    public static List<AudioClip> CustomTracks { get; private set; } = new List<AudioClip>();
    public static MusicPlaylist CustomPlaylist { get; private set; }

    // Configuration options
    public static bool LockCustomPlaylist { get; set; } = false;
    private static MusicPlaylist _previousDefaultPlaylist;
    private static int _previousDefaultTrackIndex;

    public static int CurrentTrackIndex { get;  set; } = 0;
    public static bool IsCustomPlaylistActive { get; private set; } = false;
    private static MusicPlaylist _lastAttemptedDefaultPlaylist;
    private static int _lastAttemptedTrackIndex;
    public static bool PlaylistNeedsRefresh { get; set; } = true;
    public enum PlayOrder { Sequential, Loop, Random }
    public static PlayOrder CurrentPlayOrder = PlayOrder.Sequential;
    public static List<int> shuffledOrder = new List<int>();
    public static List<int> playedTracks = new List<int>();
    public static int shuffleIndex = 0;
    public static bool IsShuffled => shuffledOrder.Count > 0;
    private static int _lastCustomTrackIndex = 0;
    public static MusicVolumeSetting MusicVolumeSetting { get; set; }
    public static bool IsLoading { get; private set; }

    [HarmonyPatch(typeof(MusicPlayer), "InitRandomAndPlay")]
    [HarmonyPrefix]
    private static bool InitRandomAndPlay_Prefix(MusicPlaylist newPlaylist)
    {
        // Track random playlist initialization attempts
        if (newPlaylist != null && newPlaylist != CustomPlaylist)
        {
            _lastAttemptedDefaultPlaylist = newPlaylist;
            _lastAttemptedTrackIndex = 0; // Random playlists start at 0
        }

        // Locking check
        if (LockCustomPlaylist && IsCustomPlaylistActive && newPlaylist != CustomPlaylist)
        {
            Debug.Log($"Blocked InitRandomAndPlay to {newPlaylist.name}");
            return false; // Skip original method
        }

        return true; // Continue to original method
    }

    [HarmonyPatch(typeof(VolumeSetting), "ApplyValue")]
    private static class VolumeSettingPatch
    {
        private static bool ApplyValue_Prefix(VolumeSetting __instance)
        {
            // Only intercept music volume changes
            if (__instance is MusicVolumeSetting)
            {
                // Get current volume from our slider
                float currentVolume = MusicDisplayPlugin.GetCurrentMusicVolume();

                // Apply to the actual setting value
                Traverse.Create(__instance).Field("m_value").SetValue(currentVolume);
            }
            return true;
        }
    }


    [HarmonyPatch(typeof(MusicPlayer), "ChangePlaylist")]
    [HarmonyPrefix]
    private static bool ChangePlaylist_Prefix(MusicPlaylist newPlaylist, int trackId = 0, bool forcePlay = false)
    {
        // Track ANY default playlist change attempts
        if (newPlaylist != null && newPlaylist != CustomPlaylist)
        {
            _lastAttemptedDefaultPlaylist = newPlaylist;
            _lastAttemptedTrackIndex = trackId;
        }

        // Locking check
        if (LockCustomPlaylist && IsCustomPlaylistActive && newPlaylist != CustomPlaylist)
        {
            Debug.Log($"Blocked ChangePlaylist to {newPlaylist.name}");
            return false; // Skip original method
        }

        // Activation state
        IsCustomPlaylistActive = (newPlaylist == CustomPlaylist);

        if (!IsCustomPlaylistActive)
        {
            CurrentTrackIndex = 0;
        }

        PlaylistNeedsRefresh = true;

        return true; // Continue to original method
    }

    [HarmonyPatch(typeof(HasteSettingsHandler), "AddSetting")]
    private static class SettingsHandlerPatch
    {
        private static void Postfix(Setting setting)
        {
            if (setting is MusicVolumeSetting musicSetting)
            {
                MusicVolumeSetting = musicSetting;
            }
        }
    }
    private static bool IsFileLocked(string filePath)
    {
        try
        {
            using (File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                return false;
            }
        }
        catch (IOException)
        {
            return true;
        }
    }

    public static bool LoadCustomTracks(string directoryPath)
    {
        try
        {
            IsLoading = true;

            // First, unload any existing tracks to prevent memory leaks
            UnloadCustomTracks();
            System.Threading.Thread.Sleep(100); // Allow time for resources to release

            // Reset last track index when loading new tracks
            _lastCustomTrackIndex = 0;

            // Validate directory exists
            if (!Directory.Exists(directoryPath))
            {
                try
                {
                    Directory.CreateDirectory(directoryPath); // Auto-create if missing
                    Debug.Log($"Created directory: {directoryPath}");
                    return false; // No tracks to load in new directory
                }
                catch (Exception createEx)
                {
                    Debug.LogError($"Failed to create directory: {createEx}");
                    return false;
                }
            }

            var loadedFiles = 0;
            var supportedExtensions = new HashSet<string> {".wav", ".mp3", ".ogg", ".flac", ".aif", ".aiff", ".wma", ".m4a", ".aac"};

            try
            {
                // Get files with read access check
                var files = Directory.GetFiles(directoryPath, "*.*")
                    .Where(f => supportedExtensions.Contains(Path.GetExtension(f).ToLower()))
                    .ToList();

                foreach (string file in files)
                {
                    try
                    {

                        // Skip files that might be in use
                        if (IsFileLocked(file)) continue;

                        AudioClip clip = AudioLoader.LoadAudioFile(file);
                        if (clip != null)
                        {
                            clip.name = Path.GetFileNameWithoutExtension(file);
                            CustomTracks.Add(clip);
                            loadedFiles++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error loading {Path.GetFileName(file)}: {ex.Message}");
                    }
                }
            }
            catch (UnauthorizedAccessException authEx)
            {
                Debug.LogError($"Access denied to directory: {authEx.Message}");
                return false;
            }
            catch (DirectoryNotFoundException dirEx)
            {
                Debug.LogError($"Directory not found: {dirEx.Message}");
                return false;
            }

            if (loadedFiles > 0)
            {
                CreateCustomPlaylist();
                CurrentTrackIndex = 0;
                Debug.Log($"Successfully loaded {loadedFiles} tracks");

                // Free unused resources immediately
                Resources.UnloadUnusedAssets();
                GC.Collect();

                return true;
            }
            else
            {
                Debug.LogWarning("No valid audio files found in directory");
                return false;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Critical error loading custom tracks: {e}");
            return false;
        }
        finally
        {
            System.Threading.Thread.Sleep(50);
            IsLoading = false;
        }
    }

    // Method to properly unload existing tracks
    private static void UnloadCustomTracks()
    {
        // Unload existing audio clips
        CustomTracks.ForEach(clip => {
            if (clip != null)
            {
                clip.UnloadAudioData();
            }
        });
        CustomTracks.Clear();

        // Destroy custom playlist if it exists
        if (CustomPlaylist != null)
        {
            // Destroy each track in the playlist
            foreach (var track in CustomPlaylist.tracks)
            {
                if (track != null && track.track != null)
                {
                    track.track.UnloadAudioData();
                }
            }

            // Destroy the playlist itself
            UnityEngine.Object.Destroy(CustomPlaylist);
            CustomPlaylist = null;
        }

        // Clear shuffle/play history
        shuffledOrder.Clear();
        playedTracks.Clear();
    }
    private static int GetNaturalIndex(AudioClip clip)
    {
        if (CustomTracks == null) return -1;
        for (int i = 0; i < CustomTracks.Count; i++)
        {
            if (CustomTracks[i] == clip) return i;
        }
        return -1;
    }


    private static void CreateCustomPlaylist()
    {
        CustomPlaylist = ScriptableObject.CreateInstance<MusicPlaylist>();
        CustomPlaylist.name = "Custom_Playlist_";
        CustomPlaylist.playRandom = false;
        CustomPlaylist.tracks = new MusicPlaylist.MusicPlaylistTrack[CustomTracks.Count];

        for (int i = 0; i < CustomTracks.Count; i++)
        {
            CustomPlaylist.tracks[i] = new MusicPlaylist.MusicPlaylistTrack()
            {
                track = CustomTracks[i],
                fadeTime = 1.0f,
                previousTrackFadeOutTime = 1.0f,
                totalBars = 32,
                musicTail = 0f,
                transitionPointsInBars = new int[0]
            };
        }
    }

    public static void PlayTrack(int trackIndex)
    {
        if (CustomPlaylist?.tracks == null ||
            trackIndex < 0 ||
            trackIndex >= CustomPlaylist.tracks.Length)
        {
            Debug.LogError("Invalid track index");
            return;
        }

        if (!IsCustomPlaylistActive)
        {
            // Switch to custom playlist and play requested track
            _lastCustomTrackIndex = trackIndex;
            PlayCustomPlaylist();
        }
        else
        {
            CurrentTrackIndex = trackIndex;
            PlayCurrentTrack();  // This will update _lastCustomTrackIndex
        }
    }

    public static void PlayCustomPlaylist()
    {

        // Don't switch if still loading
        if (CustomPlaylist == null || CustomTracks.Count == 0)
        {
            Debug.LogWarning("Custom playlist not ready");
            return;
        }

        if (MusicPlayer.Instance == null || CustomPlaylist == null)
            return;

        // Store ORIGINAL default playlist when switching to custom
        if (!IsCustomPlaylistActive && MusicPlayer.Instance.currentlyPlaying?.playlist != null)
        {
            _previousDefaultPlaylist = MusicPlayer.Instance.currentlyPlaying.playlist;
            _previousDefaultTrackIndex = MusicPlayer.Instance.currentTrackId;
        }

        // Reset track history when switching to custom
        playedTracks.Clear();

        bool isNewPlaylist = !IsCustomPlaylistActive;
        IsCustomPlaylistActive = true;

        // Restore last track index if switching back to custom
        if (_lastCustomTrackIndex >= 0 &&
            _lastCustomTrackIndex < CustomPlaylist.tracks.Length)
        {
            CurrentTrackIndex = _lastCustomTrackIndex;
        }
        else
        {
            CurrentTrackIndex = 0;
        }

        // Play the current track
        PlayCurrentTrack();
    }
    public static void SwitchToDefaultPlaylist()
    {
        // Remember current track index in custom playlist
        _lastCustomTrackIndex = CurrentTrackIndex;

        // Use the LAST ATTEMPTED default playlist if available
        MusicPlaylist targetPlaylist = _lastAttemptedDefaultPlaylist ?? _previousDefaultPlaylist;

        if (targetPlaylist == null || MusicPlayer.Instance == null)
        {
            Debug.Log("No default playlist available");
            return;
        }

        IsCustomPlaylistActive = false;

        if (targetPlaylist.playRandom)
        {
            MusicPlayer.Instance.InitRandomAndPlay(targetPlaylist);
        }
        else
        {
            // Use last attempted track index if available
            int trackIndex = _lastAttemptedDefaultPlaylist != null ?
                _lastAttemptedTrackIndex :
                _previousDefaultTrackIndex;

            trackIndex = Mathf.Clamp(trackIndex, 0, targetPlaylist.tracks.Length - 1);
            MusicPlayer.Instance.ChangePlaylist(targetPlaylist, trackIndex, true);
        }
    }

    public static void PlayNextTrack()
    {
        if (MusicPlayer.Instance == null) return;

        // Get isFadingOut using Traverse
        var isFadingOut = Traverse.Create(MusicPlayer.Instance).Field("isFadingOut").GetValue<bool>();
        var isSwitchingMusic = Traverse.Create(MusicPlayer.Instance).Field("isSwitchingMusic").GetValue<bool>();

        // Prevent rapid firing during transitions
        if (isSwitchingMusic || isFadingOut)
        {
            return;
        }

        if (!IsCustomPlaylistActive)
        {
            PlayNextDefaultTrack();
            return;
        }

        if (CustomPlaylist?.tracks == null || CustomPlaylist.tracks.Length == 0)
        {
            Debug.LogError("No tracks in custom playlist");
            return;
        }

        // Handle different play orders
        switch (CurrentPlayOrder)
        {
            case PlayOrder.Sequential:
                PlayNextSequential();
                break;

            case PlayOrder.Loop:
                // Stay on current track - just replay it
                PlayCurrentTrack();
                return;

            case PlayOrder.Random:
                PlayNextRandom();
                break;
        }

        PlayCurrentTrack();
    }

    private static void PlayNextSequential()
    {
        if (shuffledOrder.Count > 0)
        {
            // Using shuffled playlist order
            shuffleIndex = (shuffleIndex + 1) % shuffledOrder.Count;
            CurrentTrackIndex = shuffledOrder[shuffleIndex];
        }
        else
        {
            // Using natural playlist order
            CurrentTrackIndex = (CurrentTrackIndex + 1) % CustomPlaylist.tracks.Length;
        }
    }

    private static void PlayNextRandom()
    {
        // Reset if we've played all tracks
        if (playedTracks.Count >= CustomPlaylist.tracks.Length)
        {
            playedTracks.Clear();
        }

        // Get available tracks that haven't been played
        List<int> availableTracks = new List<int>();
        for (int i = 1; i < CustomPlaylist.tracks.Length; i++)
        {
            if (!playedTracks.Contains(i))
            {
                availableTracks.Add(i);
            }
        }

        // Select random track from available options
        if (availableTracks.Count > 0)
        {
            int randomIndex = UnityEngine.Random.Range(0, availableTracks.Count);
            CurrentTrackIndex = availableTracks[randomIndex];
            playedTracks.Add(CurrentTrackIndex);
        }
        else
        {
            // Fallback if no available tracks
            CurrentTrackIndex = (CurrentTrackIndex + 1) % CustomPlaylist.tracks.Length;
        }
    }
    private static void AdvanceShuffledPlaylist()
    {
        // Ensure we have a valid shuffled list
        if (MusicPlayer.Instance.randomTrackIds == null || MusicPlayer.Instance.randomTrackIds.Count == 0)
        {
            InitializeShuffledPlaylist();
        }

        CurrentTrackIndex++;
        if (CurrentTrackIndex >= MusicPlayer.Instance.randomTrackIds.Count)
        {
            CurrentTrackIndex = 0;
            InitializeShuffledPlaylist(); // Re-shuffle when we reach end
        }

        PlayCurrentTrack();
    }

    private static void AdvanceSequentialPlaylist()
    {
        CurrentTrackIndex = (CurrentTrackIndex + 1) % CustomPlaylist.tracks.Length;
        PlayCurrentTrack();
    }


    private static void PlayNextDefaultTrack()
    {
        var player = MusicPlayer.Instance;
        var playlist = player.currentlyPlaying.playlist;

        if (playlist.playRandom)
        {
            player.currentRandomId = (player.currentRandomId + 1) % player.randomTrackIds.Count;
            player.ChangePlaylist(playlist, player.randomTrackIds[player.currentRandomId], true);
        }
        else
        {
            int nextTrackId = (player.currentTrackId + 1) % playlist.tracks.Length;
            player.ChangePlaylist(playlist, nextTrackId, true);
        }
    }

    public static void PlayCurrentTrack()
    {

        if (MusicPlayer.Instance == null) return;

        if (!IsCustomPlaylistActive || CustomPlaylist?.tracks == null) return;

        // Always update the last custom track index before playing
        _lastCustomTrackIndex = CurrentTrackIndex;

        MusicPlayer.Instance.ChangePlaylist(CustomPlaylist, CurrentTrackIndex, true);
    }

    public static void InitializeShuffle()
    {
        if (CustomTracks == null || CustomTracks.Count == 0) return;

        // Remember current track before shuffling
        int currentTrackId = CurrentTrackIndex;

        shuffledOrder = Enumerable.Range(0, CustomTracks.Count).ToList();
        var rng = new System.Random();
        for (int i = shuffledOrder.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (shuffledOrder[i], shuffledOrder[j]) = (shuffledOrder[j], shuffledOrder[i]);
        }

        // Find the position of the current track in the new shuffled order
        shuffleIndex = shuffledOrder.IndexOf(currentTrackId);
        if (shuffleIndex < 0)
        {
            // If current track not found (shouldn't happen), reset to 0
            shuffleIndex = 0;
        }

        Debug.Log("Playlist shuffled. Current track position: " + shuffleIndex);
    }

    private static void InitializeShuffledPlaylist()
    {
        if (CustomPlaylist?.tracks == null || CustomPlaylist.tracks.Length == 0)
        {
            Debug.LogError("Cannot initialize - no tracks in playlist");
            return;
        }

        try
        {
            // Create fresh shuffled order with validation
            var validIndices = Enumerable.Range(0, CustomPlaylist.tracks.Length)
                .Where(i => CustomPlaylist.tracks[i]?.track != null)
                .ToList();

            if (validIndices.Count == 0)
            {
                Debug.LogError("No valid tracks available for shuffling");
                return;
            }

            // Fisher-Yates shuffle
            var rng = new System.Random();
            for (int i = validIndices.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (validIndices[j], validIndices[i]) = (validIndices[i], validIndices[j]);
            }

            MusicPlayer.Instance.randomTrackIds = validIndices;

            // Preserve current position if possible
            if (CurrentTrackIndex >= validIndices.Count)
            {
                CurrentTrackIndex = 0;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Shuffle initialization failed: {e}");
            MusicPlayer.Instance.randomTrackIds = Enumerable.Range(0, CustomPlaylist.tracks.Length).ToList();
        }
    }

    [HarmonyPatch(typeof(MusicPlayer), "Update")]
    private static bool MusicPlayer_Update_Prefix(MusicPlayer __instance)
    {
        try
        {
            // Skip if not using custom playlist
            if (!IsCustomPlaylistActive) return true;

            // Get isFadingOut using Traverse
            var isFadingOut = Traverse.Create(__instance).Field("isFadingOut").GetValue<bool>();
            var isSwitchingMusic = Traverse.Create(__instance).Field("isSwitchingMusic").GetValue<bool>();

            // Skip during transitions
            if (isSwitchingMusic || isFadingOut)
            {
                return false;
            }

            // Validate all required components
            if (__instance.currentlyPlaying?.playlist == null ||
                __instance.m_AudioSourceCurrent == null ||
                __instance.m_AudioSourceCurrent.clip == null)
            {
                return false;
            }

            // Additional safety checks
            if (__instance.randomTrackIds == null ||
                __instance.randomTrackIds.Count == 0 ||
                __instance.currentRandomId < 0 ||
                __instance.currentRandomId >= __instance.randomTrackIds.Count)
            {
                InitializeShuffledPlaylist();
                return false;
            }

            // Original logic with protection
            if (__instance.m_AudioSourceCurrent.time >
                __instance.m_AudioSourceCurrent.clip.length -
                __instance.currentlyPlaying.playlist.tracks[__instance.randomTrackIds[__instance.currentRandomId]].fadeTime * 1.15f)
            {
                __instance.currentRandomId++;
                if (__instance.currentRandomId >= __instance.randomTrackIds.Count)
                {
                    __instance.currentRandomId = 0;
                }
                __instance.ChangePlaylist(__instance.currentlyPlaying.playlist,
                    __instance.randomTrackIds[__instance.currentRandomId], false);
            }
            return false;
        }
        catch (Exception e)
        {
            Debug.LogError($"MusicPlayer.Update error: {e}");
            InitializeShuffledPlaylist();
            return false;
        }
    }
    public static void ExportPlaylist(string path)
    {
        File.WriteAllLines(path, CustomTracks.Select(t => t.name));
    }

    public static void ImportPlaylist(string path, string audioDir)
    {
        var trackOrder = File.ReadAllLines(path);
        CustomTracks = CustomTracks
            .OrderBy(t => Array.IndexOf(trackOrder, t.name))
            .ToList();
        CreateCustomPlaylist();
    }
}