using Godot;
using System.Collections.Generic;
using System.Text.Json;

namespace Jogomania.Core
{
    public partial class LocalizationManager : Node
    {
        public static LocalizationManager Instance { get; private set; }

        public string CurrentLanguage { get; private set; } = "pt-BR";
        private Dictionary<string, string> _translations = new Dictionary<string, string>();

        public delegate void LanguageChangedEventHandler();
        public event LanguageChangedEventHandler OnLanguageChanged;

        public override void _EnterTree()
        {
            if (Instance == null) Instance = this;
            else QueueFree();
        }

        public void LoadLanguage(string langCode)
        {
            CurrentLanguage = langCode;
            _translations.Clear();

            string path = $"res://Scripts/Core/assets/language/{langCode}.json";
            if (FileAccess.FileExists(path))
            {
                using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
                string json = file.GetAsText();
                _translations = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
            }
            else
            {
                GD.PrintErr($"Arquivo de idioma não encontrado: {path}");
            }

            OnLanguageChanged?.Invoke();
        }

        public string Translate(string key)
        {
            if (_translations.TryGetValue(key, out string value))
                return value;
            return key; // Retorna a chave original caso a tradução falhe
        }
    }
}