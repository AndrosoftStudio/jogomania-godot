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
            _globeContainer.Position = new Vector3(1.0f, 0, 0); // Globo mais deslocado para direita

            _planetMesh = GetNode<MeshInstance3D>("GlobeContainer/PlanetMesh");
            _camera3D = GetNode<Camera3D>("GlobeContainer/Camera3D");
            _camera3D.Current = true;

            _tacticalView = new TacticalView();
            _tacticalView.Visible = false;
            AddChild(_tacticalView);

            // Espaçamento da barra de rolagem — usa offset do ScrollContainer
            var scroll = GetNode<ScrollContainer>("UILayer/UIControl");
            scroll.OffsetRight = 330f; // Era 320, agora 330 para dar 10px de respiro

            // Criar UI de Aldeias Programaticamente
            var vbox = GetNode<VBoxContainer>("UILayer/UIControl/VBoxContainer");
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
            _toggleBordersCheckbox.Text = "Ver Fronteiras das Aldeias (Modo Político)";
            _toggleBordersCheckbox.ButtonPressed = true;
            _toggleBordersCheckbox.Toggled += (bool toggledOn) => { RenderCurrentMapData(); };
            vbox.AddChild(_toggleBordersCheckbox);
            vbox.MoveChild(_toggleBordersCheckbox, 8);

            // Botão mobile para ver nomes de aldeias
            var btnNames = new Button();
            btnNames.Text = "🏘 Nomes";
            btnNames.ToggleMode = true;
            btnNames.Toggled += (on) => { _tacticalView.ShowVillageNames = on; };
            vbox.AddChild(btnNames);
            vbox.MoveChild(btnNames, 9);

            _loadingPanel = GetNode<Control>("UILayer/LoadingPanel");
            _progressBar = GetNode<ProgressBar>("UILayer/LoadingPanel/VBoxContainer/ProgressBar");
            _labelStatus = GetNode<Label>("UILayer/LoadingPanel/VBoxContainer/LabelStatus");
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
            int vCount = (int)_villageCountInput.Value;
            await Task.Run(() => ScatterVillages(_currentMapData, vCount));

            RenderCurrentMapData();
            
            // Força a renderização do globo logo de cara
            _globeContainer.Visible = true;
            _worldContainer.Visible = false;
            _labelStatus.Text = "Costurando Textura do Globo...";
            Image globeImg = await Task.Run(() => GenerateGlobeImage());
            var material = new StandardMaterial3D();
            material.AlbedoTexture = ImageTexture.CreateFromImage(globeImg);
            material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            _planetMesh.SetSurfaceOverrideMaterial(0, material);

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
            int vCount = (int)_villageCountInput.Value;
            await Task.Run(() => ScatterVillages(_currentMapData, vCount));

            RenderCurrentMapData();

            // Força a renderização do globo logo de cara
            _globeContainer.Visible = true;
            _worldContainer.Visible = false;
            _labelStatus.Text = "Costurando Textura do Globo...";
            Image globeImg = await Task.Run(() => GenerateGlobeImage());
            var material = new StandardMaterial3D();
            material.AlbedoTexture = ImageTexture.CreateFromImage(globeImg);
            material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            _planetMesh.SetSurfaceOverrideMaterial(0, material);

            _loadingPanel.Visible = false;
        }

        private MapData GenerateNoiseMapData(int targetWidth, int targetHeight, int seed)
        {
            MapData map = new MapData();
            map.Dimensions = new Vector2I(targetWidth, targetHeight);
            map.Width = targetWidth;
            map.Height = targetHeight;
            map.MapName = "Mundo Procedural Realista";

            // Aumentando a frequência para criar múltiplos continentes menores ao invés de uma Pangeia
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

        private void ScatterVillages(MapData map, int count)
        {
            map.Villages.Clear();
            
            // UI Update: Buscando Terras
            CallDeferred(nameof(UpdateProgressDeferred), 0f, "Buscando terra firme...");
            
            // 1. Coletar todas as posições válidas
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
                        if (terrain >= 3 && terrain != 10 && terrain != 11) // Terra habitável
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
            CallDeferred(nameof(UpdateProgressDeferred), 20f, "Fundando Impérios...");

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
            CallDeferred(nameof(UpdateProgressDeferred), 30f, "Avançando Fronteiras (Voronoi)...");

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
                            
                            // Expande apenas para terra firme que ainda não tem dono
                            if (nOwner == 0 && nTerrain >= 3 && nTerrain != 11)
                            {
                                // Operação atômica simulada - corrida simples resulta em bordas irregulares naturais
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
                
                // Limitar o cálculo para não travar 100%
                if (expansionLevel % 10 == 0)
                {
                    float prog = 30f + Math.Min(65f, expansionLevel * 0.2f);
                    CallDeferred(nameof(UpdateProgressDeferred), prog, $"Mapeando Fronteiras (Nível {expansionLevel})...");
                }
            }

            CallDeferred(nameof(UpdateProgressDeferred), 100f, "Concluído!");
            GD.Print($"Semeou {placed} aldeias e gerou fronteiras em {expansionLevel} iterações.");
        }

        private void UpdateProgressDeferred(float progress, string status)
        {
            _progressBar.Value = progress;
            _labelStatus.Text = status;
        }

        private void RenderCurrentMapData()
        {
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

            _saveFeedback.Text = "Salvando...";
            _saveFeedback.Visible = true;

            string dir = GameManager.Instance.GetMapsDir();
            string path = dir + "/mapa_padrao.json";

            await System.Threading.Tasks.Task.Run(() =>
            {
                _currentMapData.ChunksList = new System.Collections.Generic.List<Data.ChunkData>(_currentMapData.Chunks.Values);
                string jsonString = System.Text.Json.JsonSerializer.Serialize(_currentMapData,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = false });
                System.IO.File.WriteAllText(path, jsonString);
            });

            _saveFeedback.Text = $"✅ Mapa Salvo! ({path})";
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
                    _currentMapData.ChunksList = new System.Collections.Generic.List<Data.ChunkData>(_currentMapData.Chunks.Values);
                    string jsonString = System.Text.Json.JsonSerializer.Serialize(_currentMapData,
                        new System.Text.Json.JsonSerializerOptions { WriteIndented = false });
                    System.IO.File.WriteAllText(path, jsonString);
                });

                _saveFeedback.Text = $"✅ Exportado para:\n{path}";
            }
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
                ClearWorld();
                string path = selectedPaths[0];

                _loadingPanel.Visible = true;
                _generationProgress = 0f;
                _labelStatus.Text = "Lendo arquivo do disco...";

                // Passo 1: Desserializar o JSON em background
                _currentMapData = await System.Threading.Tasks.Task.Run(() =>
                {
                    string jsonString = System.IO.File.ReadAllText(path);
                    var map = System.Text.Json.JsonSerializer.Deserialize<Data.MapData>(jsonString);
                    if (map != null)
                    {
                        map.Dimensions = new Vector2I(map.Width, map.Height);
                        if (map.ChunksList != null)
                        {
                            int total = map.ChunksList.Count;
                            for (int i = 0; i < total; i++)
                            {
                                var c = map.ChunksList[i];
                                c.ChunkPosition = new Vector2I(c.PosX, c.PosY);
                                map.Chunks[c.ChunkPosition] = c;
                                _generationProgress = (float)i / total * 60f;
                            }
                        }
                    }
                    return map;
                });

                if (_currentMapData == null)
                {
                    _saveFeedback.Text = "❌ Erro ao carregar mapa!";
                    _saveFeedback.Visible = true;
                    _loadingPanel.Visible = false;
                    return;
                }

                // Passo 2: Renderizar chunks 2D
                _labelStatus.Text = "Renderizando Terreno...";
                _generationProgress = 65f;
                RenderCurrentMapData();

                // Passo 3: Textura do Globo em background
                _labelStatus.Text = "Costurando Textura do Globo...";
                _generationProgress = 75f;
                _globeContainer.Visible = true;
                _worldContainer.Visible = false;

                Image globeImg = await System.Threading.Tasks.Task.Run(() => GenerateGlobeImage());
                var material = new StandardMaterial3D();
                material.AlbedoTexture = ImageTexture.CreateFromImage(globeImg);
                material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                _planetMesh.SetSurfaceOverrideMaterial(0, material);

                _generationProgress = 100f;
                _saveFeedback.Text = $"✅ Mapa Carregado! ({_currentMapData.Villages?.Count ?? 0} aldeias)";
                _saveFeedback.Visible = true;
                _loadingPanel.Visible = false;
            }
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
                _labelStatus.Text = "Costurando Textura do Globo...";
                
                Image globeImg = await Task.Run(() => GenerateGlobeImage());
                
                var material = new StandardMaterial3D();
                material.AlbedoTexture = ImageTexture.CreateFromImage(globeImg);
                material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                _planetMesh.SetSurfaceOverrideMaterial(0, material);

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
                            // Detectar se é borda olhando para os 4 vizinhos
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
                            _tacticalView.ZoomLevel = Mathf.Min(128, (int)(_tacticalView.ZoomLevel * 1.25f));
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
                            int nz = (int)(_tacticalView.ZoomLevel * 0.8f);
                            if (nz < 8)
                                ExitTacticalMode();
                            else
                                _tacticalView.ZoomLevel = nz;
                        }
                    }
                }
                else if (@event is InputEventScreenTouch touchEvent)
                {
                    if (touchEvent.Index == 0)
                    {
                        _isDraggingGlobe = touchEvent.Pressed;
                    }
                }
                else if (@event is InputEventMouseMotion mouseMotion)
                {
                    HandlePan(mouseMotion.Relative);
                }
                else if (@event is InputEventScreenDrag dragEvent)
                {
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
            _isTacticalMode = true;
            _camera3D.Current = false;
            _camera2D.Enabled = true;
            _camera2D.MakeCurrent();
            _camera2D.Zoom = new Vector2(1, 1);
            
            _tacticalView.Visible = true;
            _globeContainer.Visible = false;

            Vector3 localCenter = _planetMesh.ToLocal(new Vector3(0, 0, 1)).Normalized();
            
            float u = 0.5f + Mathf.Atan2(localCenter.X, -localCenter.Z) / (Mathf.Pi * 2.0f);
            float v = Mathf.Acos(localCenter.Y) / Mathf.Pi;
            
            if (_currentMapData != null)
            {
                int mapX = Mathf.Clamp((int)(u * _currentMapData.Width), 0, _currentMapData.Width - 1);
                int mapY = Mathf.Clamp((int)(v * _currentMapData.Height), 0, _currentMapData.Height - 1);
                
                _tacticalView.CenterX = mapX;
                _tacticalView.CenterY = mapY;
                _camera2D.Position = Vector2.Zero;
                _tacticalView.QueueRedraw();
            }
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
