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
        public List<RiverData> Rivers { get; set; } = new List<RiverData>();
        public List<ResourceRegionData> ResourceRegions { get; set; } = new List<ResourceRegionData>();

        public int MoonTextureWidth { get; set; } = 0;
        public int MoonTextureHeight { get; set; } = 0;
        public byte[] MoonTextureData { get; set; }

        public int SunTextureWidth { get; set; } = 0;
        public int SunTextureHeight { get; set; } = 0;
        public byte[] SunTextureData { get; set; }
        public int CelestialTextureVersion { get; set; } = 0;

        [System.Text.Json.Serialization.JsonIgnore]
        public Dictionary<Vector2I, ChunkData> Chunks { get; set; } = new Dictionary<Vector2I, ChunkData>();

        // Usado apenas para serialização/desserialização porque System.Text.Json não suporta Vector2I como chave de dicionário
        public List<ChunkData> ChunksList { get; set; } = new List<ChunkData>();
        
        // Opcionalmente podemos ter dados ECS aqui, mas a estrutura ECS geralmente fica separada
    }

    public class RiverData
    {
        public List<MapPointData> Points { get; set; } = new List<MapPointData>();
        public float Width { get; set; } = 1.0f;
    }

    public class MapPointData
    {
        public int X { get; set; }
        public int Y { get; set; }
    }

    public class ResourceRegionData
    {
        public string ResourceId { get; set; }
        public string Category { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Radius { get; set; }
        public float Richness { get; set; }
        public float MigrationAngle { get; set; }
        public float MigrationSpeed { get; set; }
    }
}
