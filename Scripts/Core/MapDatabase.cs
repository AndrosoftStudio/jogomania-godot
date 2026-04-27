using Godot;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Jogomania.Data;

namespace Jogomania.Core
{
    public static class MapDatabase
    {
        public static void InitializeDb(string dbPath)
        {
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS Chunks (
                    PosX INTEGER,
                    PosY INTEGER,
                    TerrainData BLOB,
                    TerritoryData BLOB,
                    EntityIds TEXT,
                    PRIMARY KEY (PosX, PosY)
                );
            ";
            command.ExecuteNonQuery();
        }

        public static void SaveChunksToDb(string dbPath, IEnumerable<ChunkData> chunks)
        {
            InitializeDb(dbPath);
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();
            using var transaction = connection.BeginTransaction();
            
            var command = connection.CreateCommand();
            command.CommandText = "INSERT OR REPLACE INTO Chunks (PosX, PosY, TerrainData, TerritoryData, EntityIds) VALUES ($x, $y, $terrain, $territory, $entities)";

            var pX = command.CreateParameter(); pX.ParameterName = "$x"; command.Parameters.Add(pX);
            var pY = command.CreateParameter(); pY.ParameterName = "$y"; command.Parameters.Add(pY);
            var pTerrain = command.CreateParameter(); pTerrain.ParameterName = "$terrain"; command.Parameters.Add(pTerrain);
            var pTerritory = command.CreateParameter(); pTerritory.ParameterName = "$territory"; command.Parameters.Add(pTerritory);
            var pEntities = command.CreateParameter(); pEntities.ParameterName = "$entities"; command.Parameters.Add(pEntities);

            foreach (var chunk in chunks)
            {
                pX.Value = chunk.PosX;
                pY.Value = chunk.PosY;
                pTerrain.Value = chunk.TerrainMap;
                pTerritory.Value = chunk.TerritoryMap;
                pEntities.Value = JsonSerializer.Serialize(chunk.EntityIds ?? new List<int>());
                command.ExecuteNonQuery();
            }
            transaction.Commit();
        }

        public static Dictionary<Vector2I, ChunkData> LoadAllChunks(string dbPath)
        {
            var dict = new Dictionary<Vector2I, ChunkData>();
            if (!File.Exists(dbPath)) return dict;

            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "SELECT PosX, PosY, TerrainData, TerritoryData, EntityIds FROM Chunks";
            
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var chunk = new ChunkData(new Vector2I(reader.GetInt32(0), reader.GetInt32(1)));
                chunk.TerrainMap = (byte[])reader["TerrainData"];
                chunk.TerritoryMap = (byte[])reader["TerritoryData"];
                chunk.EntityIds = JsonSerializer.Deserialize<List<int>>(reader.GetString(4));
                dict[chunk.ChunkPosition] = chunk;
            }
            return dict;
        }
    }
}