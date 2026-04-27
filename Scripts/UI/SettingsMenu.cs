using Godot;
using Jogomania.Core;

namespace Jogomania.UI
{
    public partial class SettingsMenu : Control
    {
        private TabContainer _tabs;
        private OptionButton _optLanguage;
        
        // Video Settings
        private OptionButton _optResolution;
        private CheckBox _chkFullscreen;
        private HSlider _sldRenderScale;
        private CheckBox _chkVSync;
        private OptionButton _optAntiAliasing;

        // Controls
        private VBoxContainer _pcControlsContainer;
        private VBoxContainer _mobileControlsContainer;

        // Labels textuais para tradução em tempo real
        private Label _titleLabel;
        private Label _lblLang;
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
            _btnClose.Pressed += () => QueueFree();
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
            
            _optLanguage.ItemSelected += (long idx) => {
                string code = (string)_optLanguage.GetItemMetadata((int)idx);
                if (LocalizationManager.Instance != null)
                    LocalizationManager.Instance.LoadLanguage(code);
            };
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

            _chkFullscreen = new CheckBox { Text = "Tela Cheia (Fullscreen)" };
            _chkFullscreen.Toggled += (on) => {
                DisplayServer.WindowSetMode(on ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed);
            };
            vbox.AddChild(_chkFullscreen);

            var hbScale = new HBoxContainer();
            _lblScale = new Label { Text = "Render Scale (Resolução Interna 3D):" };
            hbScale.AddChild(_lblScale);
            _sldRenderScale = new HSlider { MinValue = 0.5f, MaxValue = 2.0f, Step = 0.1f, Value = 1.0f, CustomMinimumSize = new Vector2(200, 0) };
            _sldRenderScale.ValueChanged += (val) => { GetViewport().Scaling3DScale = (float)val; };
            hbScale.AddChild(_sldRenderScale);
            vbox.AddChild(hbScale);

            _chkVSync = new CheckBox { Text = "Sincronização Vertical (V-Sync)" };
            _chkVSync.Toggled += (on) => {
                DisplayServer.WindowSetVsyncMode(on ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled);
            };
            vbox.AddChild(_chkVSync);

            var hbAA = new HBoxContainer();
            _lblAA = new Label { Text = "Suavização (Anti-Aliasing MSAA):" };
            hbAA.AddChild(_lblAA);
            _optAntiAliasing = new OptionButton();
            _optAntiAliasing.AddItem("Desativado");
            _optAntiAliasing.AddItem("2x (MSAA)");
            _optAntiAliasing.AddItem("4x (MSAA)");
            _optAntiAliasing.AddItem("8x (MSAA)");
            _optAntiAliasing.ItemSelected += (idx) => { GetViewport().Msaa3D = (Viewport.Msaa)(int)idx; };
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
            var sldSens = new HSlider { MinValue = 0.1f, MaxValue = 3.0f, Value = 1.0f, CustomMinimumSize = new Vector2(200, 0) };
            var hbSens = new HBoxContainer();
            _lblSens = new Label { Text = "Sensibilidade de Rotação/Pinch:" };
            hbSens.AddChild(_lblSens);
            hbSens.AddChild(sldSens);
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
            _chkFullscreen.ButtonPressed = DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Fullscreen;
            _chkVSync.ButtonPressed = DisplayServer.WindowGetVsyncMode() != DisplayServer.VSyncMode.Disabled;
            _sldRenderScale.Value = GetViewport().Scaling3DScale;
            _optAntiAliasing.Selected = (int)GetViewport().Msaa3D;
        }

        private void AdaptToPlatform()
        {
            string osName = OS.GetName();
            bool isMobile = osName == "Android" || osName == "iOS";

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
            if (_chkFullscreen != null) _chkFullscreen.Text = loc.Translate("settings_fullscreen");
            if (_lblScale != null) _lblScale.Text = loc.Translate("settings_render_scale");
            if (_chkVSync != null) _chkVSync.Text = loc.Translate("settings_vsync");
            if (_lblAA != null) _lblAA.Text = loc.Translate("settings_aa");
            if (_lblPcControls != null) _lblPcControls.Text = loc.Translate("settings_pc_controls");
            if (_lblMobileControls != null) _lblMobileControls.Text = loc.Translate("settings_mobile_controls");
            if (_btnClose != null) _btnClose.Text = loc.Translate("settings_save_return");
        }
    }
}