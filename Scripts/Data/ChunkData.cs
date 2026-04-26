using Godot;
using System.Collections.Generic;

namespace Jogomania.Data
{
    public class ChunkData
    {
        [System.Text.Json.Serialization.JsonIgnore]
        public Vector2I ChunkPosition { get; set; }
        
        // Propriedades primitivas para serialização segura
        public int PosX { get; set; }
        public int PosY { get; set; }
        
        public const int CHUNK_SIZE = 64; // Cada chunk tem 64x64 posições (tiles/pixels)
        
        // 0 = Água, 1 = Terra, 2 = Montanha, etc.
        public byte[] TerrainMap { get; set; }
        
        // Território (Dono)
        public byte[] TerritoryMap { get; set; }
        
        // Entidades presentes neste chunk (Vilarejos, Caravanas)
        public List<int> EntityIds { get; set; }

        public ChunkData()
        {
            // Construtor vazio exigido pelo JsonSerializer
        }

        public ChunkData(Vector2I position)
        {
            ChunkPosition = position;
            PosX = position.X;
            PosY = position.Y;
            TerrainMap = new byte[CHUNK_SIZE * CHUNK_SIZE];
            TerritoryMap = new byte[CHUNK_SIZE * CHUNK_SIZE];
            EntityIds = new List<int>();
        }
    }
}
