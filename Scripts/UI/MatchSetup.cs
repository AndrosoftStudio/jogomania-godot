﻿using Godot;
using System.IO;
using Jogomania.Core;

namespace Jogomania.UI
{
    public partial class MatchSetup : Control
    {
        private Label _labelTitle;
        private Label _labelMap;
        private Label _labelAI;
        private Label _labelAggressiveness;
        private OptionButton _optionMap;
        private SpinBox _spinBoxAI;
        private OptionButton _optionAggro;
        private Button _btnBack;
        private Button _btnStart;
        private bool _hasMaps;

        public override void _Ready()
        {
            _labelTitle = GetNode<Label>("MarginContainer/VBoxMain/LabelTitle");
            _labelMap = GetNode<Label>("MarginContainer/VBoxMain/PanelContainer/VBoxSettings/HBoxMap/Label");
            _labelAI = GetNode<Label>("MarginContainer/VBoxMain/PanelContainer/VBoxSettings/HBoxAI/Label");
            _labelAggressiveness = GetNode<Label>("MarginContainer/VBoxMain/PanelContainer/VBoxSettings/HBoxAggressiveness/Label");
            _optionMap = GetNode<OptionButton>("MarginContainer/VBoxMain/PanelContainer/VBoxSettings/HBoxMap/OptionMap");
            _spinBoxAI = GetNode<SpinBox>("MarginContainer/VBoxMain/PanelContainer/VBoxSettings/HBoxAI/SpinBoxAI");
            _optionAggro = GetNode<OptionButton>("MarginContainer/VBoxMain/PanelContainer/VBoxSettings/HBoxAggressiveness/OptionAggro");
            _btnBack = GetNode<Button>("MarginContainer/VBoxMain/HBoxButtons/BtnBack");
            _btnStart = GetNode<Button>("MarginContainer/VBoxMain/HBoxButtons/BtnStart");

            if (LocalizationManager.Instance != null)
                LocalizationManager.Instance.OnLanguageChanged += UpdateTexts;

            LoadAvailableMaps();
            UpdateTexts();
        }

        public override void _ExitTree()
        {
            if (LocalizationManager.Instance != null)
                LocalizationManager.Instance.OnLanguageChanged -= UpdateTexts;
        }

        private void LoadAvailableMaps()
        {
            _optionMap.Clear();
            foreach (string file in GameManager.Instance.GetSavedMapFiles())
            {
                _optionMap.AddItem(Path.GetFileNameWithoutExtension(file));
                _optionMap.SetItemMetadata(_optionMap.ItemCount - 1, file);
            }

            if (_optionMap.ItemCount == 0)
            {
                _optionMap.AddItem(Tr("setup_no_maps"));
                _optionMap.Disabled = true;
                _btnStart.Disabled = true;
            }
            _hasMaps = _optionMap.ItemCount > 0 && !_optionMap.Disabled;
        }

        private void UpdateTexts()
        {
            _labelTitle.Text = Tr("setup_title");
            _labelMap.Text = Tr("setup_map");
            _labelAI.Text = Tr("setup_ai_count");
            _labelAggressiveness.Text = Tr("setup_ai_aggressiveness");
            _btnBack.Text = Tr("setup_back");
            _btnStart.Text = Tr("setup_start");

            _optionAggro.SetItemText(0, Tr("setup_aggro_passive"));
            _optionAggro.SetItemText(1, Tr("setup_aggro_normal"));
            _optionAggro.SetItemText(2, Tr("setup_aggro_aggressive"));

            if (!_hasMaps && _optionMap.ItemCount > 0)
                _optionMap.SetItemText(0, Tr("setup_no_maps"));
        }

        private string Tr(string key)
        {
            return LocalizationManager.Instance?.Translate(key) ?? key;
        }

        private void OnBtnBackPressed() => GameManager.Instance?.GoToMainMenu();

        private void OnBtnStartPressed()
        {
            if (_optionMap.Disabled || _optionMap.Selected < 0) return;
            string mapPath = (string)_optionMap.GetItemMetadata(_optionMap.Selected);
            int aiCount = (int)_spinBoxAI.Value;
            int aggroLevel = _optionAggro.Selected;
            string selectedMapName = _optionMap.GetItemText(_optionMap.Selected);
            GD.Print($"Iniciando Jogo: Mapa={selectedMapName}, IAs={aiCount}, Agressividade={aggroLevel}");
            GameManager.Instance?.StartSoloGame(mapPath, aiCount, aggroLevel);
        }
    }
}
