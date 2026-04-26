using Godot;
using Jogomania.Map;

namespace Jogomania.Core
{
    public partial class TacticalView : Node2D
    {
        public Data.MapData MapData;
        public int CenterX = 0;
        public int CenterY = 0;
        public float ZoomLevel = 32f;
        public bool ShowVillageNames = false;
        public bool UseDayNight = true;
        public Vector3 SunDirection = Vector3.Right;
        public float TimeOffset = 0f;

        private const float MapAspectX = 1.65f;

        public float TileWidth => Mathf.Max(1f, ZoomLevel) * MapAspectX;
        public float TileHeight => Mathf.Max(1f, ZoomLevel);

        public override void _Process(double delta)
        {
            TimeOffset += (float)delta;
            QueueRedraw();
        }

        public Vector2 GetMapCenterFromCamera(Camera2D camera)
        {
            return new Vector2(
                CenterX + camera.Position.X / TileWidth,
                CenterY + camera.Position.Y / TileHeight
            );
        }

        public override void _Draw()
        {
            if (MapData == null || MapData.Width <= 0 || MapData.Height <= 0) return;

            Camera2D cam = GetViewport().GetCamera2D();
            if (cam == null) return;

            float tileW = TileWidth;
            float tileH = TileHeight;
            Vector2 viewportSize = GetViewportRect().Size;
            float cameraZoom = Mathf.Max(0.001f, cam.Zoom.X);
            float halfW = (viewportSize.X / cameraZoom) * 0.5f;
            float halfH = (viewportSize.Y / cameraZoom) * 0.5f;
            Vector2 camPos = cam.GlobalPosition;

            int startTileX = Mathf.FloorToInt((camPos.X - halfW) / tileW) - 1;
            int startTileY = Mathf.FloorToInt((camPos.Y - halfH) / tileH) - 1;
            int endTileX = Mathf.CeilToInt((camPos.X + halfW) / tileW) + 1;
            int endTileY = Mathf.CeilToInt((camPos.Y + halfH) / tileH) + 1;

            for (int tileY = startTileY; tileY <= endTileY; tileY++)
            {
                for (int tileX = startTileX; tileX <= endTileX; tileX++)
                {
                    NormalizeSphereTile(CenterX + tileX, CenterY + tileY, out int mapX, out int mapY);
                    Color tileColor = GetColorAt(mapX, mapY, out bool isBorderLeft, out bool isBorderTop, out _);
                    tileColor = ApplyDayNight(tileColor, mapX, mapY);

                    float wx = tileX * tileW;
                    float wy = tileY * tileH;
                    DrawRect(new Rect2(wx, wy, tileW + 0.5f, tileH + 0.5f), tileColor);

                    DrawOceanMotion(mapX, mapY, wx, wy, tileW, tileH);

                    if (ZoomLevel >= 14f)
                        DrawTerrainDetail(mapX, mapY, wx, wy, tileW, tileH);

                    float borderW = Mathf.Max(1.0f, ZoomLevel * 0.05f);
                    if (isBorderLeft)
                        DrawLine(new Vector2(wx, wy), new Vector2(wx, wy + tileH), new Color(0.05f, 0.05f, 0.05f, 0.9f), borderW);
                    if (isBorderTop)
                        DrawLine(new Vector2(wx, wy), new Vector2(wx + tileW, wy), new Color(0.05f, 0.05f, 0.05f, 0.9f), borderW);
                }
            }

            DrawRivers(camPos, halfW, halfH, tileW, tileH);
            DrawVillages(camPos, halfW, halfH, tileW, tileH);
        }

        private void DrawOceanMotion(int mapX, int mapY, float wx, float wy, float tileW, float tileH)
        {
            byte terrain = GetTerrainAt(mapX, mapY);
            if (!PlanetMeshBuilder.IsOcean(terrain)) return;

            float pulse = Mathf.Sin((mapX * 0.09f + mapY * 0.04f) + TimeOffset * 1.25f) * 0.5f + 0.5f;
            DrawRect(new Rect2(wx, wy, tileW + 0.5f, tileH + 0.5f), new Color(0.06f, 0.32f, 0.68f, 0.05f + pulse * 0.035f));

            if (ZoomLevel < 10f) return;
            float y = wy + tileH * (0.35f + 0.28f * pulse);
            DrawLine(new Vector2(wx + tileW * 0.18f, y), new Vector2(wx + tileW * 0.82f, y + tileH * 0.08f), new Color(0.55f, 0.82f, 1.0f, 0.18f), Mathf.Max(1f, ZoomLevel * 0.035f));
        }

        private void DrawTerrainDetail(int mapX, int mapY, float wx, float wy, float tileW, float tileH)
        {
            byte terrain = GetTerrainAt(mapX, mapY);
            if (terrain == 10 || terrain == 11)
            {
                Color ridge = terrain == 11 ? new Color(0.82f, 0.82f, 0.80f, 0.85f) : new Color(0.36f, 0.36f, 0.34f, 0.85f);
                float seed = Mathf.Sin(mapX * 12.9898f + mapY * 78.233f);
                Vector2 a = new Vector2(wx + tileW * 0.20f, wy + tileH * (0.65f + seed * 0.06f));
                Vector2 b = new Vector2(wx + tileW * 0.50f, wy + tileH * (0.18f - seed * 0.04f));
                Vector2 c = new Vector2(wx + tileW * 0.84f, wy + tileH * (0.70f - seed * 0.05f));
                DrawPolyline(new Vector2[] { a, b, c }, ridge, Mathf.Max(1.2f, ZoomLevel * 0.12f));
                DrawLine(b, new Vector2(wx + tileW * 0.45f, wy + tileH * 0.85f), new Color(0.14f, 0.14f, 0.14f, 0.35f), Mathf.Max(1f, ZoomLevel * 0.06f));
            }
            else if (terrain == 4 || terrain == 5)
            {
                Color canopy = terrain == 5 ? new Color(0.0f, 0.26f, 0.02f, 0.75f) : new Color(0.04f, 0.34f, 0.06f, 0.72f);
                for (int i = 0; i < 3; i++)
                {
                    float ox = Mathf.Abs(Mathf.Sin(mapX * (i + 1) * 5.13f + mapY)) % 1f;
                    float oy = Mathf.Abs(Mathf.Sin(mapY * (i + 2) * 3.71f + mapX)) % 1f;
                    DrawCircle(new Vector2(wx + tileW * (0.25f + ox * 0.5f), wy + tileH * (0.25f + oy * 0.5f)), Mathf.Max(1.2f, ZoomLevel * 0.16f), canopy);
                }
            }
            else if (terrain == 7)
            {
                DrawLine(new Vector2(wx + tileW * 0.15f, wy + tileH * 0.65f), new Vector2(wx + tileW * 0.85f, wy + tileH * 0.48f), new Color(0.98f, 0.78f, 0.28f, 0.28f), Mathf.Max(1f, ZoomLevel * 0.045f));
            }
            else if (terrain == 3 || terrain == 6 || terrain == 8)
            {
                DrawLine(new Vector2(wx + tileW * 0.18f, wy + tileH * 0.58f), new Vector2(wx + tileW * 0.82f, wy + tileH * 0.50f), new Color(0.08f, 0.12f, 0.06f, 0.18f), Mathf.Max(1f, ZoomLevel * 0.035f));
            }
        }

        private void DrawRivers(Vector2 camPos, float halfW, float halfH, float tileW, float tileH)
        {
            if (MapData.Rivers == null || ZoomLevel < 5f) return;

            foreach (var river in MapData.Rivers)
            {
                if (river.Points == null || river.Points.Count < 2) continue;
                var points = new System.Collections.Generic.List<Vector2>();
                foreach (var point in river.Points)
                {
                    int dx = point.X - CenterX;
                    if (dx > MapData.Width / 2) dx -= MapData.Width;
                    if (dx < -MapData.Width / 2) dx += MapData.Width;
                    int dy = point.Y - CenterY;
                    if (dy > MapData.Height / 2) dy -= MapData.Height;
                    if (dy < -MapData.Height / 2) dy += MapData.Height;
                    Vector2 p = new Vector2(dx * tileW + tileW * 0.5f, dy * tileH + tileH * 0.5f);
                    if (p.X < camPos.X - halfW - tileW * 4 || p.X > camPos.X + halfW + tileW * 4) continue;
                    if (p.Y < camPos.Y - halfH - tileH * 4 || p.Y > camPos.Y + halfH + tileH * 4) continue;
                    points.Add(p);
                }

                if (points.Count >= 2)
                    DrawPolyline(points.ToArray(), new Color(0.12f, 0.52f, 0.95f, 0.92f), Mathf.Max(1.2f, ZoomLevel * river.Width * 0.18f));
            }
        }

        private void DrawVillages(Vector2 camPos, float halfW, float halfH, float tileW, float tileH)
        {
            if (MapData.Villages == null || ZoomLevel < 4f) return;

            foreach (var v in MapData.Villages)
            {
                int dx = v.X - CenterX;
                if (dx > MapData.Width / 2) dx -= MapData.Width;
                if (dx < -MapData.Width / 2) dx += MapData.Width;
                int dy = v.Y - CenterY;
                if (dy > MapData.Height / 2) dy -= MapData.Height;
                if (dy < -MapData.Height / 2) dy += MapData.Height;

                float wx = dx * tileW;
                float wy = dy * tileH;
                if (wx < camPos.X - halfW - tileW * 3 || wx > camPos.X + halfW + tileW * 3) continue;
                if (wy < camPos.Y - halfH - tileH * 3 || wy > camPos.Y + halfH + tileH * 3) continue;

                float iconSize = Mathf.Clamp(ZoomLevel * 0.6f, 2f, 12f);
                DrawCircle(new Vector2(wx + tileW * 0.5f, wy + tileH * 0.5f), iconSize * 0.45f, new Color(1.0f, 0.86f, 0.02f, 0.95f));

                if (ShowVillageNames && ZoomLevel >= 16f)
                {
                    DrawString(ThemeDB.FallbackFont, new Vector2(wx + tileW * 0.5f, wy - 4f), v.Name,
                        HorizontalAlignment.Center, -1, Mathf.Max(10, Mathf.RoundToInt(ZoomLevel / 3f)),
                        new Color(1f, 1f, 1f, 0.95f));
                }
            }
        }

        private Color ApplyDayNight(Color color, int mapX, int mapY)
        {
            if (!UseDayNight) return color;
            Vector3 normal = PlanetMeshBuilder.MapToSphereDirection(mapX + 0.5f, mapY + 0.5f, MapData.Width, MapData.Height);
            float day = Mathf.SmoothStep(-0.10f, 0.22f, normal.Dot(SunDirection.Normalized()));
            float light = Mathf.Lerp(0.08f, 1.0f, day);
            return new Color(color.R * light, color.G * light, color.B * light, color.A);
        }

        private byte GetTerrainAt(int mapX, int mapY)
        {
            return PlanetMeshBuilder.GetTerrainAt(MapData, mapX, mapY);
        }

        private void NormalizeSphereTile(int rawX, int rawY, out int mapX, out int mapY)
        {
            int period = MapData.Height * 2;
            int y = rawY % period;
            if (y < 0) y += period;

            int xOffset = 0;
            if (y >= MapData.Height)
            {
                y = period - y - 1;
                xOffset = MapData.Width / 2;
            }

            mapY = Mathf.Clamp(y, 0, MapData.Height - 1);
            mapX = WrapX(rawX + xOffset);
        }

        private Color GetColorAt(int mapX, int mapY, out bool isBorderLeft, out bool isBorderTop, out byte ownerId)
        {
            isBorderLeft = false;
            isBorderTop = false;
            ownerId = 0;

            int cx = mapX / Data.ChunkData.CHUNK_SIZE;
            int cy = mapY / Data.ChunkData.CHUNK_SIZE;
            if (!MapData.Chunks.TryGetValue(new Vector2I(cx, cy), out var chunk))
                return new Color(0.04f, 0.04f, 0.08f);

            int flatIndex = (mapY % Data.ChunkData.CHUNK_SIZE) * Data.ChunkData.CHUNK_SIZE + (mapX % Data.ChunkData.CHUNK_SIZE);
            byte terrainType = chunk.TerrainMap[flatIndex];
            ownerId = chunk.TerritoryMap[flatIndex];
            Color c = Game.GetTerrainColor(terrainType);

            if (ownerId > 0 && ownerId != 255)
            {
                byte ownerLeft = GetOwnerAt(mapX - 1, mapY);
                if (ownerLeft != ownerId && ownerLeft != 255) isBorderLeft = true;

                byte ownerTop = GetOwnerAt(mapX, mapY - 1);
                if (ownerTop != ownerId && ownerTop != 255) isBorderTop = true;

                if (ownerId == 1)
                    return c.Lerp(Game.GetTerritoryColor(ownerId), 0.35f);
            }
            else if (ownerId == 255)
            {
                return new Color(1.0f, 0.9f, 0.05f, 1.0f);
            }

            return c;
        }

        private byte GetOwnerAt(int mapX, int mapY)
        {
            if (MapData == null || MapData.Height <= 0) return 0;
            int normX = WrapX(mapX);
            int normY = WrapY(mapY);
            int cx = normX / Data.ChunkData.CHUNK_SIZE;
            int cy = normY / Data.ChunkData.CHUNK_SIZE;
            if (MapData.Chunks.TryGetValue(new Vector2I(cx, cy), out var chunk))
                return chunk.TerritoryMap[(normY % Data.ChunkData.CHUNK_SIZE) * Data.ChunkData.CHUNK_SIZE + (normX % Data.ChunkData.CHUNK_SIZE)];
            return 255;
        }

        private int WrapX(int x)
        {
            int wrapped = x % MapData.Width;
            return wrapped < 0 ? wrapped + MapData.Width : wrapped;
        }

        private int WrapY(int y)
        {
            int wrapped = y % MapData.Height;
            return wrapped < 0 ? wrapped + MapData.Height : wrapped;
        }
    }
}
