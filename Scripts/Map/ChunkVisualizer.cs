using Godot;
using Jogomania.Data;

namespace Jogomania.Map
{
    public partial class ChunkVisualizer : Sprite2D
    {
        private Image _chunkImage;
        private ImageTexture _chunkTexture;

        private Sprite2D _overlaySprite;
        private Image _overlayImage;
        private ImageTexture _overlayTexture;

        public void RenderChunk(ChunkData chunkData)
        {
            _chunkImage = Image.CreateEmpty(ChunkData.CHUNK_SIZE, ChunkData.CHUNK_SIZE, false, Image.Format.Rgba8);
            
            for (int y = 0; y < ChunkData.CHUNK_SIZE; y++)
            {
                for (int x = 0; x < ChunkData.CHUNK_SIZE; x++)
                {
                    int flatIndex = y * ChunkData.CHUNK_SIZE + x;
                    byte terrainType = chunkData.TerrainMap[flatIndex];
                    
                    _chunkImage.SetPixel(x, y, GetTerrainColor(terrainType));
                }
            }
            
            _chunkTexture = ImageTexture.CreateFromImage(_chunkImage);
            this.Texture = _chunkTexture;
            // Multiplicamos a posição do Chunk pelo tamanho em pixels dele
            this.Position = new Vector2(chunkData.ChunkPosition.X * ChunkData.CHUNK_SIZE, chunkData.ChunkPosition.Y * ChunkData.CHUNK_SIZE);
            this.Centered = false; 

            // Inicializando o Overlay Político
            _overlaySprite = new Sprite2D();
            _overlaySprite.Centered = false;
            AddChild(_overlaySprite);
            
            _overlayImage = Image.CreateEmpty(ChunkData.CHUNK_SIZE, ChunkData.CHUNK_SIZE, false, Image.Format.Rgba8);
            for (int y = 0; y < ChunkData.CHUNK_SIZE; y++)
            {
                for (int x = 0; x < ChunkData.CHUNK_SIZE; x++)
                {
                    int flatIndex = y * ChunkData.CHUNK_SIZE + x;
                    byte territory = chunkData.TerritoryMap[flatIndex];
                    _overlayImage.SetPixel(x, y, GetTerritoryColor(territory));
                }
            }
            _overlayTexture = ImageTexture.CreateFromImage(_overlayImage);
            _overlaySprite.Texture = _overlayTexture;
            _overlaySprite.Modulate = new Color(1, 1, 1, 0.0f); // Invisível por padrão (Zoom In)
        }

        public void UpdatePixel(int localX, int localY, byte terrainType)
        {
            if (_chunkImage == null || _chunkTexture == null) return;
            
            _chunkImage.SetPixel(localX, localY, GetTerrainColor(terrainType));
            _chunkTexture.Update(_chunkImage); // Atualiza a textura na GPU rapidamente
        }

        public void UpdateTerritoryPixel(int localX, int localY, byte ownerId)
        {
            if (_overlayImage == null || _overlayTexture == null) return;
            _overlayImage.SetPixel(localX, localY, GetTerritoryColor(ownerId));
            _overlayTexture.Update(_overlayImage);
        }

        public void SetOverlayOpacity(float alpha)
        {
            if (_overlaySprite != null)
                _overlaySprite.Modulate = new Color(1, 1, 1, alpha);
        }

        private Color GetTerrainColor(byte type)
        {
            switch (type)
            {
                case 0: return new Color(0.015f, 0.075f, 0.22f); // DeepOcean
                case 1: return new Color(0.12f, 0.36f, 0.62f); // ShallowWater
                case 2: return new Color(0.84f, 0.76f, 0.52f); // Beach
                case 3: return new Color(0.42f, 0.62f, 0.30f); // Grassland
                case 4: return new Color(0.16f, 0.38f, 0.16f); // Forest
                case 5: return new Color(0.05f, 0.27f, 0.10f); // Jungle
                case 6: return new Color(0.63f, 0.56f, 0.34f); // Savanna
                case 7: return new Color(0.78f, 0.60f, 0.28f); // Desert
                case 8: return new Color(0.60f, 0.68f, 0.64f); // Tundra
                case 9: return new Color(0.9f, 0.95f, 1.0f); // Snow
                case 10: return new Color(0.47f, 0.45f, 0.40f); // Mountain
                case 11: return new Color(0.78f, 0.76f, 0.70f); // HighPeak
                default: return new Color(0,0,0);
            }
        }

        private Color GetTerritoryColor(byte ownerId)
        {
            if (ownerId == 0) return new Color(0, 0, 0, 0); // Neutro = Transparente
            if (ownerId == 1) return new Color(0.2f, 0.4f, 0.9f, 1.0f); // Player 1 = Azul
            if (ownerId == 2) return new Color(0.9f, 0.2f, 0.2f, 1.0f); // Enemy 1 = Vermelho
            if (ownerId == 3) return new Color(0.2f, 0.9f, 0.2f, 1.0f); // Enemy 2 = Verde
            if (ownerId == 4) return new Color(0.9f, 0.9f, 0.2f, 1.0f); // Enemy 3 = Amarelo
            return new Color(1, 1, 1, 1.0f);
        }
    }
}
