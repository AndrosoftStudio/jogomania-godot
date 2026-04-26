using Godot;
using System.Collections.Generic;

namespace Jogomania.Data
{
    // Armazena os dados serializáveis do mapa
    public class MapData
    {
        public string MapName { get; set; }
        
        [System.Text.Json.Serialization.JsonIgnore]
        public Vector2I Dimensions { get; set; }
        
        // Propriedades para serialização
        public int Width { get; set; }
        public int Height { get; set; }

        public List<Continent> Continents { get; set; } = new List<Continent>();
        public List<VillageData> Villages { get; set; } = new List<VillageData>();

        [System.Text.Json.Serialization.JsonIgnore]
        public Dictionary<Vector2I, ChunkData> Chunks { get; set; } = new Dictionary<Vector2I, ChunkData>();

        // Usado apenas para serialização/desserialização porque System.Text.Json não suporta Vector2I como chave de dicionário
        public List<ChunkData> ChunksList { get; set; } = new List<ChunkData>();
        
        // Opcionalmente podemos ter dados ECS aqui, mas a estrutura ECS geralmente fica separada
    }
}
