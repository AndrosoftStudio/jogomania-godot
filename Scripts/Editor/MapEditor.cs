using Godot;
using System;
using System.Text.Json;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using Jogomania.Core;
using Jogomania.Data;
using Jogomania.Map;

namespace Jogomania.Editor
{
    public partial class MapEditor : Node2D
    {
        private TextureRect _previewTexture;
        private Node2D _worldContainer;
        private Image _loadedImage;
        private LineEdit _inputWidth;
        private LineEdit _inputHeight;
        private OptionButton _optionLandColor;
        private CheckBox _checkHeightmap;
        private LineEdit _inputSeed;
        private OptionButton _optionBrush;
        private Label _saveFeedback;
        private LineEdit _inputMapName;
        private OptionButton _optionSavedMaps;
        private ConfirmationDialog _overwriteDialog;
        private string _pendingSavePath;
        
        private Node3D _globeContainer;
        private MeshInstance3D _planetMesh;

        private Control _loadingPanel;
        private ProgressBar _progressBar;
        private Label _labelStatus;

        private SpinBox _villageCountInput;
        private CheckBox _toggleBordersCheckbox;

        private Camera3D _camera3D;
        private Camera2D _camera2D;
        private TacticalView _tacticalView;
        private bool _isTacticalMode = false;
        private bool _isDraggingGlobe = false;
        private Data.MapData _currentMapData;
        private Dictionary<Vector2I, ChunkVisualizer> _activeVisualizers = new Dictionary<Vector2I, ChunkVisualizer>();

        private bool _isPainting = false;
        private float _generationProgress = 0f;

        // â”€â”€ Pinch-to-Zoom (dois dedos) â”€â”€
        private bool _finger0Down = false;
        private bool _finger1Down = false;
        private Vector2 _finger0Pos = Vector2.Zero;
        private Vector2 _finger1Pos = Vector2.Zero;
        private float _pinchPrevDistance = 0f;

        public override void _Ready()
        {
            _previewTexture = GetNode<TextureRect>("UILayer/UIControl/VBoxContainer/PreviewTexture");
            _worldContainer = GetNode<Node2D>("WorldContainer");
            
            _inputWidth = GetNode<LineEdit>("UILayer/UIControl/VBoxContainer/HBoxContainer/InputWidth");
            _inputHeight = GetNode<LineEdit>("UILayer/UIControl/VBoxContainer/HBoxContainer/InputHeight");
            _optionLandColor = GetNode<OptionButton>("UILayer/UIControl/VBoxContainer/HBoxLandColor/OptionLandColor");
            _checkHeightmap = GetNode<CheckBox>("UILayer/UIControl/VBoxContainer/CheckHeightmap");
            _inputSeed = GetNode<LineEdit>("UILayer/UIControl/VBoxContainer/HBoxSeed/InputSeed");
            _optionBrush = GetNode<OptionButton>("UILayer/UIControl/VBoxContainer/OptionBrush");
            _saveFeedback = GetNode<Label>("UILayer/UIControl/VBoxContainer/SaveFeedback");
            _camera2D = GetNode<Camera2D>("Camera2D");

            _globeContainer = GetNode<Node3D>("GlobeContainer");
            _globeContainer.Position = Vector3.Zero;

            _planetMesh = GetNode<MeshInstance3D>("GlobeContainer/PlanetMesh");
            _camera3D = GetNode<Camera3D>("GlobeContainer/Camera3D");
            _camera3D.Current = true;
            _camera3D.HOffset = 0.0f;

            _tacticalView = new TacticalView();
            _tacticalView.Visible = false;
            AddChild(_tacticalView);

            // EspaÃ§amento da barra de rolagem â€” usa offset do ScrollContainer
            var scroll = GetNode<ScrollContainer>("UILayer/UIControl");
            scroll.OffsetRight = 330f; // Era 320, agora 330 para dar 10px de respiro

            // Criar UI de Aldeias Programaticamente
            var vbox = GetNode<VBoxContainer>("UILayer/UIControl/VBoxContainer");
            CreateMapFileControls(vbox);

            var labelVillages = new Label();
            labelVillages.Text = "Quantidade de Aldeias Iniciais:";
            vbox.AddChild(labelVillages);
            vbox.MoveChild(labelVillages, 6); // Acima do gerador procedural

            _villageCountInput = new SpinBox();
            _villageCountInput.MinValue = 0;
            _villageCountInput.MaxValue = 500000;
            _villageCountInput.Value = 50;
            vbox.AddChild(_villageCountInput);
            vbox.MoveChild(_villageCountInput, 7);

            _toggleBordersCheckbox = new CheckBox();
            _toggleBordersCheckbox.Text = "Ver Fronteiras das Aldeias (Modo PolÃ­tico)";
            _toggleBordersCheckbox.ButtonPressed = true;
            _toggleBordersCheckbox.Toggled += (bool toggledOn) => { RenderCurrentMapData(); };
            vbox.AddChild(_toggleBordersCheckbox);
            vbox.MoveChild(_toggleBordersCheckbox, 8);

            // BotÃ£o mobile para ver nomes de aldeias
            var btnNames = new Button();
            btnNames.Text = "ðŸ˜ Nomes";
            btnNames.ToggleMode = true;
            btnNames.Toggled += (on) => { _tacticalView.ShowVillageNames = on; };
            vbox.AddChild(btnNames);
            vbox.MoveChild(btnNames, 9);

            // BotÃ£o: Carregar Partida do Jogo (save_atual.json do Slot 1)
            var btnLoadSave = new Button();
            btnLoadSave.Text = "ðŸ“‚ Carregar Partida Salva";
            btnLoadSave.Pressed += OnBtnLoadGameSavePressed;
            vbox.AddChild(btnLoadSave);
            vbox.MoveChild(btnLoadSave, 10);

            _loadingPanel = GetNode<Control>("UILayer/LoadingPanel");
            _progressBar = GetNode<ProgressBar>("UILayer/LoadingPanel/VBoxContainer/ProgressBar");
            _labelStatus = GetNode<Label>("UILayer/LoadingPanel/VBoxContainer/LabelStatus");

            // SaveFeedback: limita a largura para nÃ£o expandir o painel
            _saveFeedback.ClipText = true;
            _saveFeedback.CustomMinimumSize = new Vector2(0, 0);
            _saveFeedback.SizeFlagsHorizontal = Control.SizeFlags.Fill;

            _overwriteDialog = new ConfirmationDialog();
            _overwriteDialog.Title = "Mapa ja existe";
            _overwriteDialog.OkButtonText = "Sobrescrever";
            _overwriteDialog.GetCancelButton().Text = "Criar copia";
            _overwriteDialog.Confirmed += OnOverwriteConfirmed;
            _overwriteDialog.Canceled += OnOverwriteDeclined;
            AddChild(_overwriteDialog);

            RefreshSavedMapList();
        }

        private void CreateMapFileControls(VBoxContainer vbox)
        {
            var nameLabel = new Label();
            nameLabel.Text = "Nome do arquivo do mapa:";
            vbox.AddChild(nameLabel);

            _inputMapName = new LineEdit();
            _inputMapName.PlaceholderText = "meu_mapa";
            _inputMapName.Text = "mapa_padrao";
            vbox.AddChild(_inputMapName);

            var loadLabel = new Label();
            loadLabel.Text = "Mapas salvos:";
            vbox.AddChild(loadLabel);

            var loadRow = new HBoxContainer();
            _optionSavedMaps = new OptionButton();
            _optionSavedMaps.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            loadRow.AddChild(_optionSavedMaps);

            var btnLoadSelected = new Button();
            btnLoadSelected.Text = "Carregar Selecionado";
            btnLoadSelected.Pressed += OnBtnLoadSelectedMapPressed;
            loadRow.AddChild(btnLoadSelected);
            vbox.AddChild(loadRow);
        }

        public override void _Process(double delta)
        {
            if (_loadingPanel.Visible)
            {
                _progressBar.Value = _generationProgress;
            }
        }

        private void OnBtnLoadImagePressed()
        {
            DisplayServer.FileDialogShow("Carregar Imagem", OS.GetSystemDir(OS.SystemDir.Downloads), "", false, DisplayServer.FileDialogMode.OpenFile, new string[] { "*.png, *.jpg, *.jpeg ; Imagens" }, Callable.From<bool, string[], int>(OnNativeFileSelected));
        }

        private void OnNativeFileSelected(bool status, string[] selectedPaths, int selectedFilterIndex)
        {
            if (status && selectedPaths.Length > 0)
            {
                string path = selectedPaths[0];
                _loadedImage = Image.LoadFromFile(path);
                
                if (_loadedImage != null)
                {
                    var texture = ImageTexture.CreateFromImage(_loadedImage);
                    _previewTexture.Texture = texture;
                    _previewTexture.Visible = true;
                }
            }
        }

        private void OnBtnTogglePreviewPressed()
        {
            _previewTexture.Visible = !_previewTexture.Visible;
        }

        private void ClearWorld()
        {
            foreach(Node child in _worldContainer.GetChildren())
            {
                child.QueueFree();
            }
            _activeVisualizers.Clear();
            _currentMapData = new MapData();
            _saveFeedback.Visible = false;
            _globeContainer.Visible = false;
            _worldContainer.Visible = true;
        }

        private async void OnBtnGenerateImagePressed()
        {
            if (_loadedImage == null) return;
            ClearWorld();

            int targetWidth = 1000;
            int targetHeight = 1000;
            int.TryParse(_inputWidth.Text, out targetWidth);
            int.TryParse(_inputHeight.Text, out targetHeight);

            targetWidth = Mathf.CeilToInt((float)targetWidth / ChunkData.CHUNK_SIZE) * ChunkData.CHUNK_SIZE;
            targetHeight = Mathf.CeilToInt((float)targetHeight / ChunkData.CHUNK_SIZE) * ChunkData.CHUNK_SIZE;

            _loadedImage.Resize(targetWidth, targetHeight, Image.Interpolation.Bilinear);

            int width = targetWidth;
            int height = targetHeight;
            bool blackIsLand = _optionLandColor.Selected == 1;
            bool useHeightmap = _checkHeightmap.ButtonPressed;
            
            byte[] imgData = _loadedImage.GetData();
            Image.Format imgFormat = _loadedImage.GetFormat();
            bool hasMipmaps = _loadedImage.HasMipmaps();

            _loadingPanel.Visible = true;
            _generationProgress = 0f;
            _labelStatus.Text = "Mixando Biomas com Imagem...";

            _currentMapData = await Task.Run(() => GenerateImageMapData(width, height, imgData, imgFormat, hasMipmaps, blackIsLand, useHeightmap));

            _labelStatus.Text = "Semeando Aldeias...";
            GenerateWorldMetadata(_currentMapData, width * 31 + height);
            int vCount = (int)_villageCountInput.Value;
            await Task.Run(() => ScatterVillages(_currentMapData, vCount));

            RenderCurrentMapData();
            await RefreshEditorGlobe();

            _loadingPanel.Visible = false;
        }

        private MapData GenerateImageMapData(int width, int height, byte[] imgData, Image.Format imgFormat, bool hasMipmaps, bool blackIsLand, bool useHeightmap)
        {
            MapData map = new MapData();
            map.Dimensions = new Vector2I(width, height);
            map.Width = width;
            map.Height = height;
            map.MapName = "Mundo Por Imagem Mixado";

            Image threadImage = Image.CreateFromData(width, height, hasMipmaps, imgFormat, imgData);

            float dynamicFreq = 2.0f / width; 
            
            FastNoiseLite noiseElev = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Seed = (int)GD.Randi(), Frequency = dynamicFreq, FractalType = FastNoiseLite.FractalTypeEnum.Fbm, FractalOctaves = 5 };
            FastNoiseLite noiseMoist = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Seed = (int)GD.Randi() + 100, Frequency = dynamicFreq * 1.5f, FractalType = FastNoiseLite.FractalTypeEnum.Fbm };
            FastNoiseLite noiseTemp = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Seed = (int)GD.Randi() + 200, Frequency = dynamicFreq * 0.8f };

            int totalPixels = width * height;
            int processed = 0;

            for (int y = 0; y < height; y++)
            {
                // Mapeamento Equiretangular Perfeito (Latitude -90 a 90)
                float lat = ((float)y / height) * Mathf.Pi - (Mathf.Pi / 2.0f);
                float baseTemp = 1.0f - (Mathf.Abs(lat) / (Mathf.Pi / 2.0f)) * 2.0f; // 1 no Equador, -1 nos Polos

                for (int x = 0; x < width; x++)
                {
                    Color pixelColor = threadImage.GetPixel(x, y);
                    float pVal = pixelColor.V;
                    bool isLandFromImage = blackIsLand ? pVal < 0.5f : pVal > 0.5f;

                    int chunkX = x / ChunkData.CHUNK_SIZE;
                    int chunkY = y / ChunkData.CHUNK_SIZE;
                    Vector2I chunkPos = new Vector2I(chunkX, chunkY);

                    if (!map.Chunks.ContainsKey(chunkPos))
                        map.Chunks[chunkPos] = new ChunkData(chunkPos);

                    int localX = x % ChunkData.CHUNK_SIZE;
                    int localY = y % ChunkData.CHUNK_SIZE;
                    int flatIndex = localY * ChunkData.CHUNK_SIZE + localX;

                    byte terrain;

                    float lon = ((float)x / width) * Mathf.Pi * 2.0f;
                    float radius = width / (Mathf.Pi * 2.0f);
                    float nx = Mathf.Cos(lat) * Mathf.Cos(lon) * radius;
                    float nz = Mathf.Cos(lat) * Mathf.Sin(lon) * radius;
                    float ny = Mathf.Sin(lat) * radius;

                    if (isLandFromImage)
                    {
                        float e;
                        if (useHeightmap)
                        {
                            float rawH = blackIsLand ? (1.0f - pVal) : pVal;
                            e = (rawH - 0.5f) * 2.0f; // mapeia de [0.5, 1.0] para [0.0, 1.0]
                        }
                        else
                        {
                            e = noiseElev.GetNoise3D(nx, ny, nz); 
                            if (e < 0.05f) e = 0.05f; 
                        }

                        float m = noiseMoist.GetNoise3D(nx, ny, nz);
                        float tNoise = noiseTemp.GetNoise3D(nx, ny, nz);
                        float t = baseTemp + (tNoise * 0.5f);

                        terrain = GetBiome(e, m, t);
                    }
                    else
                    {
                        float e = noiseElev.GetNoise3D(nx, ny, nz); 
                        terrain = e < -0.3f ? (byte)TerrainType.DeepOcean : (byte)TerrainType.ShallowWater;
                    }

                    map.Chunks[chunkPos].TerrainMap[flatIndex] = terrain;

                    processed++;
                    if (processed % 10000 == 0)
                    {
                        _generationProgress = ((float)processed / totalPixels) * 100f;
                    }
                }
            }
            _generationProgress = 100f;
            return map;
        }

        private async void OnBtnGenerateNoisePressed()
        {
            ClearWorld();

            int targetWidth = 1000;
            int targetHeight = 1000;
            int.TryParse(_inputWidth.Text, out targetWidth);
            int.TryParse(_inputHeight.Text, out targetHeight);

            targetWidth = Mathf.CeilToInt((float)targetWidth / ChunkData.CHUNK_SIZE) * ChunkData.CHUNK_SIZE;
            targetHeight = Mathf.CeilToInt((float)targetHeight / ChunkData.CHUNK_SIZE) * ChunkData.CHUNK_SIZE;

            _loadingPanel.Visible = true;
            _generationProgress = 0f;
            _labelStatus.Text = "Gerando Globo Perfeito (Equiretangular)...";

            string seedStr = _inputSeed.Text;
            int seed = string.IsNullOrWhiteSpace(seedStr) ? (int)GD.Randi() : seedStr.GetHashCode();

            _currentMapData = await Task.Run(() => GenerateNoiseMapData(targetWidth, targetHeight, seed));

            _labelStatus.Text = "Semeando Aldeias...";
            GenerateWorldMetadata(_currentMapData, seed);
            int vCount = (int)_villageCountInput.Value;
            await Task.Run(() => ScatterVillages(_currentMapData, vCount));

            RenderCurrentMapData();
            await RefreshEditorGlobe();

            _loadingPanel.Visible = false;
        }

        private MapData GenerateNoiseMapData(int targetWidth, int targetHeight, int seed)
        {
            MapData map = new MapData();
            map.Dimensions = new Vector2I(targetWidth, targetHeight);
            map.Width = targetWidth;
            map.Height = targetHeight;
            map.MapName = "Mundo Procedural Realista";

            // Aumentando a frequÃªncia para criar mÃºltiplos continentes menores ao invÃ©s de uma Pangeia
            float dynamicFreq = 5.0f / targetWidth; 

            FastNoiseLite noiseElev = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Seed = seed, Frequency = dynamicFreq, FractalType = FastNoiseLite.FractalTypeEnum.Fbm, FractalOctaves = 5 };
            FastNoiseLite noiseMoist = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Seed = seed + 100, Frequency = dynamicFreq * 1.5f, FractalType = FastNoiseLite.FractalTypeEnum.Fbm };
            FastNoiseLite noiseTemp = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Seed = seed + 200, Frequency = dynamicFreq * 0.8f };

            int totalPixels = targetWidth * targetHeight;
            int processed = 0;

            for (int y = 0; y < targetHeight; y++)
            {
                float lat = ((float)y / targetHeight) * Mathf.Pi - (Mathf.Pi / 2.0f);
                float baseTemp = 1.0f - (Mathf.Abs(lat) / (Mathf.Pi / 2.0f)) * 2.0f; 

                for (int x = 0; x < targetWidth; x++)
                {
                    float lon = ((float)x / targetWidth) * Mathf.Pi * 2.0f;
                    float radius = targetWidth / (Mathf.Pi * 2.0f);
                    float nx = Mathf.Cos(lat) * Mathf.Cos(lon) * radius;
                    float nz = Mathf.Cos(lat) * Mathf.Sin(lon) * radius;
                    float ny = Mathf.Sin(lat) * radius;

                    float e = noiseElev.GetNoise3D(nx, ny, nz); 
                    float m = noiseMoist.GetNoise3D(nx, ny, nz);
                    float tNoise = noiseTemp.GetNoise3D(nx, ny, nz);
                    float t = baseTemp + (tNoise * 0.5f); 

                    byte terrain = GetBiome(e, m, t);
                    
                    int chunkX = x / ChunkData.CHUNK_SIZE;
                    int chunkY = y / ChunkData.CHUNK_SIZE;
                    Vector2I chunkPos = new Vector2I(chunkX, chunkY);

                    if (!map.Chunks.ContainsKey(chunkPos))
                        map.Chunks[chunkPos] = new ChunkData(chunkPos);

                    int localX = x % ChunkData.CHUNK_SIZE;
                    int localY = y % ChunkData.CHUNK_SIZE;
                    int flatIndex = localY * ChunkData.CHUNK_SIZE + localX;

                    map.Chunks[chunkPos].TerrainMap[flatIndex] = terrain;

                    processed++;
                    if (processed % 10000 == 0)
                    {
                        _generationProgress = ((float)processed / totalPixels) * 100f;
                    }
                }
            }
            _generationProgress = 100f;
            return map;
        }

        private byte GetBiome(float e, float m, float t)
        {
            if (e < -0.2f) return (byte)TerrainType.DeepOcean;
            if (e < 0.0f) return (byte)TerrainType.ShallowWater;

            if (e > 0.6f) return (byte)TerrainType.HighPeak;
            if (e > 0.4f) return (byte)TerrainType.Mountain;

            if (e >= 0.0f && e < 0.05f) return (byte)TerrainType.Beach;

            if (t < -0.3f)
            {
                if (m < 0.0f) return (byte)TerrainType.Tundra;
                return (byte)TerrainType.Snow;
            }
            else if (t > 0.3f)
            {
                if (m < -0.2f) return (byte)TerrainType.Desert;
                if (m < 0.2f) return (byte)TerrainType.Savanna;
                return (byte)TerrainType.Jungle;
            }
            else
            {
                if (m < -0.2f) return (byte)TerrainType.Grassland;
                return (byte)TerrainType.Forest;
            }
        }

        private void GenerateWorldMetadata(MapData map, int seed)
        {
            GameManager.RehydrateMap(map);
            GenerateMoonTextureForMap(map, seed);
            GenerateRivers(map, seed + 991);
            GenerateResourceRegions(map, seed + 1997);
        }

        private void EnsureWorldMetadata(MapData map, int seed)
        {
            GameManager.RehydrateMap(map);
            if (map.MoonTextureData == null || map.MoonTextureWidth <= 0 || map.MoonTextureHeight <= 0)
                GenerateMoonTextureForMap(map, seed);
            if (map.Rivers == null || map.Rivers.Count == 0)
                GenerateRivers(map, seed + 991);
            if (map.ResourceRegions == null || map.ResourceRegions.Count == 0)
                GenerateResourceRegions(map, seed + 1997);
        }

        private void GenerateMoonTextureForMap(MapData map, int seed)
        {
            Image img = Image.CreateEmpty(256, 128, false, Image.Format.Rgba8);
            var rng = new RandomNumberGenerator { Seed = (ulong)(uint)seed };

            for (int y = 0; y < img.GetHeight(); y++)
            {
                for (int x = 0; x < img.GetWidth(); x++)
                {
                    float latShade = 0.04f * Mathf.Sin((float)y / img.GetHeight() * Mathf.Pi);
                    float shade = 0.54f + latShade + 0.09f * Mathf.Sin(x * 0.09f + seed) * Mathf.Sin(y * 0.13f);
                    img.SetPixel(x, y, new Color(shade, shade, shade * 0.96f, 1f));
                }
            }

            for (int i = 0; i < 42; i++)
            {
                Vector2 center = new Vector2(rng.RandiRange(0, 255), rng.RandiRange(4, 123));
                float radius = rng.RandfRange(3f, 18f);
                for (int y = Mathf.Max(0, (int)(center.Y - radius)); y < Mathf.Min(128, (int)(center.Y + radius)); y++)
                {
                    for (int x = Mathf.Max(0, (int)(center.X - radius)); x < Mathf.Min(256, (int)(center.X + radius)); x++)
                    {
                        float d = center.DistanceTo(new Vector2(x, y)) / radius;
                        if (d > 1f) continue;
                        Color baseColor = img.GetPixel(x, y);
                        float rim = Mathf.SmoothStep(0.55f, 1.0f, d);
                        float crater = Mathf.Lerp(0.58f, 1.16f, rim);
                        img.SetPixel(x, y, new Color(baseColor.R * crater, baseColor.G * crater, baseColor.B * crater, 1f));
                    }
                }
            }

            map.MoonTextureWidth = img.GetWidth();
            map.MoonTextureHeight = img.GetHeight();
            map.MoonTextureData = img.GetData();
        }

        private void GenerateRivers(MapData map, int seed)
        {
            map.Rivers.Clear();
            var rng = new RandomNumberGenerator { Seed = (ulong)(uint)seed };
            var starts = new List<Vector2I>();

            foreach (var kvp in map.Chunks)
            {
                int baseX = kvp.Key.X * ChunkData.CHUNK_SIZE;
                int baseY = kvp.Key.Y * ChunkData.CHUNK_SIZE;
                ChunkData chunk = kvp.Value;
                for (int y = 0; y < ChunkData.CHUNK_SIZE; y += 4)
                {
                    for (int x = 0; x < ChunkData.CHUNK_SIZE; x += 4)
                    {
                        int mapX = baseX + x;
                        int mapY = baseY + y;
                        if (mapX >= map.Width || mapY >= map.Height) continue;
                        byte terrain = chunk.TerrainMap[y * ChunkData.CHUNK_SIZE + x];
                        if ((terrain == (byte)TerrainType.Mountain || terrain == (byte)TerrainType.HighPeak || terrain == (byte)TerrainType.Snow) && rng.Randf() < 0.035f)
                            starts.Add(new Vector2I(mapX, mapY));
                    }
                }
            }

            int maxRivers = Mathf.Clamp(map.Width * map.Height / (256 * 256), 4, 22);
            for (int i = 0; i < starts.Count && map.Rivers.Count < maxRivers; i++)
            {
                Vector2I start = starts[(int)(rng.Randi() % (uint)starts.Count)];
                RiverData river = TraceRiver(map, start, rng);
                if (river.Points.Count >= 8)
                {
                    map.Rivers.Add(river);
                    CarveRiverTerrain(map, river);
                }
            }
        }

        private RiverData TraceRiver(MapData map, Vector2I start, RandomNumberGenerator rng)
        {
            var river = new RiverData { Width = rng.RandfRange(0.8f, 1.8f) };
            var visited = new HashSet<Vector2I>();
            Vector2I current = start;
            int targetY = map.Height / 2;

            for (int step = 0; step < 420; step++)
            {
                if (!visited.Add(current)) break;
                if (step % 3 == 0)
                    river.Points.Add(new MapPointData { X = current.X, Y = current.Y });

                byte terrain = PlanetMeshBuilder.GetTerrainAt(map, current.X, current.Y);
                if (PlanetMeshBuilder.IsOcean(terrain) && step > 10) break;

                Vector2I best = current;
                float bestScore = float.MaxValue;
                for (int oy = -1; oy <= 1; oy++)
                {
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        if (ox == 0 && oy == 0) continue;
                        int nx = WrapMapX(current.X + ox, map.Width);
                        int ny = Mathf.Clamp(current.Y + oy, 0, map.Height - 1);
                        byte nt = PlanetMeshBuilder.GetTerrainAt(map, nx, ny);
                        float height = PlanetMeshBuilder.GetTerrainHeight(nt);
                        float equatorPull = Mathf.Abs(ny - targetY) * 0.00008f;
                        float jitter = rng.Randf() * 0.006f;
                        float waterBonus = PlanetMeshBuilder.IsOcean(nt) ? -0.08f : 0f;
                        float score = height + equatorPull + jitter + waterBonus;
                        if (score < bestScore)
                        {
                            bestScore = score;
                            best = new Vector2I(nx, ny);
                        }
                    }
                }

                if (best == current) break;
                current = best;
            }

            return river;
        }

        private void CarveRiverTerrain(MapData map, RiverData river)
        {
            foreach (MapPointData point in river.Points)
            {
                for (int oy = -1; oy <= 1; oy++)
                {
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        int x = WrapMapX(point.X + ox, map.Width);
                        int y = Mathf.Clamp(point.Y + oy, 0, map.Height - 1);
                        int cx = x / ChunkData.CHUNK_SIZE;
                        int cy = y / ChunkData.CHUNK_SIZE;
                        if (!map.Chunks.TryGetValue(new Vector2I(cx, cy), out ChunkData chunk)) continue;

                        int localX = x % ChunkData.CHUNK_SIZE;
                        int localY = y % ChunkData.CHUNK_SIZE;
                        int index = localY * ChunkData.CHUNK_SIZE + localX;
                        byte terrain = chunk.TerrainMap[index];
                        if (!PlanetMeshBuilder.IsOcean(terrain) && terrain != (byte)TerrainType.HighPeak)
                            chunk.TerrainMap[index] = (byte)TerrainType.ShallowWater;
                    }
                }
            }
        }

        private void GenerateResourceRegions(MapData map, int seed)
        {
            map.ResourceRegions.Clear();
            var rng = new RandomNumberGenerator { Seed = (ulong)(uint)seed };
            string[] minerals = { "gold", "diamond", "stone", "iron", "bronze", "silver", "platinum", "coal", "oil" };
            string[] forests = { "hardwood_forest", "fruit_forest", "medicinal_plants" };
            string[] animals = { "fish", "whales", "sharks", "birds", "cattle", "deer" };

            int target = Mathf.Clamp(map.Width * map.Height / (180 * 180), 35, 260);
            int attempts = target * 20;
            while (map.ResourceRegions.Count < target && attempts-- > 0)
            {
                int x = rng.RandiRange(0, map.Width - 1);
                int y = rng.RandiRange(0, map.Height - 1);
                byte terrain = PlanetMeshBuilder.GetTerrainAt(map, x, y);
                bool ocean = PlanetMeshBuilder.IsOcean(terrain);

                string id;
                string category;
                if (ocean)
                {
                    id = animals[rng.RandiRange(0, 2)];
                    category = "animal_migratory";
                }
                else if (terrain == (byte)TerrainType.Mountain || terrain == (byte)TerrainType.HighPeak || terrain == (byte)TerrainType.Tundra)
                {
                    id = minerals[rng.RandiRange(0, minerals.Length - 1)];
                    category = "mineral";
                }
                else if (terrain == (byte)TerrainType.Forest || terrain == (byte)TerrainType.Jungle)
                {
                    id = rng.Randf() < 0.65f ? forests[rng.RandiRange(0, forests.Length - 1)] : animals[rng.RandiRange(3, animals.Length - 1)];
                    category = id.Contains("forest") || id.Contains("plants") ? "forest" : "animal";
                }
                else if (terrain == (byte)TerrainType.Grassland || terrain == (byte)TerrainType.Savanna)
                {
                    id = rng.Randf() < 0.55f ? "cattle" : "birds";
                    category = "animal";
                }
                else if (terrain == (byte)TerrainType.Desert)
                {
                    id = rng.Randf() < 0.5f ? "oil" : "stone";
                    category = "mineral";
                }
                else
                {
                    continue;
                }

                map.ResourceRegions.Add(new ResourceRegionData
                {
                    ResourceId = id,
                    Category = category,
                    X = x,
                    Y = y,
                    Radius = rng.RandiRange(ocean ? 18 : 8, ocean ? 70 : 32),
                    Richness = rng.RandfRange(0.35f, 1.0f),
                    MigrationAngle = ocean ? rng.RandfRange(0f, Mathf.Tau) : 0f,
                    MigrationSpeed = ocean && category == "animal_migratory" ? rng.RandfRange(0.02f, 0.18f) : 0f
                });
            }
        }

        private int WrapMapX(int x, int width)
        {
            int wrapped = x % width;
            return wrapped < 0 ? wrapped + width : wrapped;
        }

        private void ScatterVillages(MapData map, int count)
        {
            map.Villages.Clear();
            
            // UI Update: Buscando Terras
            CallDeferred(nameof(UpdateProgressDeferred), 0f, "Buscando terra firme...");
            
            // 1. Coletar todas as posiÃ§Ãµes vÃ¡lidas
            List<Vector2I> validPositions = new List<Vector2I>();
            foreach (var kvp in map.Chunks)
            {
                var chunkPos = kvp.Key;
                var chunk = kvp.Value;
                
                int startX = chunkPos.X * ChunkData.CHUNK_SIZE;
                int startY = chunkPos.Y * ChunkData.CHUNK_SIZE;

                for (int ly = 0; ly < ChunkData.CHUNK_SIZE; ly++)
                {
                    for (int lx = 0; lx < ChunkData.CHUNK_SIZE; lx++)
                    {
                        int flatIndex = ly * ChunkData.CHUNK_SIZE + lx;
                        byte terrain = chunk.TerrainMap[flatIndex];
                        if (terrain >= 3 && terrain != 10 && terrain != 11) // Terra habitÃ¡vel
                        {
                            validPositions.Add(new Vector2I(startX + lx, startY + ly));
                        }
                    }
                }
            }

            // UI Update: Embaralhando
            CallDeferred(nameof(UpdateProgressDeferred), 10f, "Sorteando Capitais...");

            // 2. Fisher-Yates Shuffle
            int n = validPositions.Count;
            for (int i = 0; i < n - 1; i++)
            {
                int r = i + (int)(GD.Randi() % (uint)(n - i));
                Vector2I temp = validPositions[i];
                validPositions[i] = validPositions[r];
                validPositions[r] = temp;
            }

            // UI Update: Semeando
            CallDeferred(nameof(UpdateProgressDeferred), 20f, "Fundando ImpÃ©rios...");

            // 3. Fundar as aldeias
            int placed = Math.Min(count, validPositions.Count);
            int villageIdCounter = 1;
            
            for (int i = 0; i < placed; i++)
            {
                Vector2I pos = validPositions[i];
                var newVillage = new VillageData {
                    Id = villageIdCounter,
                    Name = VillageNameGenerator.GenerateName(villageIdCounter * 7919 + 31337),
                    X = pos.X,
                    Y = pos.Y,
                    OwnerId = villageIdCounter, 
                    Level = 1
                };
                map.Villages.Add(newVillage);
                
                int chunkX = pos.X / ChunkData.CHUNK_SIZE;
                int chunkY = pos.Y / ChunkData.CHUNK_SIZE;
                if (map.Chunks.TryGetValue(new Vector2I(chunkX, chunkY), out ChunkData chunk))
                {
                    int lx = pos.X % ChunkData.CHUNK_SIZE;
                    int ly = pos.Y % ChunkData.CHUNK_SIZE;
                    chunk.TerritoryMap[ly * ChunkData.CHUNK_SIZE + lx] = (byte)(villageIdCounter % 255 == 0 ? 1 : villageIdCounter % 255);
                }
                villageIdCounter++;
            }

            // Passo 2: Voronoi (Parallel Level-Synchronous BFS)
            CallDeferred(nameof(UpdateProgressDeferred), 30f, "AvanÃ§ando Fronteiras (Voronoi)...");

            int[] dx = { 1, -1, 0, 0 };
            int[] dy = { 0, 0, 1, -1 };
            
            var frontier = new System.Collections.Concurrent.ConcurrentBag<Vector2I>();
            foreach (var v in map.Villages)
            {
                frontier.Add(new Vector2I(v.X, v.Y));
            }

            int expansionLevel = 0;
            while (!frontier.IsEmpty)
            {
                var nextFrontier = new System.Collections.Concurrent.ConcurrentBag<Vector2I>();
                var currentLevel = frontier.ToArray();
                
                System.Threading.Tasks.Parallel.ForEach(currentLevel, curr =>
                {
                    int cx = curr.X / ChunkData.CHUNK_SIZE;
                    int cy = curr.Y / ChunkData.CHUNK_SIZE;
                    if (!map.Chunks.TryGetValue(new Vector2I(cx, cy), out ChunkData currentChunk)) return;
                    
                    int lx = curr.X % ChunkData.CHUNK_SIZE;
                    int ly = curr.Y % ChunkData.CHUNK_SIZE;
                    byte currentOwner = currentChunk.TerritoryMap[ly * ChunkData.CHUNK_SIZE + lx];

                    for (int i = 0; i < 4; i++)
                    {
                        int nx = curr.X + dx[i];
                        int ny = curr.Y + dy[i];
                        
                        if (nx < 0 || nx >= map.Width || ny < 0 || ny >= map.Height) continue;
                        
                        int ncx = nx / ChunkData.CHUNK_SIZE;
                        int ncy = ny / ChunkData.CHUNK_SIZE;
                        if (map.Chunks.TryGetValue(new Vector2I(ncx, ncy), out ChunkData nChunk))
                        {
                            int nlx = nx % ChunkData.CHUNK_SIZE;
                            int nly = ny % ChunkData.CHUNK_SIZE;
                            int nFlat = nly * ChunkData.CHUNK_SIZE + nlx;
                            
                            byte nTerrain = nChunk.TerrainMap[nFlat];
                            byte nOwner = nChunk.TerritoryMap[nFlat];
                            
                            // Expande apenas para terra firme que ainda nÃ£o tem dono
                            if (nOwner == 0 && nTerrain >= 3 && nTerrain != 11)
                            {
                                // OperaÃ§Ã£o atÃ´mica simulada - corrida simples resulta em bordas irregulares naturais
                                lock(nChunk) 
                                {
                                    if (nChunk.TerritoryMap[nFlat] == 0)
                                    {
                                        nChunk.TerritoryMap[nFlat] = currentOwner;
                                        nextFrontier.Add(new Vector2I(nx, ny));
                                    }
                                }
                            }
                        }
                    }
                });

                frontier = nextFrontier;
                expansionLevel++;
                
                // Limitar o cÃ¡lculo para nÃ£o travar 100%
                if (expansionLevel % 10 == 0)
                {
                    float prog = 30f + Math.Min(65f, expansionLevel * 0.2f);
                    CallDeferred(nameof(UpdateProgressDeferred), prog, $"Mapeando Fronteiras (NÃ­vel {expansionLevel})...");
                }
            }

            CallDeferred(nameof(UpdateProgressDeferred), 100f, "ConcluÃ­do!");
            GD.Print($"Semeou {placed} aldeias e gerou fronteiras em {expansionLevel} iteraÃ§Ãµes.");
        }

        private void UpdateProgressDeferred(float progress, string status)
        {
            _progressBar.Value = progress;
            _labelStatus.Text = status;
        }

        private void RenderCurrentMapData()
        {
            if (_currentMapData == null || _currentMapData.Chunks == null) return;

            foreach (Node child in _worldContainer.GetChildren())
            {
                child.QueueFree();
            }
            _activeVisualizers.Clear();

            foreach (var kvp in _currentMapData.Chunks)
            {
                ChunkVisualizer visualizer = new ChunkVisualizer();
                _worldContainer.AddChild(visualizer);
                visualizer.RenderChunk(kvp.Value);
                _activeVisualizers[kvp.Key] = visualizer;
            }
        }

        private async void OnBtnSaveMapPressed()
        {
            if (_currentMapData == null || _currentMapData.Chunks.Count == 0) return;

            string mapName = _inputMapName != null ? _inputMapName.Text : _currentMapData.MapName;
            string path = GameManager.Instance.GetMapPathForName(mapName);
            _currentMapData.MapName = GameManager.CleanMapFileName(mapName);
            EnsureWorldMetadata(_currentMapData, _currentMapData.MapName.GetHashCode());

            if (System.IO.File.Exists(path))
            {
                _pendingSavePath = path;
                _overwriteDialog.DialogText = $"O mapa '{System.IO.Path.GetFileName(path)}' ja existe.\nSobrescrever ou criar uma copia numerada?";
                _overwriteDialog.PopupCentered();
                return;
            }

            await SaveMapToPath(path);
        }

        private async void OnOverwriteConfirmed()
        {
            if (string.IsNullOrWhiteSpace(_pendingSavePath)) return;
            await SaveMapToPath(_pendingSavePath);
            _pendingSavePath = null;
        }

        private async void OnOverwriteDeclined()
        {
            if (string.IsNullOrWhiteSpace(_pendingSavePath)) return;
            string copyPath = GameManager.GetNonConflictingPath(_pendingSavePath);
            await SaveMapToPath(copyPath);
            _pendingSavePath = null;
        }

        private async Task SaveMapToPath(string path)
        {
            _saveFeedback.Text = "Salvando...";
            _saveFeedback.Visible = true;

            await Task.Run(() => GameManager.SaveMap(path, _currentMapData));
            if (_inputMapName != null)
                _inputMapName.Text = System.IO.Path.GetFileNameWithoutExtension(path);

            RefreshSavedMapList();
            string shortPath = path.Length > 35 ? "..." + path.Substring(path.Length - 32) : path;
            _saveFeedback.Text = $"Salvo! ({shortPath})";
        }
        private void OnBtnExportMapPressed()
        {
            if (_currentMapData == null || _currentMapData.Chunks.Count == 0) return;

            string dir = OS.GetSystemDir(OS.SystemDir.Desktop);
            DisplayServer.FileDialogShow("Exportar Mapa", dir, "meu_mapa.json", false, DisplayServer.FileDialogMode.SaveFile, new string[] { "*.json ; Arquivo JSON" }, Callable.From<bool, string[], int>(OnNativeFileExportSelected));
        }

        private async void OnNativeFileExportSelected(bool status, string[] selectedPaths, int selectedFilterIndex)
        {
            if (status && selectedPaths.Length > 0)
            {
                string path = selectedPaths[0];
                _saveFeedback.Text = "Exportando...";
                _saveFeedback.Visible = true;

                await System.Threading.Tasks.Task.Run(() =>
                {
                    GameManager.SaveMap(path, _currentMapData);
                });

                _saveFeedback.Text = $"âœ… Exportado para:\n{path}";
            }
        }

        private void RefreshSavedMapList()
        {
            if (_optionSavedMaps == null) return;
            _optionSavedMaps.Clear();

            foreach (string file in GameManager.Instance.GetSavedMapFiles())
            {
                _optionSavedMaps.AddItem(System.IO.Path.GetFileName(file));
            }

            _optionSavedMaps.Disabled = _optionSavedMaps.ItemCount == 0;
            if (_optionSavedMaps.ItemCount == 0)
                _optionSavedMaps.AddItem("Nenhum mapa salvo");
        }

        private async void OnBtnLoadSelectedMapPressed()
        {
            if (_optionSavedMaps == null || _optionSavedMaps.Disabled || _optionSavedMaps.Selected < 0) return;
            string fileName = _optionSavedMaps.GetItemText(_optionSavedMaps.Selected);
            string path = System.IO.Path.Combine(GameManager.Instance.GetMapsDir(), fileName);
            await LoadMapFromPath(path);
        }

        private void OnBtnLoadMapPressed()
        {
            string dir = GameManager.Instance.GetMapsDir();
            DisplayServer.FileDialogShow("Carregar Mapa", dir, "", false, DisplayServer.FileDialogMode.OpenFile, new string[] { "*.json ; Arquivo JSON" }, Callable.From<bool, string[], int>(OnNativeFileLoadSelected));
        }

        private async void OnNativeFileLoadSelected(bool status, string[] selectedPaths, int selectedFilterIndex)
        {
            if (status && selectedPaths.Length > 0)
            {
                await LoadMapFromPath(selectedPaths[0]);
            }
        }

        private async Task LoadMapFromPath(string path)
        {
            ClearWorld();
            _loadingPanel.Visible = true;
            _generationProgress = 0f;
            _labelStatus.Text = "Lendo arquivo do disco...";

            _currentMapData = await Task.Run(() => GameManager.LoadMap(path));
            if (_currentMapData == null)
            {
                _saveFeedback.Text = "Erro ao carregar mapa!";
                _saveFeedback.Visible = true;
                _loadingPanel.Visible = false;
                return;
            }

            if (_inputMapName != null)
                _inputMapName.Text = System.IO.Path.GetFileNameWithoutExtension(path);

            _labelStatus.Text = "Renderizando Terreno...";
            _generationProgress = 65f;
            RenderCurrentMapData();

            await RefreshEditorGlobe();

            _generationProgress = 100f;
            _saveFeedback.Text = $"{_currentMapData.Villages?.Count ?? 0} aldeias carregadas";
            _saveFeedback.Visible = true;
            _loadingPanel.Visible = false;
        }

        private async Task RefreshEditorGlobe()
        {
            _labelStatus.Text = "Costurando Globo...";
            _generationProgress = 80f;
            _globeContainer.Visible = true;
            _worldContainer.Visible = false;

            Image globeImg = await Task.Run(() => GenerateGlobeImage());
            _planetMesh.Mesh = PlanetMeshBuilder.CreatePlanetMesh(_currentMapData, 128, 64);
            var material = new StandardMaterial3D();
            material.AlbedoTexture = ImageTexture.CreateFromImage(globeImg);
            material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            material.TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest;
            _planetMesh.SetSurfaceOverrideMaterial(0, material);
            _tacticalView.MapData = _currentMapData;
        }

        private async void OnBtnLoadGameSavePressed()
        {
            string savePath = System.IO.Path.Combine(GameManager.Instance.GetPartidasDir(), "Slot_1", "save_atual.json");
            if (!System.IO.File.Exists(savePath))
            {
                _saveFeedback.Text = "Nenhuma partida salva encontrada!";
                _saveFeedback.Visible = true;
                return;
            }

            await LoadMapFromPath(savePath);
            if (_currentMapData != null)
                _saveFeedback.Text = $"Partida carregada ({_currentMapData.Villages?.Count ?? 0} aldeias)";
        }
        private async void OnBtnToggle3DPressed()
        {
            if (_currentMapData == null) return;

            _globeContainer.Visible = !_globeContainer.Visible;
            _worldContainer.Visible = !_globeContainer.Visible;

            if (_globeContainer.Visible)
            {
                _loadingPanel.Visible = true;
                _generationProgress = 0f;
                await RefreshEditorGlobe();
                _loadingPanel.Visible = false;
            }
        }

        private Image GenerateGlobeImage()
        {
            int mapW = _currentMapData.Dimensions.X;
            int mapH = _currentMapData.Dimensions.Y;
            
            int globeW = Math.Min(mapW, 2048);
            int globeH = Math.Min(mapH, 1024);
            
            float scaleX = (float)mapW / globeW;
            float scaleY = (float)mapH / globeH;

            Image globeImg = Image.CreateEmpty(globeW, globeH, false, Image.Format.Rgba8);

            int totalPixels = globeW * globeH;
            int processed = 0;

            for (int y = 0; y < globeH; y++)
            {
                for (int x = 0; x < globeW; x++)
                {
                    int realX = (int)(x * scaleX);
                    int realY = (int)(y * scaleY);

                    int chunkX = realX / ChunkData.CHUNK_SIZE;
                    int chunkY = realY / ChunkData.CHUNK_SIZE;
                    Vector2I chunkPos = new Vector2I(chunkX, chunkY);

                    if (_currentMapData.Chunks.TryGetValue(chunkPos, out ChunkData chunk))
                    {
                        int localX = realX % ChunkData.CHUNK_SIZE;
                        int localY = realY % ChunkData.CHUNK_SIZE;
                        int flatIndex = localY * ChunkData.CHUNK_SIZE + localX;
                        byte terrainType = chunk.TerrainMap[flatIndex];
                        byte territoryOwner = chunk.TerritoryMap[flatIndex];
                        
                        Color c = GetTerrainColor(terrainType);
                        
                        if (_toggleBordersCheckbox.ButtonPressed && territoryOwner > 0 && territoryOwner != 255)
                        {
                            // Detectar se Ã© borda olhando para os 4 vizinhos
                            bool isBorder = false;
                            int[] dx = { 1, -1, 0, 0 };
                            int[] dy = { 0, 0, 1, -1 };
                            
                            for (int i=0; i<4; i++)
                            {
                                int nx = realX + dx[i];
                                int ny = realY + dy[i];
                                if (nx >= 0 && nx < _currentMapData.Width && ny >= 0 && ny < _currentMapData.Height)
                                {
                                    int ncx = nx / ChunkData.CHUNK_SIZE;
                                    int ncy = ny / ChunkData.CHUNK_SIZE;
                                    if (_currentMapData.Chunks.TryGetValue(new Vector2I(ncx, ncy), out ChunkData nChunk))
                                    {
                                        byte nOwner = nChunk.TerritoryMap[(ny % ChunkData.CHUNK_SIZE) * ChunkData.CHUNK_SIZE + (nx % ChunkData.CHUNK_SIZE)];
                                        if (nOwner != territoryOwner && nOwner != 255)
                                        {
                                            isBorder = true;
                                            break;
                                        }
                                    }
                                }
                            }
                            
                            if (isBorder)
                            {
                                // Borda Escura Elegante
                                c = new Color(0.2f, 0.2f, 0.2f, 1.0f);
                            }
                        }

                        globeImg.SetPixel(x, y, c);
                    }
                    else
                    {
                        globeImg.SetPixel(x, y, new Color(0,0,0));
                    }

                    processed++;
                    if (processed % 10000 == 0)
                    {
                        _generationProgress = ((float)processed / totalPixels) * 100f;
                    }
                }
            }

            _generationProgress = 100f;
            return globeImg;
        }

        private Color GetTerrainColor(byte type)
        {
            switch (type)
            {
                case 0: return new Color(0.0f, 0.1f, 0.5f);
                case 1: return new Color(0.2f, 0.4f, 0.8f);
                case 2: return new Color(0.9f, 0.8f, 0.5f);
                case 3: return new Color(0.3f, 0.7f, 0.2f);
                case 4: return new Color(0.1f, 0.5f, 0.1f);
                case 5: return new Color(0.05f, 0.3f, 0.05f);
                case 6: return new Color(0.7f, 0.6f, 0.3f);
                case 7: return new Color(0.9f, 0.7f, 0.2f);
                case 8: return new Color(0.7f, 0.8f, 0.8f);
                case 9: return new Color(0.9f, 0.95f, 1.0f);
                case 10: return new Color(0.5f, 0.5f, 0.5f);
                case 11: return new Color(0.8f, 0.8f, 0.8f);
                default: return new Color(0,0,0);
            }
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            // Tab: toggle nomes de aldeias
            if (@event is InputEventKey keyEv && keyEv.Pressed && !keyEv.Echo)
            {
                if (keyEv.Keycode == Key.Tab)
                {
                    _tacticalView.ShowVillageNames = !_tacticalView.ShowVillageNames;
                    return;
                }
                if (keyEv.Keycode == Key.Escape && _isTacticalMode)
                {
                    ExitTacticalMode();
                    return;
                }
            }

            if (_globeContainer.Visible || _isTacticalMode)
            {
                if (@event is InputEventMouseButton mouseBtn)
                {
                    if (mouseBtn.ButtonIndex == MouseButton.Left)
                    {
                        _isDraggingGlobe = mouseBtn.Pressed;
                    }
                    else if (mouseBtn.ButtonIndex == MouseButton.WheelUp && mouseBtn.Pressed)
                    {
                        if (!_isTacticalMode)
                        {
                            _camera3D.Position = new Vector3(0, 0, Mathf.Max(1.02f, _camera3D.Position.Z - 0.15f));
                            if (_camera3D.Position.Z <= 1.08f)
                                EnterTacticalMode();
                        }
                        else
                        {
                            _tacticalView.ZoomLevel = Mathf.Min(128f, _tacticalView.ZoomLevel * 1.25f);
                        }
                    }
                    else if (mouseBtn.ButtonIndex == MouseButton.WheelDown && mouseBtn.Pressed)
                    {
                        if (!_isTacticalMode)
                        {
                            _camera3D.Position = new Vector3(0, 0, Mathf.Min(5.0f, _camera3D.Position.Z + 0.15f));
                        }
                        else
                        {
                            float nz = _tacticalView.ZoomLevel * 0.8f;
                            if (nz < 3.5f)
                                ExitTacticalMode();
                            else
                                _tacticalView.ZoomLevel = nz;
                        }
                    }
                }
                else if (@event is InputEventScreenTouch touchEvent)
                {
                    // Rastrear estado de cada dedo individualmente
                    if (touchEvent.Index == 0)
                    {
                        _finger0Down = touchEvent.Pressed;
                        _finger0Pos = touchEvent.Position;
                        if (touchEvent.Pressed)
                        {
                            _isDraggingGlobe = true;
                        }
                        else
                        {
                            _isDraggingGlobe = false;
                            _pinchPrevDistance = 0f;
                        }
                    }
                    else if (touchEvent.Index == 1)
                    {
                        _finger1Down = touchEvent.Pressed;
                        _finger1Pos = touchEvent.Position;
                        if (touchEvent.Pressed)
                        {
                            // Segundo dedo tocou â€” inicia pinch
                            _pinchPrevDistance = _finger0Pos.DistanceTo(_finger1Pos);
                            _isDraggingGlobe = false; // Cancela pan enquanto pinÃ§a
                        }
                        else
                        {
                            _pinchPrevDistance = 0f;
                        }
                    }
                }
                else if (@event is InputEventMouseMotion mouseMotion)
                {
                    HandlePan(mouseMotion.Relative);
                }
                else if (@event is InputEventScreenDrag dragEvent)
                {
                    // Atualiza posiÃ§Ã£o do dedo que arrastou
                    if (dragEvent.Index == 0) _finger0Pos = dragEvent.Position;
                    else if (dragEvent.Index == 1) _finger1Pos = dragEvent.Position;

                    // Pinch ativo quando dois dedos estÃ£o na tela
                    if (_finger0Down && _finger1Down)
                        HandlePinch();
                    else
                        HandlePan(dragEvent.Relative);
                }
                return;
            }

            if (@event is InputEventMouseButton paintBtn)
            {
                if (paintBtn.ButtonIndex == MouseButton.Left)
                {
                    _isPainting = paintBtn.Pressed;
                    if (_isPainting) PaintAtMouse(GetGlobalMousePosition());
                }
            }
            else if (@event is InputEventMouseMotion paintMotion && _isPainting)
            {
                PaintAtMouse(GetGlobalMousePosition());
            }
            else if (@event is InputEventScreenDrag paintDrag && _isPainting)
            {
                PaintAtMouse(GetGlobalMousePosition());
            }
            else if (@event is InputEventScreenTouch paintTouch)
            {
                if (paintTouch.Index == 0)
                {
                    _isPainting = paintTouch.Pressed;
                    if (_isPainting) PaintAtMouse(GetGlobalMousePosition());
                }
            }
        }

        private void HandlePan(Vector2 relative)
        {
            if (_isDraggingGlobe && !_isTacticalMode)
            {
                bool isCtrlPressed = Input.IsKeyPressed(Key.Ctrl);
                bool isShiftPressed = Input.IsKeyPressed(Key.Shift);
                
                if (isCtrlPressed)
                {
                    Vector3 forward = _camera3D.Transform.Basis.Z;
                    _planetMesh.Rotate(forward.Normalized(), relative.X * 0.01f);
                }
                else if (isShiftPressed)
                {
                    Vector3 right = _camera3D.Transform.Basis.X;
                    _planetMesh.Rotate(right.Normalized(), relative.Y * 0.01f);
                }
                else
                {
                    _planetMesh.RotateY(relative.X * 0.01f);
                    _planetMesh.RotateX(relative.Y * 0.01f);
                }
            }
            else if (_isDraggingGlobe && _isTacticalMode)
            {
                _camera2D.Position -= relative * (_camera2D.Zoom.X > 0 ? 1.0f / _camera2D.Zoom.X : 1.0f);
            }
        }

        /// <summary>Processa o gesto de pinÃ§a (dois dedos) para dar zoom.</summary>
        private void HandlePinch()
        {
            float currentDistance = _finger0Pos.DistanceTo(_finger1Pos);
            if (_pinchPrevDistance <= 0f)
            {
                _pinchPrevDistance = currentDistance;
                return;
            }

            float delta = currentDistance - _pinchPrevDistance;
            _pinchPrevDistance = currentDistance;

            if (Mathf.Abs(delta) < 2f) return; // Threshold para evitar jitter

            if (!_isTacticalMode)
            {
                // Globo 3D
                float newZ = _camera3D.Position.Z - delta * 0.005f;
                newZ = Mathf.Clamp(newZ, 1.02f, 5.0f);
                _camera3D.Position = new Vector3(0, 0, newZ);
                if (newZ <= 1.08f)
                    EnterTacticalMode();
            }
            else
            {
                // VisÃ£o TÃ¡tica 2D
                float zoomFactor = 1.0f + delta * 0.008f;
                float newZoom = _tacticalView.ZoomLevel * zoomFactor;
                if (newZoom < 3.5f)
                    ExitTacticalMode();
                else
                    _tacticalView.ZoomLevel = Mathf.Clamp(newZoom, 3.5f, 128f);
            }
        }

        private void PaintAtMouse(Vector2 mousePos)
        {
            if (_currentMapData == null) return;

            int mapX = Mathf.FloorToInt(mousePos.X);
            int mapY = Mathf.FloorToInt(mousePos.Y);

            if (mapX < 0 || mapX >= _currentMapData.Dimensions.X || mapY < 0 || mapY >= _currentMapData.Dimensions.Y) return;

            int chunkX = mapX / ChunkData.CHUNK_SIZE;
            int chunkY = mapY / ChunkData.CHUNK_SIZE;
            Vector2I chunkPos = new Vector2I(chunkX, chunkY);

            if (_currentMapData.Chunks.TryGetValue(chunkPos, out ChunkData chunk))
            {
                int localX = mapX % ChunkData.CHUNK_SIZE;
                int localY = mapY % ChunkData.CHUNK_SIZE;
                int flatIndex = localY * ChunkData.CHUNK_SIZE + localX;

                byte selectedBrush = (byte)_optionBrush.GetSelectedId();
                chunk.TerrainMap[flatIndex] = selectedBrush;

                if (_activeVisualizers.TryGetValue(chunkPos, out ChunkVisualizer visualizer))
                {
                    visualizer.UpdatePixel(localX, localY, selectedBrush);
                }
            }
        }

        private void EnterTacticalMode()
        {
            if (_currentMapData == null) return;

            _isTacticalMode = true;
            _camera3D.Current = false;
            _camera2D.Enabled = true;
            _camera2D.MakeCurrent();
            _camera2D.Zoom = new Vector2(1, 1);

            _tacticalView.MapData = _currentMapData; // Garante que o mapa estÃ¡ setado
            _tacticalView.ZoomLevel = 4f;
            _tacticalView.Visible = true;
            _globeContainer.Visible = false;

            // Calcula o ponto do mapa que a cÃ¢mera estava vendo no globo
            Vector3 localCenter = _planetMesh.ToLocal(_camera3D.GlobalPosition).Normalized();
            Vector2I mapPoint = PlanetMeshBuilder.SphereDirectionToMap(localCenter, _currentMapData);

            _tacticalView.CenterX = mapPoint.X;
            _tacticalView.CenterY = mapPoint.Y;
            _camera2D.Position = Vector2.Zero;
            _tacticalView.QueueRedraw();
        }

        private void ExitTacticalMode()
        {
            _isTacticalMode = false;
            _camera3D.Current = true;
            _camera2D.Enabled = false;
            _tacticalView.Visible = false;
            _globeContainer.Visible = true;
        }

        private void OnBtnBackPressed()
        {
            GameManager.Instance?.GoToMainMenu();
        }
    }
}



