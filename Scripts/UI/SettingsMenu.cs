using Godot;
using Jogomania.Core;
using System.Collections.Generic;

namespace Jogomania.UI
{
    public partial class SettingsMenu : Control
    {
        private TabContainer _tabs;
        private OptionButton _optLanguage;
        
        // Video Settings
        private OptionButton _optWindowMode;
        private OptionButton _optResolution;
        private HSlider _sldRenderScale;
        private CheckBox _chkVSync;
        private OptionButton _optAntiAliasing;
        private HSlider _sldMobileSensitivity;

        // Controls
        private VBoxContainer _pcControlsContainer;
        private VBoxContainer _mobileControlsContainer;

        // Labels textuais para tradução em tempo real
        private Label _titleLabel;
        private Label _lblLang;
        private Label _lblWindowMode;
        private Label _lblResolution;
        private Label _lblScale;
        private Label _lblAA;
        private Label _lblPcControls;
        private Label _lblMobileControls;
        private Label _lblSens;
        private Label _lblCredits;
        private Button _btnClose;
        private CheckBox _chkFps;

        public override void _Ready()
        {
            // Limpa a tela antiga feita pelo Editor Visual para não ficar no fundo
            foreach (Node child in GetChildren())
            {
                child.QueueFree();
            }

            BuildUI();
            LoadCurrentSettings();
            AdaptToPlatform();

            // Inscreve a tela para escutar a mudança de idiomas
            if (LocalizationManager.Instance != null)
            {
                LocalizationManager.Instance.OnLanguageChanged += UpdateTexts;
                UpdateTexts();
            }
        }

        public override void _ExitTree()
        {
            if (LocalizationManager.Instance != null)
            {
                LocalizationManager.Instance.OnLanguageChanged -= UpdateTexts;
            }
        }

        private void BuildUI()
        {
            // Fundo escuro modal
            var bg = new ColorRect { Color = new Color(0, 0, 0, 0.8f) };
            bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(bg);

            var margin = new MarginContainer();
            margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            margin.AddThemeConstantOverride("margin_left", 50);
            margin.AddThemeConstantOverride("margin_top", 50);
            margin.AddThemeConstantOverride("margin_right", 50);
            margin.AddThemeConstantOverride("margin_bottom", 50);
            AddChild(margin);

            var vbox = new VBoxContainer();
            margin.AddChild(vbox);

            _titleLabel = new Label { Text = "Configurações", HorizontalAlignment = HorizontalAlignment.Center };
            _titleLabel.AddThemeFontSizeOverride("font_size", 32);
            vbox.AddChild(_titleLabel);

            _tabs = new TabContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
            vbox.AddChild(_tabs);

            BuildGeneralTab();
            BuildVideoTab();
            BuildControlsTab();
            BuildExtrasTab();

            _btnClose = new Button { Text = "Voltar / Salvar", CustomMinimumSize = new Vector2(200, 50), SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
            _btnClose.Pressed += () => 
            {
                SaveAndApplySettings();
                if (GetParent() == GetTree().Root)
                {
                    GameManager.Instance?.GoToMainMenu();
                }
                else
                {
                    QueueFree();
                }
            };
            vbox.AddChild(_btnClose);
        }

        private void BuildGeneralTab()
        {
            var tab = new MarginContainer { Name = "Geral" };
            tab.AddThemeConstantOverride("margin_left", 20);
            tab.AddThemeConstantOverride("margin_top", 20);
            _tabs.AddChild(tab);

            var vbox = new VBoxContainer();
            tab.AddChild(vbox);

            var hbLang = new HBoxContainer();
            _lblLang = new Label { Text = "Idioma / Language:" };
            hbLang.AddChild(_lblLang);
            _optLanguage = new OptionButton();
            _optLanguage.AddItem("Português (Brasil)", 0);
            _optLanguage.SetItemMetadata(0, "pt-BR");
            _optLanguage.AddItem("English", 1);
            _optLanguage.SetItemMetadata(1, "en");
            _optLanguage.AddItem("Français", 2);
            _optLanguage.SetItemMetadata(2, "fr");
            _optLanguage.AddItem("Español", 3);
            _optLanguage.SetItemMetadata(3, "es");
            _optLanguage.AddItem("中文 (Chinese)", 4);
            _optLanguage.SetItemMetadata(4, "zh");
            _optLanguage.AddItem("Русский (Russian)", 5);
            _optLanguage.SetItemMetadata(5, "ru");

            hbLang.AddChild(_optLanguage);
            vbox.AddChild(hbLang);
        }

        private void BuildVideoTab()
        {
            var tab = new MarginContainer { Name = "Vídeo" };
            tab.AddThemeConstantOverride("margin_left", 20);
            tab.AddThemeConstantOverride("margin_top", 20);
            _tabs.AddChild(tab);

            var vbox = new VBoxContainer();
            tab.AddChild(vbox);

            if (!IsMobilePlatform())
            {
                var hbWindowMode = new HBoxContainer();
                _lblWindowMode = new Label { Text = "Modo de Janela:" };
                hbWindowMode.AddChild(_lblWindowMode);
                _optWindowMode = new OptionButton();
                _optWindowMode.AddItem("Janela", (int)VideoWindowMode.Windowed);
                _optWindowMode.AddItem("Tela Cheia", (int)VideoWindowMode.Fullscreen);
                _optWindowMode.AddItem("Janela Sem Bordas", (int)VideoWindowMode.Borderless);
                hbWindowMode.AddChild(_optWindowMode);
                vbox.AddChild(hbWindowMode);

                var hbResolution = new HBoxContainer();
                _lblResolution = new Label { Text = "Resolucao:" };
                hbResolution.AddChild(_lblResolution);
                _optResolution = new OptionButton();
                PopulateResolutionOptions();
                hbResolution.AddChild(_optResolution);
                vbox.AddChild(hbResolution);
            }

            var hbScale = new HBoxContainer();
            _lblScale = new Label { Text = "Render Scale (Resolução Interna 3D):" };
            hbScale.AddChild(_lblScale);
            _sldRenderScale = new HSlider { MinValue = 0.5f, MaxValue = 2.0f, Step = 0.1f, Value = 1.0f, CustomMinimumSize = new Vector2(200, 0) };
            hbScale.AddChild(_sldRenderScale);
            vbox.AddChild(hbScale);

            _chkVSync = new CheckBox { Text = "Sincronização Vertical (V-Sync)" };
            vbox.AddChild(_chkVSync);

            var hbAA = new HBoxContainer();
            _lblAA = new Label { Text = "Suavização (Anti-Aliasing MSAA):" };
            hbAA.AddChild(_lblAA);
            _optAntiAliasing = new OptionButton();
            _optAntiAliasing.AddItem("Desativado");
            _optAntiAliasing.AddItem("2x (MSAA)");
            _optAntiAliasing.AddItem("4x (MSAA)");
            _optAntiAliasing.AddItem("8x (MSAA)");
            hbAA.AddChild(_optAntiAliasing);
            vbox.AddChild(hbAA);
        }

        private void BuildControlsTab()
        {
            var tab = new MarginContainer { Name = "Controles" };
            tab.AddThemeConstantOverride("margin_left", 20);
            tab.AddThemeConstantOverride("margin_top", 20);
            _tabs.AddChild(tab);

            var scroll = new ScrollContainer();
            tab.AddChild(scroll);

            var vbox = new VBoxContainer();
            scroll.AddChild(vbox);

            _pcControlsContainer = new VBoxContainer();
            _lblPcControls = new Label { Text = "-- Controles de PC (Teclado/Mouse) --" };
            _pcControlsContainer.AddChild(_lblPcControls);
            // (O sistema de remapeamento InputMap completo necessitaria de mais lógica)
            _pcControlsContainer.AddChild(new Label { Text = "Mover Câmera Cima: W\nMover Câmera Baixo: S\nMover Câmera Esquerda: A\nMover Câmera Direita: D\nVisão Tática: Tab\nRotacionar Eixo Z: Ctrl+Arraste Mouse" });
            vbox.AddChild(_pcControlsContainer);

            _mobileControlsContainer = new VBoxContainer();
            _lblMobileControls = new Label { Text = "-- Controles de Celular (Toque) --" };
            _mobileControlsContainer.AddChild(_lblMobileControls);
            _sldMobileSensitivity = new HSlider { MinValue = 0.1f, MaxValue = 3.0f, Step = 0.1f, Value = 1.0f, CustomMinimumSize = new Vector2(200, 0) };
            var hbSens = new HBoxContainer();
            _lblSens = new Label { Text = "Sensibilidade de Rotação/Pinch:" };
            hbSens.AddChild(_lblSens);
            hbSens.AddChild(_sldMobileSensitivity);
            _mobileControlsContainer.AddChild(hbSens);
            vbox.AddChild(_mobileControlsContainer);
        }

        private void BuildExtrasTab()
        {
            var tab = new MarginContainer { Name = "Extras" };
            tab.AddThemeConstantOverride("margin_left", 20);
            tab.AddThemeConstantOverride("margin_top", 20);
            _tabs.AddChild(tab);

            var vbox = new VBoxContainer();
            tab.AddChild(vbox);

            _lblCredits = new Label { Text = "Créditos: Desenvolvido e Renderizado com Godot Engine" };
            vbox.AddChild(_lblCredits);
            _chkFps = new CheckBox { Text = "Mostrar Indicador de FPS no Jogo", ButtonPressed = false };
            vbox.AddChild(_chkFps);
        }

        private void LoadCurrentSettings()
        {
            UserSettings settings = GameManager.Instance?.Settings?.Clone() ?? UserSettings.Load();

            SelectWindowMode(settings.WindowMode);
            SelectResolution(new Vector2I(settings.ResolutionWidth, settings.ResolutionHeight));
            _chkVSync.ButtonPressed = settings.VSync;
            _sldRenderScale.Value = settings.RenderScale;
            _optAntiAliasing.Selected = Mathf.Clamp(settings.AntiAliasing, 0, _optAntiAliasing.ItemCount - 1);
            _sldMobileSensitivity.Value = settings.MobileSensitivity;
            _chkFps.ButtonPressed = settings.ShowFps;
            SelectLanguage(settings.Language);
        }

        private void SelectLanguage(string lang)
        {
            for (int i = 0; i < _optLanguage.ItemCount; i++)
            {
                if ((string)_optLanguage.GetItemMetadata(i) == lang)
                {
                    _optLanguage.Selected = i;
                    return;
                }
            }

            _optLanguage.Selected = 0;
        }

        private void SaveAndApplySettings()
        {
            var settings = new UserSettings
            {
                Language = _optLanguage.Selected >= 0 ? (string)_optLanguage.GetItemMetadata(_optLanguage.Selected) : "pt-BR",
                WindowMode = GetSelectedWindowMode(),
                ResolutionWidth = GetSelectedResolution().X,
                ResolutionHeight = GetSelectedResolution().Y,
                VSync = _chkVSync.ButtonPressed,
                RenderScale = (float)_sldRenderScale.Value,
                AntiAliasing = _optAntiAliasing.Selected,
                MobileSensitivity = (float)_sldMobileSensitivity.Value,
                ShowFps = _chkFps.ButtonPressed
            };

            GameManager.Instance?.SaveAndApplyUserSettings(settings);
        }

        private void PopulateResolutionOptions()
        {
            if (_optResolution == null) return;
            _optResolution.Clear();

            List<Vector2I> resolutions = VideoResolutionProvider.GetAvailable16By9Resolutions();
            foreach (Vector2I resolution in resolutions)
            {
                _optResolution.AddItem($"{resolution.X} x {resolution.Y}");
                _optResolution.SetItemMetadata(_optResolution.ItemCount - 1, resolution);
            }
        }

        private void SelectWindowMode(VideoWindowMode mode)
        {
            if (_optWindowMode == null) return;
            for (int i = 0; i < _optWindowMode.ItemCount; i++)
            {
                if (_optWindowMode.GetItemId(i) == (int)mode)
                {
                    _optWindowMode.Selected = i;
                    return;
                }
            }
            _optWindowMode.Selected = 0;
        }

        private VideoWindowMode GetSelectedWindowMode()
        {
            if (_optWindowMode == null || _optWindowMode.Selected < 0)
                return GameManager.Instance?.Settings?.WindowMode ?? VideoWindowMode.Windowed;

            return (VideoWindowMode)_optWindowMode.GetItemId(_optWindowMode.Selected);
        }

        private void SelectResolution(Vector2I resolution)
        {
            if (_optResolution == null || _optResolution.ItemCount == 0) return;
            for (int i = 0; i < _optResolution.ItemCount; i++)
            {
                if ((Vector2I)_optResolution.GetItemMetadata(i) == resolution)
                {
                    _optResolution.Selected = i;
                    return;
                }
            }
            _optResolution.Selected = 0;
        }

        private Vector2I GetSelectedResolution()
        {
            if (_optResolution == null || _optResolution.Selected < 0)
            {
                UserSettings current = GameManager.Instance?.Settings;
                return current != null
                    ? new Vector2I(current.ResolutionWidth, current.ResolutionHeight)
                    : DisplayServer.ScreenGetSize(DisplayServer.WindowGetCurrentScreen());
            }

            return (Vector2I)_optResolution.GetItemMetadata(_optResolution.Selected);
        }

        private void AdaptToPlatform()
        {
            bool isMobile = IsMobilePlatform();

            if (isMobile)
            {
                _pcControlsContainer.Visible = false;
                _mobileControlsContainer.Visible = true;
                _tabs.SetTabHidden(1, true); // Esconde configurações de vídeo pesadas no Celular
            }
            else
            {
                _pcControlsContainer.Visible = true;
                _mobileControlsContainer.Visible = false;
            }
        }

        private void UpdateTexts()
        {
            var loc = LocalizationManager.Instance;
            if (loc == null) return;

            if (_titleLabel != null) _titleLabel.Text = loc.Translate("settings_title");
            if (_tabs != null)
            {
                _tabs.SetTabTitle(0, loc.Translate("settings_general"));
                _tabs.SetTabTitle(1, loc.Translate("settings_video"));
                _tabs.SetTabTitle(2, loc.Translate("settings_controls"));
                _tabs.SetTabTitle(3, loc.Translate("settings_extras"));
            }
            if (_lblLang != null) _lblLang.Text = loc.Translate("settings_language");
            if (_lblWindowMode != null) _lblWindowMode.Text = loc.Translate("settings_window_mode");
            if (_lblResolution != null) _lblResolution.Text = loc.Translate("settings_resolution");
            if (_optWindowMode != null && _optWindowMode.ItemCount >= 3)
            {
                _optWindowMode.SetItemText(0, loc.Translate("settings_windowed"));
                _optWindowMode.SetItemText(1, loc.Translate("settings_fullscreen"));
                _optWindowMode.SetItemText(2, loc.Translate("settings_borderless"));
            }
            if (_lblScale != null) _lblScale.Text = loc.Translate("settings_render_scale");
            if (_chkVSync != null) _chkVSync.Text = loc.Translate("settings_vsync");
            if (_lblAA != null) _lblAA.Text = loc.Translate("settings_aa");
            if (_lblPcControls != null) _lblPcControls.Text = loc.Translate("settings_pc_controls");
            if (_lblMobileControls != null) _lblMobileControls.Text = loc.Translate("settings_mobile_controls");
            if (_lblSens != null) _lblSens.Text = loc.Translate("settings_mobile_sensitivity");
            if (_lblCredits != null) _lblCredits.Text = loc.Translate("settings_credits");
            if (_chkFps != null) _chkFps.Text = loc.Translate("settings_show_fps");
            if (_btnClose != null) _btnClose.Text = loc.Translate("settings_save_return");
        }

        private bool IsMobilePlatform()
        {
            string osName = OS.GetName();
            return osName == "Android" || osName == "iOS";
        }
    }
}
