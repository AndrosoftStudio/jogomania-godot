using Godot;
using System.IO;
using Jogomania.Core;

namespace Jogomania.UI
{
    public partial class MatchSetup : Control
    {
        private OptionButton _optionMap;
        private SpinBox _spinBoxAI;
        private OptionButton _optionAggro;

        public override void _Ready()
        {
            _optionMap = GetNode<OptionButton>("MarginContainer/VBoxMain/PanelContainer/VBoxSettings/HBoxMap/OptionMap");
            _spinBoxAI = GetNode<SpinBox>("MarginContainer/VBoxMain/PanelContainer/VBoxSettings/HBoxAI/SpinBoxAI");
            _optionAggro = GetNode<OptionButton>("MarginContainer/VBoxMain/PanelContainer/VBoxSettings/HBoxAggressiveness/OptionAggro");

            LoadAvailableMaps();
        }

        private void LoadAvailableMaps()
        {
            _optionMap.Clear();
            string dir = GameManager.Instance.GetMapsDir();

            string[] files = Directory.GetFiles(dir, "*.json");
            foreach (string file in files)
            {
                _optionMap.AddItem(Path.GetFileName(file));
            }

            if (_optionMap.ItemCount == 0)
            {
                _optionMap.AddItem("Nenhum mapa encontrado. Use o Editor primeiro.");
                _optionMap.Disabled = true;
                GetNode<Button>("MarginContainer/VBoxMain/HBoxButtons/BtnStart").Disabled = true;
            }
        }

        private void OnBtnBackPressed()
        {
            GameManager.Instance?.GoToMainMenu();
        }

        private void OnBtnStartPressed()
        {
            string selectedMapName = _optionMap.GetItemText(_optionMap.Selected);
            string mapPath = GameManager.Instance.GetMapsDir() + "/" + selectedMapName;

            int aiCount = (int)_spinBoxAI.Value;
            int aggroLevel = _optionAggro.Selected;

            // TODO: Passar os parâmetros de IA e Mapa para o GameManager para que a Cena Game leia.
            GD.Print($"Iniciando Jogo: Mapa={selectedMapName}, IAs={aiCount}, Agressividade={aggroLevel}");
            
            GameManager.Instance?.StartSoloGame(mapPath, aiCount, aggroLevel);
        }
    }
}
