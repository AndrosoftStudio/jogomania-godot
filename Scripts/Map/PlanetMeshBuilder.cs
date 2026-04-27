using Godot;
using Jogomania.Data;

namespace Jogomania.Map
{
    public static class PlanetMeshBuilder
    {
        public const float BaseRadius = 1.0f;

        public static ArrayMesh CreatePlanetMesh(MapData mapData, int lonSegments = 160, int latSegments = 80)
        {
            var surface = new SurfaceTool();
            surface.Begin(Mesh.PrimitiveType.Triangles);

            for (int y = 0; y < latSegments; y++)
            {
                float v0 = (float)y / latSegments;
                float v1 = (float)(y + 1) / latSegments;
                for (int x = 0; x < lonSegments; x++)
                {
                    float u0 = (float)x / lonSegments;
                    float u1 = (float)(x + 1) / lonSegments;

                    AddVertex(surface, mapData, u0, v0);
                    AddVertex(surface, mapData, u0, v1);
                    AddVertex(surface, mapData, u1, v0);

                    AddVertex(surface, mapData, u1, v0);
                    AddVertex(surface, mapData, u0, v1);
                    AddVertex(surface, mapData, u1, v1);
                }
            }

            return surface.Commit();
        }

        private static void AddVertex(SurfaceTool surface, MapData mapData, float u, float v)
        {
            int mapX = WrapX(Mathf.FloorToInt(u * mapData.Width), mapData.Width);
            int mapY = WrapY(Mathf.FloorToInt(v * mapData.Height), mapData.Height);
            float height = GetSmoothedTerrainHeight(mapData, mapX, mapY);
            Vector3 normal = UvToSphereDirection(u, v);
            surface.SetNormal(normal);
            surface.SetUV(new Vector2(u, v));
            surface.AddVertex(normal * (BaseRadius + height));
        }

        public static Vector3 MapToSphereDirection(float mapX, float mapY, int width, int height)
        {
            float u = width <= 0 ? 0f : mapX / width;
            float v = height <= 0 ? 0f : mapY / height;
            return UvToSphereDirection(u, v);
        }

        public static Vector2I SphereDirectionToMap(Vector3 direction, MapData mapData)
        {
            direction = direction.Normalized();
            float u = 0.5f + Mathf.Atan2(direction.X, -direction.Z) / Mathf.Tau;
            u -= Mathf.Floor(u);
            float v = Mathf.Acos(Mathf.Clamp(direction.Y, -1f, 1f)) / Mathf.Pi;

            int mapX = WrapX(Mathf.FloorToInt(u * mapData.Width), mapData.Width);
            int mapY = Mathf.Clamp(Mathf.FloorToInt(v * mapData.Height), 0, mapData.Height - 1);
            return new Vector2I(mapX, mapY);
        }

        public static Vector3 UvToSphereDirection(float u, float v)
        {
            float theta = v * Mathf.Pi;
            float phi = (u - 0.5f) * Mathf.Tau;
            float sinTheta = Mathf.Sin(theta);
            return new Vector3(
                sinTheta * Mathf.Sin(phi),
                Mathf.Cos(theta),
                -sinTheta * Mathf.Cos(phi)
            ).Normalized();
        }

        public static float GetTerrainHeight(byte terrain)
        {
            return terrain switch
            {
                (byte)TerrainType.DeepOcean => -0.010f,
                (byte)TerrainType.ShallowWater => -0.006f,
                (byte)TerrainType.Beach => 0.001f,
                (byte)TerrainType.Grassland => 0.004f,
                (byte)TerrainType.Forest => 0.006f,
                (byte)TerrainType.Jungle => 0.006f,
                (byte)TerrainType.Savanna => 0.005f,
                (byte)TerrainType.Desert => 0.004f,
                (byte)TerrainType.Tundra => 0.007f,
                (byte)TerrainType.Snow => 0.009f,
                (byte)TerrainType.Mountain => 0.022f,
                (byte)TerrainType.HighPeak => 0.034f,
                _ => 0f
            };
        }

        public static float GetSmoothedTerrainHeight(MapData mapData, int mapX, int mapY)
        {
            float center = GetTerrainHeight(GetTerrainAt(mapData, mapX, mapY));
            float sum = center * 4.0f;
            float weight = 4.0f;

            int[] offsets = { -1, 0, 1 };
            foreach (int oy in offsets)
            {
                foreach (int ox in offsets)
                {
                    if (ox == 0 && oy == 0) continue;
                    int sampleY = WrapY(mapY + oy, mapData.Height);
                    sum += GetTerrainHeight(GetTerrainAt(mapData, mapX + ox, sampleY));
                    weight += 1.0f;
                }
            }

            return sum / weight;
        }

        public static bool IsOcean(byte terrain)
        {
            return terrain == (byte)TerrainType.DeepOcean || terrain == (byte)TerrainType.ShallowWater;
        }

        public static byte GetTerrainAt(MapData mapData, int mapX, int mapY)
        {
            if (mapData == null || mapData.Width <= 0 || mapData.Height <= 0) return 0;
            mapX = WrapX(mapX, mapData.Width);
            mapY = WrapY(mapY, mapData.Height);

            int cx = mapX / ChunkData.CHUNK_SIZE;
            int cy = mapY / ChunkData.CHUNK_SIZE;
            if (!mapData.Chunks.TryGetValue(new Vector2I(cx, cy), out ChunkData chunk) || chunk.TerrainMap == null)
                return 0;

            int localX = mapX % ChunkData.CHUNK_SIZE;
            int localY = mapY % ChunkData.CHUNK_SIZE;
            return chunk.TerrainMap[localY * ChunkData.CHUNK_SIZE + localX];
        }

        private static int WrapX(int x, int width)
        {
            if (width <= 0) return 0;
            int wrapped = x % width;
            return wrapped < 0 ? wrapped + width : wrapped;
        }

        private static int WrapY(int y, int height)
        {
            if (height <= 0) return 0;
            int wrapped = y % height;
            return wrapped < 0 ? wrapped + height : wrapped;
        }
    }
}
