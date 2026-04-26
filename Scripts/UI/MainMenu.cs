using Godot;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Jogomania.Core;
using Jogomania.Data;

namespace Jogomania.UI
{
    public partial class MainMenu : Control
    {
        private TextureRect _texturePreview;
        private Label _labelInfo;
        private Button _btnContinue;

        public override void _Ready()
        {
            _texturePreview = GetNode<TextureRect>("MarginContainer/VBoxMain/HBoxContent/PanelPreview/VBoxPreview/TexturePreview");
            _labelInfo = GetNode<Label>("MarginContainer/VBoxMain/HBoxContent/PanelPreview/VBoxPreview/LabelInfo");
            _btnContinue = GetNode<Button>("MarginContainer/VBoxMain/HBoxContent/VBoxButtons/BtnContinue");

            LoadLatestSavePreview();
        }

        private async void LoadLatestSavePreview()
        {
            string savesDir = GameManager.Instance.GetPartidasDir() + "/Slot_1/save_atual.json";
            
            if (File.Exists(savesDir))
            {
                _btnContinue.Disabled = false;
                _labelInfo.Text = "Lendo Dados do Save...";
                
                // Leitura em background para não travar o menu
                var previewData = await Task.Run(() => 
                {
                    string json = File.ReadAllText(savesDir);
                    return JsonSerializer.Deserialize<MapData>(json);
                });

                if (previewData != null)
                {
                    _labelInfo.Text = $"Ano: 1 | Facção: Jogador | Mapa: {previewData.Dimensions.X}x{previewData.Dimensions.Y}";
                    
                    Image previewImage = await Task.Run(() => GeneratePreviewImage(previewData));
                    if (previewImage != null)
                    {
                        _texturePreview.Texture = ImageTexture.CreateFromImage(previewImage);
                    }
                }
            }
            else
            {
                _btnContinue.Disabled = true;
                _labelInfo.Text = "Nenhuma Partida Salva no Slot 1";
            }
        }

        private Image GeneratePreviewImage(MapData mapData)
        {
            int mapW = mapData.Dimensions.X;
            int mapH = mapData.Dimensions.Y;
            
            int previewW = 400;
            int previewH = 200;
            
            float scaleX = (float)mapW / previewW;
            float scaleY = (float)mapH / previewH;

            Image img = Image.CreateEmpty(previewW, previewH, false, Image.Format.Rgba8);

            for (int y = 0; y < previewH; y++)
            {
                for (int x = 0; x < previewW; x++)
                {
                    int realX = (int)(x * scaleX);
                    int realY = (int)(y * scaleY);

                    int chunkX = realX / ChunkData.CHUNK_SIZE;
                    int chunkY = realY / ChunkData.CHUNK_SIZE;
                    Vector2I chunkPos = new Vector2I(chunkX, chunkY);

                    if (mapData.Chunks.TryGetValue(chunkPos, out ChunkData chunk))
                    {
                        int localX = realX % ChunkData.CHUNK_SIZE;
                        int localY = realY % ChunkData.CHUNK_SIZE;
                        byte terrainType = chunk.TerrainMap[localY * ChunkData.CHUNK_SIZE + localX];
                        img.SetPixel(x, y, GetPoliticalTerrainColor(terrainType));
                    }
                    else
                    {
                        img.SetPixel(x, y, new Color(0,0,0));
                    }
                }
            }
            return img;
        }

        private Color GetPoliticalTerrainColor(byte type)
        {
            // Cores mais políticas (Sólidas) para o Menu, como Age of History
            switch (type)
            {
                case 0: return new Color(0.1f, 0.2f, 0.6f); // Ocean
                case 1: return new Color(0.2f, 0.4f, 0.7f); // Sea
                case 2: return new Color(0.8f, 0.7f, 0.5f); // Beach
                case 3: return new Color(0.3f, 0.6f, 0.3f); // Grass
                case 4: return new Color(0.2f, 0.5f, 0.2f); // Forest
                case 5: return new Color(0.1f, 0.4f, 0.1f); // Jungle
                case 6: return new Color(0.7f, 0.6f, 0.4f); // Savanna
                case 7: return new Color(0.8f, 0.7f, 0.3f); // Desert
                case 8: return new Color(0.6f, 0.7f, 0.7f); // Tundra
                case 9: return new Color(0.9f, 0.9f, 0.9f); // Snow
                case 10: return new Color(0.5f, 0.5f, 0.5f); // Mountain
                case 11: return new Color(0.7f, 0.7f, 0.7f); // Peak
                default: return new Color(0,0,0);
            }
        }

        private void OnBtnContinuePressed()
        {
            GameManager.Instance?.StartSoloGame();
        }

        private void OnBtnNewSoloPressed()
        {
            GameManager.Instance?.GoToMatchSetup();
        }

        private void OnBtnMultiplayerPressed()
        {
            GD.Print("Iniciando Fase 9: Cloudflare Tunnels Multiplayer...");
        }

        private void OnBtnSettingsPressed()
        {
            GameManager.Instance?.GoToSettings();
        }

        private void OnBtnEditorPressed()
        {
            GameManager.Instance?.OpenMapEditor();
        }

        private void OnBtnExitPressed()
        {
            GetTree().Quit();
        }
    }
}
