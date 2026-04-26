using Godot;
using System;

namespace Jogomania.Core
{
    /// <summary>
    /// Visão tática 2D do mapa — renderiza tiles usando world-space da Camera2D.
    /// Suporta pan infinito (wrapping) no eixo X (longitude).
    /// </summary>
    public partial class TacticalView : Node2D
    {
        public Data.MapData MapData;

        /// <summary>Coordenada do mapa que corresponde ao ponto de mundo (0, 0).</summary>
        public int CenterX = 0;
        public int CenterY = 0;

        /// <summary>Quantos pixels cada tile ocupa no world-space.</summary>
        public int ZoomLevel = 32;

        /// <summary>Se verdadeiro, desenha os nomes das aldeias acima dos tiles.</summary>
        public bool ShowVillageNames = false;

        public override void _Process(double delta)
        {
            QueueRedraw();
        }

        public override void _Draw()
        {
            if (MapData == null) return;

            Camera2D cam = GetViewport().GetCamera2D();
            if (cam == null) return;

            Vector2 viewportSize = GetViewportRect().Size;
            float zoom = cam.Zoom.X;

            // Espaço de mundo visível pela câmera (em pixels de mundo)
            float halfW = (viewportSize.X / zoom) * 0.5f;
            float halfH = (viewportSize.Y / zoom) * 0.5f;

            Vector2 camPos = cam.GlobalPosition;

            // Índices de tile que cobrem a tela
            int startTileX = Mathf.FloorToInt((camPos.X - halfW) / ZoomLevel);
            int startTileY = Mathf.FloorToInt((camPos.Y - halfH) / ZoomLevel);
            int endTileX   = Mathf.CeilToInt ((camPos.X + halfW) / ZoomLevel) + 1;
            int endTileY   = Mathf.CeilToInt ((camPos.Y + halfH) / ZoomLevel) + 1;

            for (int tileY = startTileY; tileY <= endTileY; tileY++)
            {
                // Cálculo de índice de mapa para o eixo Y — sem wrapping (polos são limites)
                int mapY = CenterY + tileY;
                if (mapY < 0 || mapY >= MapData.Height) continue;

                for (int tileX = startTileX; tileX <= endTileX; tileX++)
                {
                    // Cálculo de índice de mapa para X — COM wrapping (longitude é circular)
                    int rawMapX = CenterX + tileX;
                    int mapX = ((rawMapX % MapData.Width) + MapData.Width) % MapData.Width;

                    Color tileColor = GetColorAt(mapX, mapY, out bool isBorderLeft, out bool isBorderTop, out byte ownerId);

                    // Posição no world-space onde este tile é desenhado
                    float wx = tileX * ZoomLevel;
                    float wy = tileY * ZoomLevel;

                    DrawRect(new Rect2(wx, wy, ZoomLevel, ZoomLevel), tileColor);

                    // Detalhes de terreno (escala-dependente: só aparece em zoom alto)
                    if (ZoomLevel >= 16)
                    {
                        byte terrain = GetTerrainAt(mapX, mapY);
                        if (terrain == 10) // Montanha — triângulo simples
                        {
                            DrawColoredPolygon(new Vector2[] {
                                new Vector2(wx + ZoomLevel * 0.5f, wy + ZoomLevel * 0.1f),
                                new Vector2(wx + ZoomLevel * 0.1f, wy + ZoomLevel * 0.9f),
                                new Vector2(wx + ZoomLevel * 0.9f, wy + ZoomLevel * 0.9f),
                            }, new Color(0.55f, 0.55f, 0.55f));
                        }
                        else if (terrain == 4 || terrain == 5) // Floresta / Jungle
                        {
                            float r = ZoomLevel * 0.3f;
                            float cx2 = wx + ZoomLevel * 0.5f;
                            float cy2 = wy + ZoomLevel * 0.5f;
                            DrawCircle(new Vector2(cx2, cy2), r, terrain == 5 ? new Color(0.0f, 0.35f, 0.0f) : new Color(0.1f, 0.5f, 0.1f));
                        }
                        else if (terrain == 7) // Deserto — pontinhos de areia
                        {
                            DrawCircle(new Vector2(wx + ZoomLevel * 0.3f, wy + ZoomLevel * 0.6f), ZoomLevel * 0.08f, new Color(0.9f, 0.8f, 0.4f));
                            DrawCircle(new Vector2(wx + ZoomLevel * 0.7f, wy + ZoomLevel * 0.4f), ZoomLevel * 0.08f, new Color(0.9f, 0.8f, 0.4f));
                        }
                        else if (terrain == 11) // Neve
                        {
                            DrawCircle(new Vector2(wx + ZoomLevel * 0.5f, wy + ZoomLevel * 0.3f), ZoomLevel * 0.12f, Colors.White);
                        }
                    }

                    // Bordas de fronteira — linhas finas nas arestas
                    float borderW = Mathf.Max(1.5f, ZoomLevel * 0.05f);
                    if (isBorderLeft)
                        DrawLine(new Vector2(wx, wy), new Vector2(wx, wy + ZoomLevel), new Color(0.15f, 0.15f, 0.15f, 0.9f), borderW);
                    if (isBorderTop)
                        DrawLine(new Vector2(wx, wy), new Vector2(wx + ZoomLevel, wy), new Color(0.15f, 0.15f, 0.15f, 0.9f), borderW);
                }
            }

            // Nomes das aldeias — só se ShowVillageNames = true e zoom suficiente
            if (ShowVillageNames && MapData.Villages != null && ZoomLevel >= 8)
            {
                foreach (var v in MapData.Villages)
                {
                    // Converter posição de mapa para world-space
                    int tileX = v.X - CenterX;
                    int tileY = v.Y - CenterY;

                    float wx = tileX * ZoomLevel;
                    float wy = tileY * ZoomLevel;

                    // Só desenha se está na área visível (margem de 2 tiles)
                    if (wx < camPos.X - halfW - ZoomLevel * 2 || wx > camPos.X + halfW + ZoomLevel * 2) continue;
                    if (wy < camPos.Y - halfH - ZoomLevel * 2 || wy > camPos.Y + halfH + ZoomLevel * 2) continue;

                    // Ícone da aldeia
                    float iconSize = ZoomLevel * 0.6f;
                    DrawRect(new Rect2(wx + (ZoomLevel - iconSize) * 0.5f, wy + (ZoomLevel - iconSize) * 0.5f, iconSize, iconSize),
                        new Color(0.9f, 0.8f, 0.2f, 0.9f));

                    // Nome da aldeia (texto simples)
                    if (ZoomLevel >= 24)
                    {
                        DrawString(ThemeDB.FallbackFont, new Vector2(wx + ZoomLevel * 0.5f, wy - 4f), v.Name,
                            HorizontalAlignment.Center, -1, Mathf.Max(10, ZoomLevel / 3),
                            new Color(1f, 1f, 1f, 0.95f));
                    }
                }
            }
        }

        // ── Helpers ────────────────────────────────────────────────────────────────

        private byte GetTerrainAt(int mapX, int mapY)
        {
            int cx = mapX / Data.ChunkData.CHUNK_SIZE;
            int cy = mapY / Data.ChunkData.CHUNK_SIZE;
            if (MapData.Chunks.TryGetValue(new Vector2I(cx, cy), out var chunk))
                return chunk.TerrainMap[(mapY % Data.ChunkData.CHUNK_SIZE) * Data.ChunkData.CHUNK_SIZE + (mapX % Data.ChunkData.CHUNK_SIZE)];
            return 0;
        }

        private Color GetColorAt(int mapX, int mapY, out bool isBorderLeft, out bool isBorderTop, out byte ownerId)
        {
            isBorderLeft = false;
            isBorderTop  = false;
            ownerId      = 0;

            int cx = mapX / Data.ChunkData.CHUNK_SIZE;
            int cy = mapY / Data.ChunkData.CHUNK_SIZE;
            if (!MapData.Chunks.TryGetValue(new Vector2I(cx, cy), out var chunk))
                return new Color(0.05f, 0.05f, 0.1f);

            int flatIndex = (mapY % Data.ChunkData.CHUNK_SIZE) * Data.ChunkData.CHUNK_SIZE + (mapX % Data.ChunkData.CHUNK_SIZE);
            byte terrainType = chunk.TerrainMap[flatIndex];
            ownerId = chunk.TerritoryMap[flatIndex];

            Color c = Game.GetTerrainColor(terrainType);

            if (ownerId > 0 && ownerId != 255)
            {
                // Borda esquerda
                int nxLeft = (mapX - 1 + MapData.Width) % MapData.Width;
                byte ownerLeft = GetOwnerAt(nxLeft, mapY);
                if (ownerLeft != ownerId && ownerLeft != 255) isBorderLeft = true;

                // Borda superior
                if (mapY > 0)
                {
                    byte ownerTop = GetOwnerAt(mapX, mapY - 1);
                    if (ownerTop != ownerId && ownerTop != 255) isBorderTop = true;
                }

                // Colorir território do jogador
                if (ownerId == 1)
                {
                    Color terrColor = Game.GetTerritoryColor(ownerId);
                    return c.Lerp(terrColor, 0.35f);
                }
            }

            return c;
        }

        private byte GetOwnerAt(int mapX, int mapY)
        {
            if (mapY < 0 || mapY >= MapData.Height) return 0;
            int normX = ((mapX % MapData.Width) + MapData.Width) % MapData.Width;
            int cx = normX / Data.ChunkData.CHUNK_SIZE;
            int cy = mapY  / Data.ChunkData.CHUNK_SIZE;
            if (MapData.Chunks.TryGetValue(new Vector2I(cx, cy), out var chunk))
                return chunk.TerritoryMap[(mapY % Data.ChunkData.CHUNK_SIZE) * Data.ChunkData.CHUNK_SIZE + (normX % Data.ChunkData.CHUNK_SIZE)];
            return 255;
        }
    }
}
