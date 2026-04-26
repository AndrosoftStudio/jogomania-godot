using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Jogomania.Data;

namespace Jogomania.Core
{
    public partial class GameManager : Node
    {
        public static GameManager Instance { get; private set; }

        public string CurrentMapPath { get; set; }
        public int CurrentAICount { get; set; }
        public int CurrentAggroLevel { get; set; }

        public override void _EnterTree()
        {
            if (Instance == null)
                Instance = this;
            else
                QueueFree();
        }

        public override void _Ready()
        {
            EnsureDir(GetMapsDir());
            EnsureDir(GetPartidasDir());

            if (OS.GetName() == "Android" || OS.GetName() == "iOS")
            {
                int screenDpi = DisplayServer.ScreenGetDpi();
                float scaleFactor = screenDpi > 0 ? Mathf.Clamp(screenDpi / 420f, 1.0f, 1.35f) : 1.0f;
                GetTree().Root.ContentScaleFactor = scaleFactor;
                GD.Print($"[UI Scale] DPI={screenDpi}, ScaleFactor={scaleFactor:F2}x");
            }
        }

        public static string GodotToSysPath(string godotPath)
        {
            return ProjectSettings.GlobalizePath(godotPath);
        }

        public string GetMapsDir()
        {
            string godotDir = "user://saves/maps";
            EnsureDir(godotDir);
            return GodotToSysPath(godotDir);
        }

        public string GetPartidasDir()
        {
            string godotDir = "user://saves/partidas";
            EnsureDir(godotDir);
            return GodotToSysPath(godotDir);
        }

        public string[] GetSavedMapFiles()
        {
            string dir = GetMapsDir();
            if (!Directory.Exists(dir)) return Array.Empty<string>();
            string[] files = Directory.GetFiles(dir, "*.json");
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            return files;
        }

        public static MapData LoadMap(string sysPath)
        {
            if (string.IsNullOrWhiteSpace(sysPath) || !File.Exists(sysPath)) return null;
            string json = File.ReadAllText(sysPath);
            MapData map = JsonSerializer.Deserialize<MapData>(json);
            return RehydrateMap(map);
        }

        public static void SaveMap(string sysPath, MapData mapData)
        {
            if (mapData == null || string.IsNullOrWhiteSpace(sysPath)) return;
            mapData.Dimensions = new Vector2I(mapData.Width, mapData.Height);
            mapData.Chunks ??= new Dictionary<Vector2I, ChunkData>();
            foreach (KeyValuePair<Vector2I, ChunkData> kvp in mapData.Chunks)
            {
                kvp.Value.ChunkPosition = kvp.Key;
                kvp.Value.PosX = kvp.Key.X;
                kvp.Value.PosY = kvp.Key.Y;
                kvp.Value.TerrainMap ??= new byte[ChunkData.CHUNK_SIZE * ChunkData.CHUNK_SIZE];
                kvp.Value.TerritoryMap ??= new byte[ChunkData.CHUNK_SIZE * ChunkData.CHUNK_SIZE];
                kvp.Value.EntityIds ??= new List<int>();
            }

            string dir = Path.GetDirectoryName(sysPath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);
            mapData.ChunksList = new List<ChunkData>(mapData.Chunks.Values);
            string jsonString = JsonSerializer.Serialize(mapData, new JsonSerializerOptions { WriteIndented = false });
            File.WriteAllText(sysPath, jsonString);
        }

        public static MapData RehydrateMap(MapData map)
        {
            if (map == null) return null;
            map.Dimensions = new Vector2I(map.Width, map.Height);
            map.Chunks ??= new Dictionary<Vector2I, ChunkData>();

            if (map.ChunksList != null && map.ChunksList.Count > 0)
            {
                map.Chunks.Clear();
                foreach (ChunkData c in map.ChunksList)
                {
                    if (c == null) continue;
                    c.ChunkPosition = new Vector2I(c.PosX, c.PosY);
                    c.TerrainMap ??= new byte[ChunkData.CHUNK_SIZE * ChunkData.CHUNK_SIZE];
                    c.TerritoryMap ??= new byte[ChunkData.CHUNK_SIZE * ChunkData.CHUNK_SIZE];
                    c.EntityIds ??= new List<int>();
                    map.Chunks[c.ChunkPosition] = c;
                }
            }

            map.Continents ??= new List<Continent>();
            map.Villages ??= new List<VillageData>();
            map.Rivers ??= new List<RiverData>();
            map.ResourceRegions ??= new List<ResourceRegionData>();
            map.ChunksList = new List<ChunkData>(map.Chunks.Values);
            return map;
        }

        public static string CleanMapFileName(string rawName)
        {
            string name = string.IsNullOrWhiteSpace(rawName) ? "mapa_padrao" : rawName.Trim();
            if (name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                name = name.Substring(0, name.Length - 5);

            foreach (char invalid in Path.GetInvalidFileNameChars())
                name = name.Replace(invalid, '_');

            return string.IsNullOrWhiteSpace(name) ? "mapa_padrao" : name;
        }

        public string GetMapPathForName(string rawName)
        {
            return Path.Combine(GetMapsDir(), CleanMapFileName(rawName) + ".json");
        }

        public static string GetNonConflictingPath(string desiredPath)
        {
            if (!File.Exists(desiredPath)) return desiredPath;

            string dir = Path.GetDirectoryName(desiredPath) ?? string.Empty;
            string name = Path.GetFileNameWithoutExtension(desiredPath);
            string ext = Path.GetExtension(desiredPath);
            int index = 2;
            string candidate;
            do
            {
                candidate = Path.Combine(dir, $"{name}({index}){ext}");
                index++;
            }
            while (File.Exists(candidate));

            return candidate;
        }

        private static void EnsureDir(string godotOrSysPath)
        {
            if (godotOrSysPath.StartsWith("user://") || godotOrSysPath.StartsWith("res://"))
                DirAccess.MakeDirRecursiveAbsolute(godotOrSysPath);
            else if (!Directory.Exists(godotOrSysPath))
                Directory.CreateDirectory(godotOrSysPath);
        }

        public static void WriteAllText(string sysPath, string content)
        {
            File.WriteAllText(sysPath, content);
        }

        public static string ReadAllText(string sysPath)
        {
            return File.ReadAllText(sysPath);
        }

        public void StartSoloGame()
        {
            GetTree().ChangeSceneToFile("res://Scenes/Game.tscn");
        }

        public void StartSoloGame(string mapPath, int aiCount, int aggroLevel)
        {
            CurrentMapPath = mapPath;
            CurrentAICount = aiCount;
            CurrentAggroLevel = aggroLevel;

            string matchDir = Path.Combine(GetPartidasDir(), "Slot_1");
            EnsureDir(matchDir);

            string matchFile = Path.Combine(matchDir, "save_atual.json");
            MapData map = LoadMap(mapPath);
            if (map != null)
                SaveMap(matchFile, map);

            GetTree().ChangeSceneToFile("res://Scenes/Game.tscn");
        }

        public void GoToMatchSetup() => GetTree().ChangeSceneToFile("res://Scenes/MatchSetup.tscn");
        public void GoToSettings() => GetTree().ChangeSceneToFile("res://Scenes/SettingsMenu.tscn");
        public void StartOnlineGame() => GetTree().ChangeSceneToFile("res://Scenes/Game.tscn");
        public void OpenMapEditor() => GetTree().ChangeSceneToFile("res://Scenes/MapEditor.tscn");
        public void GoToMainMenu() => GetTree().ChangeSceneToFile("res://Scenes/MainMenu.tscn");
    }
}
