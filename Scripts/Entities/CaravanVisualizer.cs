using Godot;

namespace Jogomania.Entities
{
    public partial class CaravanVisualizer : Node2D
    {
        private Caravan _caravanData;
        private Sprite2D _sprite;
        private Label _nameLabel;

        public void Setup(Caravan data)
        {
            _caravanData = data;
            
            // Criar um Sprite para representar a Caravana no mundo 2D
            _sprite = new Sprite2D();
            _sprite.Texture = GD.Load<Texture2D>("res://icon.svg");
            _sprite.Scale = new Vector2(0.2f, 0.2f); // Escala reduzida
            AddChild(_sprite);

            // Adicionar texto flutuante para identificar a população
            _nameLabel = new Label();
            _nameLabel.Text = $"Caravana Jogador {_caravanData.OwnerPlayerId}\nPop: {_caravanData.Inhabitants.Count}";
            _nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _nameLabel.Position = new Vector2(-75, -40); // Centralizado acima do icone
            AddChild(_nameLabel);

            // Mover para a posição definida pela regra de negócios da Caravana
            this.Position = new Vector2(_caravanData.Position.X, _caravanData.Position.Y);
        }
    }
}
