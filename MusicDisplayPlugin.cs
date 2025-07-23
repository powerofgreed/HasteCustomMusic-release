using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Landfall.Haste.Music;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

[BepInPlugin("com.PoG.HasteModPlaylist", "Custom Playlist", "0.0.1")]
public class MusicDisplayPlugin : BaseUnityPlugin
{
    private bool _showGUI = true;
    private Rect _windowRect = new Rect(20, 20, 400, 180);
    private Texture2D _backgroundTexture;
    private string _customMusicPath = Path.Combine(Application.dataPath, "CustomMusic");
    private MusicPlaylist _lastKnownPlaylist;
    private int _selectedTrackIndex = -1;
    private float _lastClickTime;
    private const float DoubleClickTime = 0.3f;
    private int _lastClickedTrack = -1;
    private const float DoubleClickThreshold = 0.3f;
    private Rect _playlistWindowRect;
    private bool _playlistWindowVisible = true;
    private bool _isResizing = false;
    private Rect _resizeHandle = new Rect(0, 0, 100, 5);
    private Vector2 _resizeStartMouse;
    private float _resizeStartHeight;
    private Vector2 _customScrollPos = Vector2.zero;
    private Vector2 _defaultScrollPos = Vector2.zero;
    private static float _currentVolume = 1.0f;
    public static float GetCurrentMusicVolume() => _currentVolume;
    private ConfigEntry<KeyboardShortcut> _showGuiShortcut;
    public enum LoaderPriority { BassFirst, UnityFirst, NAudioFirst }
    ConfigEntry<LoaderPriority> _loaderPriority;
    private ConfigEntry<KeyboardShortcut> _nextTrackShortcut;
    private ConfigEntry<string> _customMusicPathConfig;
    private ConfigEntry<string> _playOrderConfig;
    private ConfigEntry<bool> _forceCustomPlaylistConfig;
    private bool _forcePlaylistPending = false;
    public static LoaderPriority CurrentLoaderPriority { get; set; } = LoaderPriority.BassFirst;



    void Awake()
    {
        _backgroundTexture = new Texture2D(1, 1);
        _backgroundTexture.SetPixel(0, 0, new Color(0.1f, 0.1f, 0.85f, 0.7f));
        _backgroundTexture.Apply();
        Harmony.CreateAndPatchAll(typeof(CustomMusicManager));

        // Config bindings
        _showGuiShortcut = Config.Bind("Hotkeys", "Show GUI", new KeyboardShortcut(KeyCode.F2),
            "Toggle plugin UI visibility");

        _nextTrackShortcut = Config.Bind("Hotkeys", "Next Track", new KeyboardShortcut(KeyCode.F3),
            "Play next track in playlist");

        _customMusicPathConfig = Config.Bind("Paths", "Custom Music Path",
            Path.Combine(Application.dataPath, "CustomMusic"),
            "Directory for custom music files");

        _playOrderConfig = Config.Bind("Playback", "Play Order",
               CustomMusicManager.PlayOrder.Sequential.ToString(),
               new ConfigDescription("Initial playback mode: Sequential, Loop, or Random",
                   new AcceptableValueList<string>(Enum.GetNames(typeof(CustomMusicManager.PlayOrder)))
                   ));

        // Validate path before using
        _customMusicPath = GetValidatedMusicPath();

        _forceCustomPlaylistConfig = Config.Bind("Playback", "Force Custom Playlist", false,
            "Always load custom playlist on startup and locks it");
    }
void Start()
{
    // Set initial values from config
    _customMusicPath = _customMusicPathConfig.Value;

    // Handle forced custom playlist
    if (_forceCustomPlaylistConfig.Value)
    {
        // Validate path before using
        _customMusicPath = GetValidatedMusicPath();
        
        // Start the coroutine for force loading
        StartCoroutine(LoadAndForceCustomPlaylist());
    }

    _loaderPriority = Config.Bind(
        "Loader",
        "Priority",
        LoaderPriority.BassFirst,
        "Audio loader priority"
    );

        CurrentLoaderPriority = _loaderPriority.Value;
}


    void OnGUI()
    {
        if (_forcePlaylistPending) return;
        if (!_showGUI || MusicPlayer.Instance == null) return;

        try
        {
            _windowRect = GUI.Window(0, _windowRect, DrawMusicWindow, "*ੈ✩‧₊˚༺ Music Player " + $"({_showGuiShortcut.Value}) ༻*ੈ✩‧₊˚"); 

            // Playlist window below main window
            if (_playlistWindowVisible)
            {
                // Only set initial position once
                if (_playlistWindowRect.width == 0)
                {
                    _playlistWindowRect = new Rect(
                        _windowRect.x,
                        _windowRect.y + _windowRect.height + 5,
                        _windowRect.width,
                        160
                    );
                }
                else
                {
                    // Preserve custom height but update position relative to main window
                    _playlistWindowRect.x = _windowRect.x;
                    _playlistWindowRect.y = _windowRect.y + _windowRect.height;
                    _playlistWindowRect.width = _windowRect.width;
                }

                _playlistWindowRect = GUI.Window(1, _playlistWindowRect, DrawPlaylistWindow, "⋆⋆✮♪♫ Playlist ♫♪✮⋆⋆");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"GUI Error: {e}");
            _showGUI = false;
        }

        // Handle resizing outside of window drawing
        HandleResizing();
    }
    private void HandleResizing()
    {
        // Calculate absolute position of resize handle
        Rect absoluteResizeHandle = new Rect(
            _playlistWindowRect.x + _resizeHandle.x,
            _playlistWindowRect.y + _resizeHandle.y,
            _resizeHandle.width,
            _resizeHandle.height
        );

        if (Event.current.type == EventType.MouseDown && absoluteResizeHandle.Contains(Event.current.mousePosition))
        {
            _isResizing = true;
            _resizeStartMouse = Event.current.mousePosition;
            _resizeStartHeight = _playlistWindowRect.height;
            Event.current.Use(); // Mark event as handled
        }

        if (_isResizing)
        {
            // Get current mouse position
            Vector2 mousePos = Event.current.mousePosition;

            float heightDelta = mousePos.y - _resizeStartMouse.y;
            float newHeight = Mathf.Clamp(
                _resizeStartHeight + heightDelta,
                160, // Minimum height
                700  // Maximum height
            );

            // Only update if height actually changed
            if (Math.Abs(newHeight - _playlistWindowRect.height) > 0.1f)
            {
                _playlistWindowRect.height = newHeight;
            }

            // End resizing on mouse up
            if (Event.current.type == EventType.MouseUp)
            {
                _isResizing = false;
            }

            // Repaint GUI to show changes immediately
            GUI.changed = true;
        }
    }
    private IEnumerator LoadAndForceCustomPlaylist()
    {
        _forcePlaylistPending = true;

        // Load tracks
        bool success = false;
        yield return null; // Wait one frame to ensure everything is initialized

        try
        {
            success = CustomMusicManager.LoadCustomTracks(_customMusicPath);
        }
        catch (Exception e)
        {
            Debug.LogError($"Load error: {e}");
        }

        // Wait until loading completes
        yield return new WaitUntil(() => !CustomMusicManager.IsLoading);

        if (success && CustomMusicManager.CustomTracks.Count > 0)
        {
            // Wait for 1 second and one additional frame
            yield return new WaitForSecondsRealtime(1f);
            yield return null;

            CustomMusicManager.PlayCustomPlaylist();
            CustomMusicManager.LockCustomPlaylist = true;
            Debug.Log("Force-loaded custom playlist after delay");
        }
        else
        {
            Debug.LogError("Failed to force-load custom playlist");
        }

        _forcePlaylistPending = false;
    }

    private string GetValidatedMusicPath()
    {
        // Get configured path
        string configPath = _customMusicPathConfig.Value;

        // Check if path exists and is accessible
        try
        {
            if (Directory.Exists(configPath))
            {
                // Test read access
                Directory.GetFiles(configPath, "*.*", SearchOption.TopDirectoryOnly);
                return configPath;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Invalid music path: {ex.Message}");
        }

        // Fallback to default path
        string defaultPath = Path.Combine(Application.dataPath, "CustomMusic");
        Debug.Log($"Using default path: {defaultPath}");
        return defaultPath;
    }

    void DrawMusicWindow(int windowID)
    {

        // Save original background color
        Color originalColor = GUI.backgroundColor;

        // Set semi-transparent background (blue tint with 70% opacity)
        GUI.backgroundColor = new Color(0.1f, 0.1f, 0.85f, 0.7f);

        // Create a box that fills the window for background
        GUI.Box(new Rect(0, 0, _windowRect.width, _windowRect.height), "", new GUIStyle(GUI.skin.box));

        // Reset background color for other elements
        GUI.backgroundColor = originalColor;

        var buttonStyle20 = new GUIStyle(GUI.skin.button)
        {
            fontSize = 20,
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.MiddleCenter
        };
        var buttonStyle16 = new GUIStyle(GUI.skin.button)
        {
            fontSize = 16,
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.MiddleCenter
        };

        var toggleStyle = new GUIStyle(GUI.skin.toggle)
        {
            fontSize = 14,
            fontStyle = FontStyle.Normal,
        };
        var LableOF = new GUIStyle(GUI.skin.box)
        {
            border = GUI.skin.label.border,
            normal = GUI.skin.label.normal,
            alignment = GUI.skin.label.alignment
        };

        // Window background
        GUILayout.BeginArea(new Rect(10, 20, _windowRect.width - 20, 100));
        {

            if (MusicPlayer.Instance.m_AudioSourceCurrent?.clip != null)
            {
                float progress = MusicPlayer.Instance.m_AudioSourceCurrent.time /
                                 MusicPlayer.Instance.m_AudioSourceCurrent.clip.length;

                // Horizontal group for clip name and time
                GUILayout.BeginHorizontal(GUILayout.MaxHeight(20));
                {
                    // Clip name on left
                    GUILayout.Label(($"{MusicPlayer.Instance.m_AudioSourceCurrent.clip.name}"), LableOF,
                                   GUILayout.ExpandWidth(true), GUILayout.MaxHeight(20), GUILayout.ExpandHeight(false), GUILayout.Width(300));
                    GUILayout.FlexibleSpace();
                    // Time display on right
                    GUILayout.Label($"{FormatTime(MusicPlayer.Instance.m_AudioSourceCurrent.time)} / " +
                                   $"{FormatTime(MusicPlayer.Instance.m_AudioSourceCurrent.clip.length)}", LableOF,
                                    GUILayout.MaxHeight(20), GUILayout.ExpandHeight(false), GUILayout.ExpandWidth(true));
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                // Progress bar below
                float newProgress = GUILayout.HorizontalSlider(progress, 0f, 1f, GUILayout.ExpandHeight(true), GUILayout.MaxHeight(20));
                GUILayout.EndHorizontal();
                // Handle seeking only when value changes
                if (newProgress != progress)
                {
                    MusicPlayer.Instance.m_AudioSourceCurrent.time =
                        newProgress * MusicPlayer.Instance.m_AudioSourceCurrent.clip.length;
                }

            }
            else
            {
                GUILayout.Label("No music playing");
            }
        }
        GUILayout.EndArea();

        GUILayout.BeginArea(new Rect(200, 55, 190, 20));
        { 
            if (MusicPlayer.Instance.m_AudioSourceCurrent?.clip != null)
            {

                // Volume control
                GUILayout.BeginHorizontal();
                GUILayout.BeginVertical(GUILayout.MaxWidth(48));

                GUILayout.Label("Volume:", GUILayout.MaxWidth(48));
                GUILayout.Space(4);
                GUILayout.EndVertical();
                GUILayout.BeginVertical();  

                GUILayout.Space(4);
                float newVolume = GUILayout.HorizontalSlider(_currentVolume, 0f, 1f);
                if (Mathf.Abs(newVolume - _currentVolume) > 0.01f)
                {
                    _currentVolume = newVolume;
                    SetMusicVolumeLinear(_currentVolume);
                }
                
                GUILayout.EndVertical();
                GUILayout.BeginVertical(GUILayout.Width(33));

                //Percentage display
                GUILayout.Label($"{Mathf.RoundToInt(_currentVolume * 100)}%", GUILayout.Width(33));
                GUILayout.Space(4);
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }
        }
        GUILayout.EndArea();



        // Playlist name display
        string playlistName = GetCurrentPlaylistName();
        GUI.Label(new Rect(10, 55, 180, 20), $"Playlist: {playlistName}");



        // Play order button
        Rect modeRect = new Rect(10, 75, 120, 25);
        if (GUI.Button(modeRect, GetPlayOrderLabel(), buttonStyle20))
        {
            CyclePlayOrder();
        }




        // Shuffle button - only enabled in custom mode
        Rect shuffleRect = new Rect(140, 75, 120, 25);
        GUI.enabled = CustomMusicManager.IsCustomPlaylistActive;
        if (GUI.Button(shuffleRect, "🔀 Shuffle", buttonStyle20))
        {
            CustomMusicManager.InitializeShuffle();
        }
        GUI.enabled = true;

        // Next track button
        if (GUI.Button(new Rect(270, 75, 120, 25), "▶▶I", buttonStyle20))
        {
            CustomMusicManager.PlayNextTrack();
        }

        // Custom music controls
        GUI.Label(new Rect(10, 100, 315, 20), "Custom Music Path:");
        _customMusicPath = GUI.TextField(new Rect(10, 119, 315, 22), _customMusicPath);

        if (GUI.Button(new Rect(330, 119, 60, 22), "Load", buttonStyle16))
        {   
            CustomMusicManager.LoadCustomTracks(_customMusicPath);
        }

        if (GUI.Button(new Rect(10, 145, 185, 25), _playlistWindowVisible ? "Hide Playlist" : "Show Playlist", buttonStyle16))
        {
            _playlistWindowVisible = !_playlistWindowVisible;
        }
        GUI.enabled = CustomMusicManager.IsCustomPlaylistActive;
        CustomMusicManager.LockCustomPlaylist = GUI.Toggle(
            new Rect(198, 153, 105, 25),
            CustomMusicManager.LockCustomPlaylist,
            "Lock Custom", 
            toggleStyle
            );
        GUI.enabled = true;


        // Playlist switch
        Rect playlistToggleRect = new Rect(300, 145, 90, 25);
        GUI.enabled = CustomMusicManager.CustomPlaylist != null;
        string buttonText = CustomMusicManager.IsCustomPlaylistActive ?
        "◀ Default" : "Custom ▶";
        if (GUI.Button(playlistToggleRect, buttonText, buttonStyle16))
        {
            if (CustomMusicManager.IsCustomPlaylistActive)
            {
                CustomMusicManager.SwitchToDefaultPlaylist();
            }
            else
            {
                CustomMusicManager.PlayCustomPlaylist();
            }
        }
        GUI.enabled = true;

        GUI.DragWindow(new Rect(0, 0, 400, 20));
        GUI.DragWindow(new Rect(_playlistWindowRect.x, _playlistWindowRect.y, 400, 20));

    }

    private void DrawPlaylistWindow(int windowID)
    {
        // Save original background color
        Color originalColor = GUI.backgroundColor;

        // Set semi-transparent background (slightly darker blue)
        GUI.backgroundColor = new Color(0.08f, 0.08f, 0.8f, 0.7f);

        // Create a box that fills the window for background
        GUI.Box(new Rect(0, 0, _playlistWindowRect.width, _playlistWindowRect.height), "", new GUIStyle(GUI.skin.box));

        // Reset background color for other elements
        GUI.backgroundColor = originalColor;

        // Draw playlist content
        DrawPlaylistContents(5, 20, _playlistWindowRect.width - 10, _playlistWindowRect.height - 25);

        _resizeHandle = new Rect(0, _playlistWindowRect.height - 5, _playlistWindowRect.width, 10);
        GUI.Box(_resizeHandle, "", GUI.skin.button);

    }
    private string GetCurrentPlaylistName()
    {
        if (CustomMusicManager.IsCustomPlaylistActive)
        {
            return "Custom Music";
        }

        // Get name of current default playlist
        if (MusicPlayer.Instance != null &&
            MusicPlayer.Instance.currentlyPlaying != null &&
            MusicPlayer.Instance.currentlyPlaying.playlist != null)
        {
            return MusicPlayer.Instance.currentlyPlaying.playlist.name;
        }

        return "Default Playlist";
    }

    // Helper methods
    private string GetPlayOrderLabel()
    {
        switch (CustomMusicManager.CurrentPlayOrder)
        {
            case CustomMusicManager.PlayOrder.Sequential: return "▶▶▶";
            case CustomMusicManager.PlayOrder.Loop: return "↳↰";
            case CustomMusicManager.PlayOrder.Random: return "❹▶❶▶❸";
            default: return "▶▶";
        }
    }
    private void CyclePlayOrder()
    {
        int next = (int)CustomMusicManager.CurrentPlayOrder + 1;
        if (next > 2) next = 0;
        CustomMusicManager.CurrentPlayOrder = (CustomMusicManager.PlayOrder)next;

        // Reset track history when switching to random
        if (CustomMusicManager.CurrentPlayOrder == CustomMusicManager.PlayOrder.Random)
        {
            CustomMusicManager.playedTracks.Clear();
        }
    }


    private void DrawPlaylistContents(float x, float y, float width, float height)
    {
        Vector2 scrollPos = CustomMusicManager.IsCustomPlaylistActive ?
            _customScrollPos :
            _defaultScrollPos;

        MusicPlaylist currentPlaylist = CustomMusicManager.IsCustomPlaylistActive ?
            CustomMusicManager.CustomPlaylist :
            MusicPlayer.Instance.currentlyPlaying?.playlist;

        if (currentPlaylist?.tracks == null)
        {
            GUI.Label(new Rect(x + 5, y + 5, width - 10, 20), "No playlist loaded");
            return;
        }

        // Get the display order (shuffled or natural)
        List<int> displayOrder = new List<int>();

        if (CustomMusicManager.IsCustomPlaylistActive &&
            CustomMusicManager.shuffledOrder != null &&
            CustomMusicManager.shuffledOrder.Count == currentPlaylist.tracks.Length)
        {
            displayOrder = CustomMusicManager.shuffledOrder;
        }
        else
        {
            displayOrder = Enumerable.Range(0, currentPlaylist.tracks.Length).ToList();
        }

        // Get highlight index
        int highlightIndex = GetCurrentDisplayIndex();

        // Calculate content height based on number of tracks
        float contentHeight = displayOrder.Count * 20;

        // Begin scroll view without horizontal scroll bar
        scrollPos = GUI.BeginScrollView(
            new Rect(x, y, width, height),
            scrollPos,
            new Rect(0, 0, width - 5, contentHeight),
            false,
            true,
            GUIStyle.none,
            GUI.skin.verticalScrollbar
        );

        for (int displayIndex = 0; displayIndex < displayOrder.Count; displayIndex++)
        {
            int actualIndex = displayOrder[displayIndex];
            bool isCurrent = (actualIndex == highlightIndex);
            bool isSelected = (actualIndex == _selectedTrackIndex);
            string trackName = currentPlaylist.tracks[actualIndex]?.track?.name ?? "Unknown Track";

            // Set color based on selection state
            if (isCurrent) GUI.contentColor = Color.yellow;
            else if (isSelected) GUI.contentColor = Color.cyan;
            else GUI.contentColor = Color.white;
            Rect trackRect = new Rect(0, displayIndex * 20, 10000, 20);
            GUI.Label(trackRect, $"{displayIndex + 1}. {trackName}");

            // Handle mouse events
            if (Event.current.type == EventType.MouseDown &&
                trackRect.Contains(Event.current.mousePosition))
            {
                // Check for double-click
                if (actualIndex == _lastClickedTrack &&
                    (Time.realtimeSinceStartup - _lastClickTime) < DoubleClickThreshold)
                {
                    PlaySelectedTrack(actualIndex);
                    _lastClickedTrack = -1;
                }
                else
                {
                    _selectedTrackIndex = actualIndex;
                    _lastClickedTrack = actualIndex;
                    _lastClickTime = Time.realtimeSinceStartup;
                }
                Event.current.Use();
            }
        }

        GUI.contentColor = Color.white;
        GUI.EndScrollView();

        if (CustomMusicManager.IsCustomPlaylistActive)
        {
            _customScrollPos = scrollPos;
        }
        else
        {
            _defaultScrollPos = scrollPos;
        }
    }
    private int GetCurrentDisplayIndex()
    {
        // Null safety checks
        if (MusicPlayer.Instance == null ||
            MusicPlayer.Instance.currentlyPlaying?.playlist == null)
        {
            return -1;
        }

        // Custom playlist handling
        if (CustomMusicManager.IsCustomPlaylistActive)
        {
            if (CustomMusicManager.CustomPlaylist == null)
                return -1;

            // For custom playlists, always return the actual track index
            return CustomMusicManager.CurrentTrackIndex;
        }
        // Default playlist handling
        else
        {
            var playlist = MusicPlayer.Instance.currentlyPlaying.playlist;

            if (playlist.playRandom)
            {
                // For shuffled default playlist:
                if (MusicPlayer.Instance.randomTrackIds == null ||
                    MusicPlayer.Instance.currentRandomId < 0 ||
                    MusicPlayer.Instance.currentRandomId >= MusicPlayer.Instance.randomTrackIds.Count)
                {
                    return -1;
                }
                return MusicPlayer.Instance.randomTrackIds[MusicPlayer.Instance.currentRandomId];
            }
            else
            {
                // For sequential default playlist:
                return MusicPlayer.Instance.currentTrackId;
            }
        }
    }

    //Method to play selected track
    private void PlaySelectedTrack(int trackIndex)
    {
        try
        {
            if (CustomMusicManager.IsCustomPlaylistActive)
            {
                // Use the centralized PlayTrack method for custom playlists
                CustomMusicManager.PlayTrack(trackIndex);
            }
            else
            {
                // Handle default playlist with random flag
                var player = MusicPlayer.Instance;
                var playlist = player.currentlyPlaying.playlist;

                if (playlist.playRandom)
                {
                    // Find position in shuffled list
                    int position = player.randomTrackIds.IndexOf(trackIndex);
                    if (position >= 0)
                    {
                        player.currentRandomId = position;
                        player.ChangePlaylist(playlist, player.randomTrackIds[position], true);
                    }
                }
                else
                {
                    // Direct track access
                    player.ChangePlaylist(playlist, trackIndex, true);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error playing selected track: {e}");
        }
    }

    void Update()
    {
        if (_showGuiShortcut.Value.IsDown()) _showGUI = !_showGUI;
        if (_nextTrackShortcut.Value.IsDown()) CustomMusicManager.PlayNextTrack();

        // Detect playlist changes
        var current = MusicPlayer.Instance?.currentlyPlaying?.playlist;
        if (current != _lastKnownPlaylist)
        {
            _lastKnownPlaylist = current;
        }

        // Auto-advance with safety checks
        if (CustomMusicManager.IsCustomPlaylistActive &&
            CustomMusicManager.CurrentPlayOrder != CustomMusicManager.PlayOrder.Loop)
        {
            // Get a local reference to prevent null changes during execution
            var player = MusicPlayer.Instance;

            if (player != null &&
                player.m_AudioSourceCurrent != null &&
                player.m_AudioSourceCurrent.clip != null)
            {
                // Calculate safe threshold (handles very short clips)
                float endThreshold = Mathf.Max(0.1f, player.m_AudioSourceCurrent.clip.length - 1f);

                if (player.m_AudioSourceCurrent.time >= endThreshold)
                {
                    CustomMusicManager.PlayNextTrack();
                }
            }
        }

        // Reset double-click timer
        if (Time.time - _lastClickTime > DoubleClickTime)
        {
            _selectedTrackIndex = -1;
        }
    }


    private void SetMusicVolumeLinear(float linear)
    {
        // Fallback to direct mixer update
        if (MusicPlayer.Instance?.DefaultMixer?.audioMixer == null)
            return;

        float dB = linear <= 0.01f ? -80f : 20f * Mathf.Log10(linear);
        MusicPlayer.Instance.DefaultMixer.audioMixer.SetFloat("MusicVolume", dB);
    }

    private string FormatTime(float seconds)
    {
        int minutes = (int)(seconds / 60);
        int secs = (int)(seconds % 60);
        return $"{minutes}:{secs:00}";
    }
}