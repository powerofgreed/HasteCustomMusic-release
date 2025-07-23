using BepInEx;
using BepInEx.Configuration;
using System.IO;
using UnityEngine;

public static class PluginConfig
{
    // Config entries
    public static ConfigEntry<bool> ShuffleEnabled;
    public static ConfigEntry<bool> LoopEnabled;
    public static ConfigEntry<bool> LockEnabled;
    public static ConfigEntry<string> MusicPath;
    public static ConfigEntry<KeyboardShortcut> ToggleUIKey;
    public static ConfigEntry<KeyboardShortcut> NextTrackKey;

    public static void Init(ConfigFile config)
    {
        ShuffleEnabled = config.Bind("Settings", "Shuffle", true,
            "Enable shuffle mode for playlists");

        LoopEnabled = config.Bind("Settings", "Loop", false,
            "Enable single track looping");

        LockEnabled = config.Bind("Settings", "Lock", false,
            "Lock to custom playlist mode");

        MusicPath = config.Bind("Paths", "CustomMusic",
            Path.Combine(Application.dataPath, "CustomMusic"),
            "Custom music directory path");

        ToggleUIKey = config.Bind("Hotkeys", "ToggleUI",
            new KeyboardShortcut(KeyCode.F5),
            "Toggle UI visibility");

        NextTrackKey = config.Bind("Hotkeys", "NextTrack",
            new KeyboardShortcut(KeyCode.F6),
            "Play next track");
    }
}