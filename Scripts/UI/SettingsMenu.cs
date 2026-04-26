using Godot;
using Jogomania.Core;

namespace Jogomania.UI
{
    public partial class SettingsMenu : Control
    {
        public override void _Ready()
        {
            // Futuramente preencher os botões com as categorias exatas.
        }

        private void OnBtnBackPressed()
        {
            GameManager.Instance?.GoToMainMenu();
        }
    }
}
