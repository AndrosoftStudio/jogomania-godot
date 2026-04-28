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
        public bool FlipX = true;
        public bool FlipY = true;

        private const float MapAspectX = 1.0f; // equiretangular 2:1: tiles quadrados mantem a Tactical View coerente com o globo
        public const float MinZoomLevel = 2.35f;
        public const float MaxZoomLevel = 64f;

        public float TileWidth => ClampZoom(ZoomLevel) * MapAspectX;
        public float TileHeight => ClampZoom(ZoomLevel);

        public static float ClampZoom(float zoom)
        {
            return Mathf.Clamp(zoom, MinZoomLevel, MaxZoomLevel);
        }

        public Vector2 ClampCameraPosition(Vector2 position)
        {
            if (MapData == null || MapData.Height <= 0) return position;

            float tileH = TileHeight;
            float rawY = CenterY + (FlipY ? -position.Y : position.Y) / tileH;
            float clampedY = Mathf.Clamp(rawY, 0f, MapData.Height - 1f);
            position.Y = (FlipY ? -(clampedY - CenterY) : (clampedY - CenterY)) * tileH;
            return position;
        }

        public override void _Process(double delta)
        {
            TimeOffset += (float)delta;
            QueueRedraw();
        }

        public Vector2 GetMapCenterFromCamera(Camera2D camera)
        {
            return new Vector2(
                CenterX + (FlipX ? camera.Position.X : -camera.Position.X) / TileWidth,
                CenterY + (FlipY ? -camera.Position.Y : camera.Position.Y) / TileHeight
            );
        }

        public Vector3 GetSphereDirectionFromCamera(Camera2D camera)
        {
            Vector2 center = GetMapCenterFromCamera(camera);
            return RawMapToSphereDirection(center.X, center.Y);
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
            int tileStep = GetDrawTileStep();
            startTileX = Mathf.FloorToInt((float)startTileX / tileStep) * tileStep;
            startTileY = Mathf.FloorToInt((float)startTileY / tileStep) * tileStep;

            for (int tileY = startTileY; tileY <= endTileY; tileY += tileStep)
            {
                for (int tileX = startTileX; tileX <= endTileX; tileX += tileStep)
                {
                    int rawMapX = CenterX + (FlipX ? tileX : -tileX);
                    int rawMapY = CenterY + (FlipY ? -tileY : tileY);
                    NormalizePaperTile(rawMapX, rawMapY, out int mapX, out int mapY);
                    Color tileColor = GetColorAt(mapX, mapY, out bool isBorderLeft, out bool isBorderTop, out _);
                    tileColor = ApplyDayNight(tileColor, rawMapX + tileStep * 0.5f, rawMapY + tileStep * 0.5f);

                    float wx = tileX * tileW;
                    float wy = tileY * tileH;
                    float drawW = tileW * tileStep + 0.5f;
                    float drawH = tileH * tileStep + 0.5f;
                    DrawRect(new Rect2(wx, wy, drawW, drawH), tileColor);

                    if (ZoomLevel >= 2.0f)
                        DrawOceanMotion(mapX, mapY, wx, wy, drawW, drawH);

                    if (tileStep == 1 && ZoomLevel >= 14f)
                        DrawTerrainDetail(mapX, mapY, wx, wy, tileW, tileH);

                    float borderW = Mathf.Max(1.0f, ZoomLevel * 0.05f);
                    if (tileStep == 1 && isBorderLeft)
                        DrawLine(new Vector2(wx, wy), new Vector2(wx, wy + tileH), new Color(0.05f, 0.05f, 0.05f, 0.9f), borderW);
                    if (tileStep == 1 && isBorderTop)
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

        private int GetDrawTileStep()
        {
            if (ZoomLevel < 0.8f) return 8;
            if (ZoomLevel < 1.2f) return 6;
            if (ZoomLevel < 2.0f) return 4;
            if (ZoomLevel < 3.0f) return 2;
            return 1;
        }

        private void DrawTerrainDetail(int mapX, int mapY, float wx, float wy, float tileW, float tileH)
        {
            // Visual de mapa: substitui símbolos grandes por uma textura cartográfica sutil.
            byte terrain = GetTerrainAt(mapX, mapY);
            float n = Hash01(mapX, mapY) - 0.5f;

            if (terrain == 10 || terrain == 11)
            {
                Color ridge = terrain == 11 ? new Color(0.95f, 0.95f, 0.90f, 0.30f) : new Color(0.18f, 0.16f, 0.13f, 0.22f);
                DrawLine(new Vector2(wx + tileW * 0.18f, wy + tileH * (0.62f + n * 0.10f)),
                         new Vector2(wx + tileW * 0.82f, wy + tileH * (0.40f - n * 0.10f)),
                         ridge, Mathf.Max(0.7f, ZoomLevel * 0.035f));
            }
            else if (terrain == 4 || terrain == 5)
            {
                DrawCircle(new Vector2(wx + tileW * (0.35f + n * 0.20f), wy + tileH * 0.45f),
                           Mathf.Max(0.6f, ZoomLevel * 0.045f), new Color(0.02f, 0.12f, 0.02f, 0.18f));
            }
            else if (terrain == 7 || terrain == 6)
            {
                DrawLine(new Vector2(wx + tileW * 0.12f, wy + tileH * (0.52f + n * 0.16f)),
                         new Vector2(wx + tileW * 0.88f, wy + tileH * (0.48f - n * 0.16f)),
                         new Color(1.0f, 0.82f, 0.42f, 0.13f), Mathf.Max(0.5f, ZoomLevel * 0.025f));
            }
        }

        private static float Hash01(int x, int y)
        {
            float v = Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f;
            return v - Mathf.Floor(v);
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
                    GetNearestDisplayDelta(point.X, point.Y, out float dx, out float dy);
                    float screenDy = FlipY ? -dy : dy;
                    float screenDx = FlipX ? dx : -dx;
                    Vector2 p = new Vector2(screenDx * tileW + tileW * 0.5f, screenDy * tileH + tileH * 0.5f);
                    if (p.X < camPos.X - halfW - tileW * 4 || p.X > camPos.X + halfW + tileW * 4) continue;
                    if (p.Y < camPos.Y - halfH - tileH * 4 || p.Y > camPos.Y + halfH + tileH * 4) continue;
                    points.Add(p);
                }

                if (points.Count >= 2)
                {
                    Vector2[] smooth = SmoothRiverPoints(points);
                    float width = Mathf.Max(1.2f, ZoomLevel * river.Width * 0.16f);
                    DrawPolyline(smooth, new Color(0.02f, 0.10f, 0.18f, 0.28f), width + Mathf.Max(1.0f, ZoomLevel * 0.05f));
                    DrawPolyline(smooth, new Color(0.10f, 0.42f, 0.78f, 0.94f), width);
                    if (ZoomLevel >= 12f)
                        DrawPolyline(smooth, new Color(0.62f, 0.86f, 1.0f, 0.30f), Mathf.Max(0.6f, width * 0.35f));
                }
            }
        }

        private Vector2[] SmoothRiverPoints(System.Collections.Generic.List<Vector2> points)
        {
            if (points.Count < 3) return points.ToArray();
            var smooth = new System.Collections.Generic.List<Vector2>();
            smooth.Add(points[0]);
            for (int i = 1; i < points.Count - 1; i++)
            {
                Vector2 prev = points[i - 1];
                Vector2 current = points[i];
                Vector2 next = points[i + 1];
                smooth.Add(prev.Lerp(current, 0.72f));
                smooth.Add(current.Lerp(next, 0.28f));
            }
            smooth.Add(points[points.Count - 1]);
            return smooth.ToArray();
        }

        private void DrawVillages(Vector2 camPos, float halfW, float halfH, float tileW, float tileH)
        {
            if (MapData.Villages == null || ZoomLevel < 4f) return;

            foreach (var v in MapData.Villages)
            {
                GetNearestDisplayDelta(v.X, v.Y, out float dx, out float dy);

                float wx = (FlipX ? dx : -dx) * tileW;
                float wy = (FlipY ? -dy : dy) * tileH;
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

        private Color ApplyDayNight(Color color, float rawMapX, float rawMapY)
        {
            if (!UseDayNight) return color;
            Vector3 normal = RawMapToSphereDirection(rawMapX, rawMapY);
            float sunDot = normal.Dot(SunDirection.Normalized());
            float day = Mathf.SmoothStep(0.02f, 0.38f, sunDot);
            float nightGradient = Mathf.Pow(Mathf.Clamp((sunDot + 1.0f) / 1.12f, 0f, 1f), 1.22f);
            float nightLight = Mathf.Lerp(0.020f, 0.50f, nightGradient);
            float light = Mathf.Lerp(nightLight, 1.0f, day);
            return new Color(color.R * light, color.G * light, color.B * light, color.A);
        }

        private byte GetTerrainAt(int mapX, int mapY)
        {
            return PlanetMeshBuilder.GetTerrainAt(MapData, mapX, mapY);
        }

        private void NormalizePaperTile(int rawX, int rawY, out int mapX, out int mapY)
        {
            mapX = WrapX(rawX);
            mapY = Mathf.Clamp(rawY, 0, MapData.Height - 1);
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
                return c.Lerp(new Color(0.95f, 0.82f, 0.32f, 1.0f), 0.18f);
            }

            return c;
        }

        private byte GetOwnerAt(int mapX, int mapY)
        {
            if (MapData == null || MapData.Height <= 0) return 0;
            NormalizePaperTile(mapX, mapY, out int normX, out int normY);
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

        private Vector3 RawMapToSphereDirection(float rawX, float rawY)
        {
            if (MapData == null || MapData.Width <= 0 || MapData.Height <= 0) return Vector3.Forward;

            float x = rawX - Mathf.Floor(rawX / MapData.Width) * MapData.Width;
            float y = Mathf.Clamp(rawY, 0f, MapData.Height - 1f);
            return PlanetMeshBuilder.MapToSphereDirection(x, y, MapData.Width, MapData.Height);
        }

        private void GetNearestDisplayDelta(int mapX, int mapY, out float bestDx, out float bestDy)
        {
            bestDx = mapX - CenterX;
            bestDy = mapY - CenterY;
            float bestDist = bestDx * bestDx + bestDy * bestDy;
            for (int kx = -1; kx <= 1; kx++)
                TestDisplayCandidate(mapX + kx * MapData.Width, mapY, ref bestDx, ref bestDy, ref bestDist);
        }

        private void TestDisplayCandidate(float rawX, float rawY, ref float bestDx, ref float bestDy, ref float bestDist)
        {
            float dx = rawX - CenterX;
            float dy = rawY - CenterY;
            float dist = dx * dx + dy * dy;
            if (dist >= bestDist) return;
            bestDist = dist;
            bestDx = dx;
            bestDy = dy;
        }
    }
}
