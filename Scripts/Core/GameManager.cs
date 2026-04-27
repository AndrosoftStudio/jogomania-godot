﻿using Godot;
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

            LoadUserSettings();
        }

        private void LoadUserSettings()
        {
            var config = new ConfigFile();
            if (config.Load("user://settings.cfg") == Godot.Error.Ok)
            {
                bool fs = (bool)config.GetValue("Video", "Fullscreen", false);
                DisplayServer.WindowSetMode(fs ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed);

                bool vsync = (bool)config.GetValue("Video", "VSync", true);
                DisplayServer.WindowSetVsyncMode(vsync ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled);

                float scale = (float)config.GetValue("Video", "RenderScale", 1.0f);
                GetViewport().Scaling3DScale = scale;

                int aa = (int)config.GetValue("Video", "AntiAliasing", 0);
                GetViewport().Msaa3D = (Viewport.Msaa)aa;

                if (LocalizationManager.Instance != null)
                {
                    string lang = (string)config.GetValue("General", "Language", "pt-BR");
                    LocalizationManager.Instance.LoadLanguage(lang);
                }
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
            
            var validMaps = new List<string>();
            foreach (string subDir in Directory.GetDirectories(dir))
            {
                string dirName = Path.GetFileName(subDir);
                string expectedJson = Path.Combine(subDir, dirName + ".json");
                if (File.Exists(expectedJson)) validMaps.Add(expectedJson);
            }
            
            string[] files = validMaps.ToArray();
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            return files;
        }

        public static MapData LoadMap(string sysPath)
        {
            if (string.IsNullOrWhiteSpace(sysPath) || !File.Exists(sysPath)) return null;
            string json = File.ReadAllText(sysPath);
            MapData map = JsonSerializer.Deserialize<MapData>(json);
            return RehydrateMap(map, sysPath);
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
                
            string dbPath = Path.Combine(dir, Path.GetFileNameWithoutExtension(sysPath) + ".db");
            MapDatabase.SaveChunksToDb(dbPath, mapData.Chunks.Values);
            
            mapData.ChunksList = new List<ChunkData>(); // Limpa a lista do JSON para deixá-lo minúsculo
            string jsonString = JsonSerializer.Serialize(mapData, new JsonSerializerOptions { WriteIndented = false });
            File.WriteAllText(sysPath, jsonString);
        }

        public static MapData RehydrateMap(MapData map, string sysPath = null)
        {
            if (map == null) return null;
            map.Dimensions = new Vector2I(map.Width, map.Height);
            map.Chunks ??= new Dictionary<Vector2I, ChunkData>();

            bool dbLoaded = false;
            // Se possuir o caminho, ele carrega as matrizes gigantes de terreno do Banco de Dados
            if (!string.IsNullOrWhiteSpace(sysPath))
            {
                string dbPath = Path.Combine(Path.GetDirectoryName(sysPath), Path.GetFileNameWithoutExtension(sysPath) + ".db");
                if (File.Exists(dbPath))
                {
                    map.Chunks = MapDatabase.LoadAllChunks(dbPath);
                    dbLoaded = true;
                }
            }

            // Retrocompatibilidade para mapas antigos salvos inteiramente no JSON
            if (!dbLoaded && map.ChunksList != null && map.ChunksList.Count > 0)
            {
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
            string clean = CleanMapFileName(rawName);
            return Path.Combine(GetMapsDir(), clean, clean + ".json");
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

            // A pasta do save_atual precisará abrigar o .db também
            string matchDir = Path.Combine(GetPartidasDir(), "Slot_1", "save_atual");
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
