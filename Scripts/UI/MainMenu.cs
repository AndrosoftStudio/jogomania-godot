﻿using Godot;
using System.IO;
using System.Threading.Tasks;
using Jogomania.Core;
using Jogomania.Data;

namespace Jogomania.UI
{
    public partial class MainMenu : Control
    {
        private TextureRect _texturePreview;
        private Label _labelInfo;
        private Label _labelPreviewTitle;
        private Button _btnContinue;
        private Button _btnNewSolo;
        private Button _btnMultiplayer;
        private Button _btnSettings;
        private Button _btnEditor;
        private Button _btnExit;
        private int _previewRequestId = 0;

        public override void _Ready()
        {
            _texturePreview = GetNode<TextureRect>("MarginContainer/VBoxMain/HBoxContent/PanelPreview/VBoxPreview/TexturePreview");
            _labelInfo = GetNode<Label>("MarginContainer/VBoxMain/HBoxContent/PanelPreview/VBoxPreview/LabelInfo");
            _labelPreviewTitle = GetNode<Label>("MarginContainer/VBoxMain/HBoxContent/PanelPreview/VBoxPreview/LabelPreviewTitle");
            _btnContinue = GetNode<Button>("MarginContainer/VBoxMain/HBoxContent/VBoxButtons/BtnContinue");
            _btnNewSolo = GetNode<Button>("MarginContainer/VBoxMain/HBoxContent/VBoxButtons/BtnNewSolo");
            _btnMultiplayer = GetNode<Button>("MarginContainer/VBoxMain/HBoxContent/VBoxButtons/BtnMultiplayer");
            _btnSettings = GetNode<Button>("MarginContainer/VBoxMain/HBoxContent/VBoxButtons/BtnSettings");
            _btnEditor = GetNode<Button>("MarginContainer/VBoxMain/HBoxContent/VBoxButtons/BtnEditor");
            _btnExit = GetNode<Button>("MarginContainer/VBoxMain/HBoxContent/VBoxButtons/BtnExit");

            if (LocalizationManager.Instance != null)
            {
                LocalizationManager.Instance.OnLanguageChanged += OnLanguageChanged;
                UpdateTexts();
            }

            LoadLatestSavePreview();
        }

        private async void LoadLatestSavePreview()
        {
            int requestId = ++_previewRequestId;
            string savePath = Path.Combine(GameManager.Instance.GetPartidasDir(), "Slot_1", "save_atual", "save_atual.json");
            if (!File.Exists(savePath))
            {
                if (!CanApplyPreviewResult(requestId)) return;
                _btnContinue.Disabled = true;
                _labelInfo.Text = Tr("menu_no_saved_match");
                return;
            }

            _btnContinue.Disabled = false;
            _labelInfo.Text = Tr("menu_reading_save");
            MapData previewData = await Task.Run(() => GameManager.LoadMap(savePath));
            if (!CanApplyPreviewResult(requestId)) return;
            if (previewData == null)
            {
                _btnContinue.Disabled = true;
                _labelInfo.Text = Tr("menu_save_load_error");
                return;
            }

            _labelInfo.Text = string.Format(Tr("menu_preview_info"), 1, Tr("menu_player_faction"), previewData.Width, previewData.Height);
            Image previewImage = await Task.Run(() => GeneratePreviewImage(previewData));
            if (!CanApplyPreviewResult(requestId)) return;
            if (previewImage != null)
                _texturePreview.Texture = ImageTexture.CreateFromImage(previewImage);
        }

        public override void _ExitTree()
        {
            _previewRequestId++;
            if (LocalizationManager.Instance != null)
                LocalizationManager.Instance.OnLanguageChanged -= OnLanguageChanged;
        }

        private void OnLanguageChanged()
        {
            UpdateTexts();
            LoadLatestSavePreview();
        }

        private void UpdateTexts()
        {
            var loc = LocalizationManager.Instance;
            if (loc == null) return;

            _btnContinue.Text = loc.Translate("menu_continue");
            _btnNewSolo.Text = loc.Translate("menu_new_game");
            _btnMultiplayer.Text = loc.Translate("menu_multiplayer");
            _btnSettings.Text = loc.Translate("menu_settings");
            _btnEditor.Text = loc.Translate("menu_editor");
            _btnExit.Text = loc.Translate("menu_exit");
            _labelPreviewTitle.Text = loc.Translate("menu_latest_match");
        }

        private string Tr(string key)
        {
            return LocalizationManager.Instance?.Translate(key) ?? key;
        }

        private bool CanApplyPreviewResult(int requestId)
        {
            return requestId == _previewRequestId
                && GodotObject.IsInstanceValid(this)
                && GodotObject.IsInstanceValid(_labelInfo)
                && GodotObject.IsInstanceValid(_btnContinue)
                && GodotObject.IsInstanceValid(_texturePreview);
        }

        private Image GeneratePreviewImage(MapData mapData)
        {
            int mapW = Mathf.Max(1, mapData.Width);
            int mapH = Mathf.Max(1, mapData.Height);
            int previewW = 400;
            int previewH = 200;
            float scaleX = (float)mapW / previewW;
            float scaleY = (float)mapH / previewH;
            Image img = Image.CreateEmpty(previewW, previewH, false, Image.Format.Rgba8);

            for (int y = 0; y < previewH; y++)
            {
                for (int x = 0; x < previewW; x++)
                {
                    int realX = Mathf.Clamp((int)(x * scaleX), 0, mapW - 1);
                    int realY = Mathf.Clamp((int)(y * scaleY), 0, mapH - 1);
                    byte terrain = Jogomania.Map.PlanetMeshBuilder.GetTerrainAt(mapData, realX, realY);
                    img.SetPixel(x, y, GetPoliticalTerrainColor(terrain));
                }
            }
            return img;
        }

        private Color GetPoliticalTerrainColor(byte type)
        {
            return type switch
            {
                0 => new Color(0.1f, 0.2f, 0.6f),
                1 => new Color(0.2f, 0.4f, 0.7f),
                2 => new Color(0.8f, 0.7f, 0.5f),
                3 => new Color(0.3f, 0.6f, 0.3f),
                4 => new Color(0.2f, 0.5f, 0.2f),
                5 => new Color(0.1f, 0.4f, 0.1f),
                6 => new Color(0.7f, 0.6f, 0.4f),
                7 => new Color(0.8f, 0.7f, 0.3f),
                8 => new Color(0.6f, 0.7f, 0.7f),
                9 => new Color(0.9f, 0.9f, 0.9f),
                10 => new Color(0.5f, 0.5f, 0.5f),
                11 => new Color(0.7f, 0.7f, 0.7f),
                _ => Colors.Black
            };
        }

        private void OnBtnContinuePressed() => GameManager.Instance?.StartSoloGame();
        private void OnBtnNewSoloPressed() => GameManager.Instance?.GoToMatchSetup();
        private void OnBtnMultiplayerPressed() => GD.Print("Iniciando Fase 9: Cloudflare Tunnels Multiplayer...");
        private void OnBtnSettingsPressed() => GameManager.Instance?.GoToSettings();
        private void OnBtnEditorPressed() => GameManager.Instance?.OpenMapEditor();
        private void OnBtnExitPressed() => GetTree().Quit();
    }
}
