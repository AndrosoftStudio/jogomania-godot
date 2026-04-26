using Godot;
using System.Collections.Generic;
using System;
using Jogomania.Data;
using Jogomania.Map;

namespace Jogomania.ECS
{
    public partial class TerritorySystem : Node
    {
        public static TerritorySystem Instance { get; private set; }

        private MapData _mapData;
        private bool _isRunning = false;
        private double _timeSinceLastTick = 0;
        public float TickRate = 1.0f; // 1 expansão por segundo

        // Cada vila tem uma fila de blocos na borda que podem ser expandidos
        // Chave: ID do dono (1 = Player, 2+ = IAs)
        private Dictionary<int, Queue<Vector2I>> _frontierQueues = new Dictionary<int, Queue<Vector2I>>();

        // Acesso direto para pintar o Globo 3D
        private Action<int, int, byte> _onTerritoryChanged;

        public override void _EnterTree()
        {
            if (Instance == null) Instance = this;
            else QueueFree();
        }

        public void StartSimulation(MapData mapData, Action<int, int, byte> onTerritoryChanged)
        {
            _mapData = mapData;
            _onTerritoryChanged = onTerritoryChanged;
            _frontierQueues.Clear();
            _isRunning = true;
        }

        public void StopSimulation()
        {
            _isRunning = false;
        }

        public void RegisterVillage(Vector2I tilePos, int ownerId)
        {
            if (!_frontierQueues.ContainsKey(ownerId))
                _frontierQueues[ownerId] = new Queue<Vector2I>();

            // Força a posse do Tile Inicial (Capital)
            int chunkX = tilePos.X / ChunkData.CHUNK_SIZE;
            int chunkY = tilePos.Y / ChunkData.CHUNK_SIZE;
            Vector2I chunkPos = new Vector2I(chunkX, chunkY);

            if (_mapData.Chunks.TryGetValue(chunkPos, out ChunkData chunk))
            {
                int localX = tilePos.X % ChunkData.CHUNK_SIZE;
                int localY = tilePos.Y % ChunkData.CHUNK_SIZE;
                chunk.TerritoryMap[localY * ChunkData.CHUNK_SIZE + localX] = (byte)ownerId;
                
                _onTerritoryChanged?.Invoke(tilePos.X, tilePos.Y, (byte)ownerId);
            }

            _frontierQueues[ownerId].Enqueue(tilePos);
        }

        public override void _Process(double delta)
        {
            if (!_isRunning) return;

            _timeSinceLastTick += delta;
            if (_timeSinceLastTick >= TickRate)
            {
                _timeSinceLastTick = 0;
                ProcessExpansionTick();
            }
        }

        private void ProcessExpansionTick()
        {
            // Para cada nação, processa uma certa quantidade de blocos de fronteira
            foreach (var kvp in _frontierQueues)
            {
                int ownerId = kvp.Key;
                Queue<Vector2I> frontier = kvp.Value;

                // Limitar número de processamentos por frame para não travar
                int tilesToProcess = frontier.Count;
                for (int i = 0; i < tilesToProcess; i++)
                {
                    Vector2I current = frontier.Dequeue();

                    // Ruído Orgânico: 30% de chance de atrasar essa célula para o próximo tick.
                    // Isso quebra a forma de diamante perfeito e cria fronteiras squiggly (orgânicas)
                    if (GD.Randf() < 0.30f)
                    {
                        frontier.Enqueue(current);
                        continue;
                    }

                    // Tentar expandir para os 4 vizinhos
                    TryExpandTo(current.X + 1, current.Y, (byte)ownerId, frontier);
                    TryExpandTo(current.X - 1, current.Y, (byte)ownerId, frontier);
                    TryExpandTo(current.X, current.Y + 1, (byte)ownerId, frontier);
                    TryExpandTo(current.X, current.Y - 1, (byte)ownerId, frontier);
                }
            }
        }

        private void TryExpandTo(int x, int y, byte ownerId, Queue<Vector2I> frontier)
        {
            // Evita sair do mapa horizontalmente (Opcional: Wrap-around implementado aqui no futuro)
            if (x < 0 || x >= _mapData.Dimensions.X || y < 0 || y >= _mapData.Dimensions.Y) return;

            int chunkX = x / ChunkData.CHUNK_SIZE;
            int chunkY = y / ChunkData.CHUNK_SIZE;
            Vector2I chunkPos = new Vector2I(chunkX, chunkY);

            if (_mapData.Chunks.TryGetValue(chunkPos, out ChunkData chunk))
            {
                int localX = x % ChunkData.CHUNK_SIZE;
                int localY = y % ChunkData.CHUNK_SIZE;
                int flatIndex = localY * ChunkData.CHUNK_SIZE + localX;

                byte currentOwner = chunk.TerritoryMap[flatIndex];
                byte terrain = chunk.TerrainMap[flatIndex];

                // Regra de Expansão: Só domina se for Terra firme tolerável (Terrain >= 3, mas não montanha extrema 11) e sem dono civilizado (0 ou 255-aldeia neutra)
                if ((currentOwner == 0 || currentOwner == 255) && terrain >= 3 && terrain != 11)
                {
                    chunk.TerritoryMap[flatIndex] = ownerId;
                    
                    // Atualiza o visual (Overlay no Globo)
                    _onTerritoryChanged?.Invoke(x, y, ownerId);

                    // Coloca na fila para expandir no próximo tick
                    frontier.Enqueue(new Vector2I(x, y));
                }
            }
        }
    }
}
