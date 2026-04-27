using Godot;

namespace Jogomania.Core
{
    public enum VideoWindowMode
    {
        Windowed = 0,
        Fullscreen = 1,
        Borderless = 2
    }

    public class UserSettings
    {
        public const string SettingsPath = "user://settings.cfg";

        public string Language { get; set; } = "pt-BR";
        public VideoWindowMode WindowMode { get; set; } = VideoWindowMode.Windowed;
        public int ResolutionWidth { get; set; } = 1280;
        public int ResolutionHeight { get; set; } = 720;
        public bool VSync { get; set; } = true;
        public float RenderScale { get; set; } = 1.0f;
        public int AntiAliasing { get; set; } = 0;
        public float MobileSensitivity { get; set; } = 1.0f;
        public bool ShowFps { get; set; } = false;

        public UserSettings Clone()
        {
            return new UserSettings
            {
                Language = Language,
                WindowMode = WindowMode,
                ResolutionWidth = ResolutionWidth,
                ResolutionHeight = ResolutionHeight,
                VSync = VSync,
                RenderScale = RenderScale,
                AntiAliasing = AntiAliasing,
                MobileSensitivity = MobileSensitivity,
                ShowFps = ShowFps
            };
        }

        public static UserSettings Load()
        {
            var settings = new UserSettings();
            var config = new ConfigFile();
            if (config.Load(SettingsPath) != Error.Ok)
                return settings;

            settings.Language = GetString(config, "General", "Language", settings.Language);
            int fallbackMode = GetBool(config, "Video", "Fullscreen", false) ? (int)VideoWindowMode.Fullscreen : (int)settings.WindowMode;
            settings.WindowMode = (VideoWindowMode)Mathf.Clamp(GetInt(config, "Video", "WindowMode", fallbackMode), 0, 2);
            settings.ResolutionWidth = Mathf.Max(640, GetInt(config, "Video", "ResolutionWidth", settings.ResolutionWidth));
            settings.ResolutionHeight = Mathf.Max(360, GetInt(config, "Video", "ResolutionHeight", settings.ResolutionHeight));
            settings.VSync = GetBool(config, "Video", "VSync", settings.VSync);
            settings.RenderScale = Mathf.Clamp(GetFloat(config, "Video", "RenderScale", settings.RenderScale), 0.5f, 2.0f);
            settings.AntiAliasing = Mathf.Clamp(GetInt(config, "Video", "AntiAliasing", settings.AntiAliasing), 0, 3);
            settings.MobileSensitivity = Mathf.Clamp(GetFloat(config, "Controls", "MobileSensitivity", settings.MobileSensitivity), 0.1f, 3.0f);
            settings.ShowFps = GetBool(config, "Extras", "ShowFps", settings.ShowFps);
            return settings;
        }

        public Error Save()
        {
            var config = new ConfigFile();
            config.SetValue("General", "Language", Language);
            config.SetValue("Video", "Fullscreen", WindowMode == VideoWindowMode.Fullscreen);
            config.SetValue("Video", "WindowMode", (int)WindowMode);
            config.SetValue("Video", "ResolutionWidth", ResolutionWidth);
            config.SetValue("Video", "ResolutionHeight", ResolutionHeight);
            config.SetValue("Video", "VSync", VSync);
            config.SetValue("Video", "RenderScale", RenderScale);
            config.SetValue("Video", "AntiAliasing", AntiAliasing);
            config.SetValue("Controls", "MobileSensitivity", MobileSensitivity);
            config.SetValue("Extras", "ShowFps", ShowFps);
            return config.Save(SettingsPath);
        }

        private static bool GetBool(ConfigFile config, string section, string key, bool fallback)
        {
            Variant value = config.GetValue(section, key, fallback);
            return value.VariantType == Variant.Type.Bool ? value.AsBool() : fallback;
        }

        private static int GetInt(ConfigFile config, string section, string key, int fallback)
        {
            Variant value = config.GetValue(section, key, fallback);
            return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
        }

        private static float GetFloat(ConfigFile config, string section, string key, float fallback)
        {
            Variant value = config.GetValue(section, key, fallback);
            return value.VariantType == Variant.Type.Float || value.VariantType == Variant.Type.Int
                ? (float)value.AsDouble()
                : fallback;
        }

        private static string GetString(ConfigFile config, string section, string key, string fallback)
        {
            Variant value = config.GetValue(section, key, fallback);
            return value.VariantType == Variant.Type.String ? value.AsString() : fallback;
        }
    }
}
