using Godot;
using System;
using System.IO;

namespace Jogomania.Core
{
    public partial class GameManager : Node
    {
        public static GameManager Instance { get; private set; }

        public override void _EnterTree()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                QueueFree();
            }
        }

        public string CurrentMapPath { get; set; }
        public int CurrentAICount { get; set; }
        public int CurrentAggroLevel { get; set; }

        public override void _Ready()
        {
            // Garante que os diretórios existam na primeira execução
            EnsureDir(GetMapsDir());
            EnsureDir(GetPartidasDir());

            // ── Escalonamento Automático de UI para telas de alta densidade (2K, 4K) ──
            // Aplica apenas em dispositivos móveis para não afetar o editor/PC.
            // Baseline: 160 DPI (Android mdpi). Poco X6 Pro ≈ 453 DPI → scale ≈ 2.83x
            if (OS.GetName() == "Android" || OS.GetName() == "iOS")
            {
                int screenDpi = DisplayServer.ScreenGetDpi();
                float scaleFactor = Mathf.Clamp(screenDpi / 240f, 1.0f, 2.5f); // 240dpi = hdpi baseline
                GetTree().Root.ContentScaleFactor = scaleFactor;
                GD.Print($"[UI Scale] DPI={screenDpi}, ScaleFactor={scaleFactor:F2}x");
            }
        }

        // ── Helpers de I/O cross-platform ──────────────────────────────────────────

        /// <summary>
        /// Retorna o caminho REAL no sistema de arquivos correspondente a um path
        /// "user://..." para que o System.IO.File possa usá-lo.
        /// No Android isso mapeia para /data/data/&lt;package&gt;/files/
        /// No Windows/Linux mapeia para %APPDATA%/Jogomania/ ou ~/.local/share/
        /// </summary>
        public static string GodotToSysPath(string godotPath)
        {
            return ProjectSettings.GlobalizePath(godotPath);
        }

        /// <summary>Retorna o diretório user:// dos mapas — funciona em TODAS as plataformas.</summary>
        public string GetMapsDir()
        {
            string godotDir = "user://saves/maps";
            EnsureDir(godotDir);
            return GodotToSysPath(godotDir);
        }

        /// <summary>Retorna o diretório user:// das partidas salvas.</summary>
        public string GetPartidasDir()
        {
            string godotDir = "user://saves/partidas";
            EnsureDir(godotDir);
            return GodotToSysPath(godotDir);
        }

        /// <summary>Cria o diretório (path user://) se não existir — usando Godot DirAccess.</summary>
        private static void EnsureDir(string godotOrSysPath)
        {
            // Se for path user:// usa DirAccess da Godot (funciona no Android)
            if (godotOrSysPath.StartsWith("user://") || godotOrSysPath.StartsWith("res://"))
            {
                DirAccess.MakeDirRecursiveAbsolute(godotOrSysPath);
            }
            else
            {
                // Path de sistema — usa System.IO normal
                if (!Directory.Exists(godotOrSysPath))
                    Directory.CreateDirectory(godotOrSysPath);
            }
        }

        // ── Escrita/Leitura de arquivos cross-platform ─────────────────────────────

        /// <summary>
        /// Escreve texto em um arquivo usando FileAccess da Godot.
        /// Funciona no PC, Android e iOS sem permissões extras.
        /// </summary>
        public static void WriteAllText(string sysPath, string content)
        {
            // Converte path de sistema para user:// se possível — mas FileAccess aceita caminhos globalizados
            // então simplesmente usamos System.IO que funciona desde que o caminho já esteja em GetMapsDir()
            // (que já retorna um path globalizado de user://)
            File.WriteAllText(sysPath, content);
        }

        /// <summary>
        /// Lê texto de um arquivo. Funciona no PC, Android e iOS.
        /// </summary>
        public static string ReadAllText(string sysPath)
        {
            return File.ReadAllText(sysPath);
        }

        // ── Navegação ──────────────────────────────────────────────────────────────

        public void StartSoloGame()
        {
            GetTree().ChangeSceneToFile("res://Scenes/Game.tscn");
        }

        public void StartSoloGame(string mapPath, int aiCount, int aggroLevel)
        {
            CurrentMapPath = mapPath;
            CurrentAICount = aiCount;
            CurrentAggroLevel = aggroLevel;

            string matchDir = GetPartidasDir() + "/Slot_1";
            EnsureDir(matchDir);

            string matchFile = matchDir + "/save_atual.json";
            if (File.Exists(mapPath))
            {
                File.Copy(mapPath, matchFile, true);
            }

            GetTree().ChangeSceneToFile("res://Scenes/Game.tscn");
        }

        public void GoToMatchSetup()
        {
            GetTree().ChangeSceneToFile("res://Scenes/MatchSetup.tscn");
        }

        public void GoToSettings()
        {
            GetTree().ChangeSceneToFile("res://Scenes/SettingsMenu.tscn");
        }

        public void StartOnlineGame()
        {
            GetTree().ChangeSceneToFile("res://Scenes/Game.tscn");
        }

        public void OpenMapEditor()
        {
            GetTree().ChangeSceneToFile("res://Scenes/MapEditor.tscn");
        }

        public void GoToMainMenu()
        {
            GetTree().ChangeSceneToFile("res://Scenes/MainMenu.tscn");
        }
    }
}
