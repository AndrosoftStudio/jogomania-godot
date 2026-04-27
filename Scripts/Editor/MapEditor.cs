using Godot;
using System;
using System.Text.Json;
using System.IO;
using System.Collections.Generic;
using System.Threading;
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
        private OptionButton _bodySelector;
        private ConfirmationDialog _overwriteDialog;
        private string _pendingSavePath;
        
        private Node3D _globeContainer;
        private MeshInstance3D _planetMesh;
        private ShaderMaterial _editorSunMaterial;
        private Node3D _editorSolarProminenceRoot;
        private ShaderMaterial _editorSolarProminenceMat;

        private Control _loadingPanel;
        private ProgressBar _progressBar;
        private Label _labelStatus;
        private ProgressBar _moonProgressBar;
        private ProgressBar _sunProgressBar;
        private ProgressBar _riverProgressBar;
        private ProgressBar _resourceProgressBar;

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
        private const float GlobePickRadius = 1.0f;
        private const int CelestialTextureVersion = 3;
        private CelestialBodyView _selectedBody = CelestialBodyView.Planet;

        private enum CelestialBodyView
        {
            Planet = 0,
            Moon = 1,
            Sun = 2
        }

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
            CreateBodySelector();
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
            CreateMetadataProgressBars();

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

        private void CreateMetadataProgressBars()
        {
            var vbox = GetNode<VBoxContainer>("UILayer/LoadingPanel/VBoxContainer");
            _moonProgressBar = CreateNamedProgressBar(vbox, "Lua");
            _sunProgressBar = CreateNamedProgressBar(vbox, "Sol");
            _riverProgressBar = CreateNamedProgressBar(vbox, "Rios");
            _resourceProgressBar = CreateNamedProgressBar(vbox, "Recursos");
            SetMetadataBarsVisible(false);
        }

        private ProgressBar CreateNamedProgressBar(VBoxContainer parent, string label)
        {
            var row = new VBoxContainer { Name = $"Meta{label}Row", Visible = false };
            var text = new Label { Text = label, HorizontalAlignment = HorizontalAlignment.Center };
            var bar = new ProgressBar { MinValue = 0, MaxValue = 100, Value = 0 };
            row.AddChild(text);
            row.AddChild(bar);
            parent.AddChild(row);
            return bar;
        }

        private void SetMetadataBarsVisible(bool visible)
        {
            if (visible)
            {
                _moonProgressBar.Value = 0;
                _sunProgressBar.Value = 0;
                _riverProgressBar.Value = 0;
                _resourceProgressBar.Value = 0;
            }
            SetProgressBarRowVisible(_moonProgressBar, visible);
            SetProgressBarRowVisible(_sunProgressBar, visible);
            SetProgressBarRowVisible(_riverProgressBar, visible);
            SetProgressBarRowVisible(_resourceProgressBar, visible);
        }

        private void SetProgressBarRowVisible(ProgressBar bar, bool visible)
        {
            if (bar?.GetParent() is Control row)
                row.Visible = visible;
        }

        private void CreateBodySelector()
        {
            var panel = new PanelContainer
            {
                AnchorLeft = 0.5f,
                AnchorRight = 0.5f,
                OffsetLeft = -120f,
                OffsetRight = 120f,
                OffsetTop = 12f,
                OffsetBottom = 52f
            };

            _bodySelector = new OptionButton
            {
                CustomMinimumSize = new Vector2(220, 34)
            };
            _bodySelector.AddItem("Planeta", (int)CelestialBodyView.Planet);
            _bodySelector.AddItem("Lua", (int)CelestialBodyView.Moon);
            _bodySelector.AddItem("Sol", (int)CelestialBodyView.Sun);
            _bodySelector.Selected = 0;
            _bodySelector.ItemSelected += OnBodySelectorItemSelected;
            panel.AddChild(_bodySelector);
            GetNode<CanvasLayer>("UILayer").AddChild(panel);
        }

        private async void OnBodySelectorItemSelected(long index)
        {
            _selectedBody = (CelestialBodyView)_bodySelector.GetItemId((int)index);
            if (_currentMapData == null || !_globeContainer.Visible) return;
            _loadingPanel.Visible = true;
            _generationProgress = 0f;
            await RefreshEditorGlobe();
            _loadingPanel.Visible = false;
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
            if (_editorSunMaterial != null)
            {
                _editorSunMaterial.SetShaderParameter("time_offset", Time.GetTicksMsec() / 1000.0f);
            }
            if (_editorSolarProminenceRoot != null)
            {
                _editorSolarProminenceRoot.RotateY(0.0025f);
                float pulse = 0.72f + 0.28f * Mathf.Sin(Time.GetTicksMsec() / 420.0f);
                _editorSolarProminenceRoot.Scale = Vector3.One * (0.94f + pulse * 0.08f);
                _editorSolarProminenceMat?.SetShaderParameter("pulse", pulse);
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
            _editorSunMaterial = null;
            _editorSolarProminenceRoot?.QueueFree();
            _editorSolarProminenceRoot = null;
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
            _loadedImage.Convert(Image.Format.Rgba8);

            int width = targetWidth;
            int height = targetHeight;
            bool blackIsLand = _optionLandColor.Selected == 1;
            bool useHeightmap = _checkHeightmap.ButtonPressed;
            
            byte[] imgData = _loadedImage.GetData();
            int imageSeed = (int)GD.Randi();

            _loadingPanel.Visible = true;
            _generationProgress = 0f;
            _labelStatus.Text = "Mixando Biomas com Imagem...";

            _currentMapData = await Task.Run(() => GenerateImageMapData(width, height, imgData, blackIsLand, useHeightmap, imageSeed));

            _labelStatus.Text = "Gerando rios e recursos...";
            SetMetadataBarsVisible(true);
            await Task.Run(() => GenerateWorldMetadata(_currentMapData, width * 31 + height));
            SetMetadataBarsVisible(false);
            _labelStatus.Text = "Semeando Aldeias...";
            int vCount = (int)_villageCountInput.Value;
            await Task.Run(() => ScatterVillages(_currentMapData, vCount));

            RenderCurrentMapData();
            await RefreshEditorGlobe();

            _loadingPanel.Visible = false;
        }

        private MapData GenerateImageMapData(int width, int height, byte[] imgData, bool blackIsLand, bool useHeightmap, int seed)
        {
            MapData map = new MapData();
            map.Dimensions = new Vector2I(width, height);
            map.Width = width;
            map.Height = height;
            map.MapName = "Mundo Por Imagem Mixado";

            float dynamicFreq = 2.0f / width; 
            
            int chunkCols = Mathf.CeilToInt((float)width / ChunkData.CHUNK_SIZE);
            int chunkRows = Mathf.CeilToInt((float)height / ChunkData.CHUNK_SIZE);
            ChunkData[] chunks = new ChunkData[chunkCols * chunkRows];
            long totalPixels = (long)width * height;
            long processed = 0;

            System.Threading.Tasks.Parallel.For(0, chunkRows, chunkY =>
            {
                var noiseElev = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Seed = seed, Frequency = dynamicFreq, FractalType = FastNoiseLite.FractalTypeEnum.Fbm, FractalOctaves = 5 };
                var noiseMoist = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Seed = seed + 100, Frequency = dynamicFreq * 1.5f, FractalType = FastNoiseLite.FractalTypeEnum.Fbm };
                var noiseTemp = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Seed = seed + 200, Frequency = dynamicFreq * 0.8f };

                for (int chunkX = 0; chunkX < chunkCols; chunkX++)
                {
                    Vector2I chunkPos = new Vector2I(chunkX, chunkY);
                    ChunkData chunk = new ChunkData(chunkPos);
                    int startX = chunkX * ChunkData.CHUNK_SIZE;
                    int startY = chunkY * ChunkData.CHUNK_SIZE;
                    int endX = Mathf.Min(startX + ChunkData.CHUNK_SIZE, width);
                    int endY = Mathf.Min(startY + ChunkData.CHUNK_SIZE, height);

                    for (int y = startY; y < endY; y++)
                    {
                        float lat = ((float)y / height) * Mathf.Pi - (Mathf.Pi / 2.0f);
                        float baseTemp = 1.0f - (Mathf.Abs(lat) / (Mathf.Pi / 2.0f)) * 2.0f;

                        for (int x = startX; x < endX; x++)
                        {
                            float pVal = GetRgbaValue(imgData, x, y, width);
                            bool isLandFromImage = blackIsLand ? pVal < 0.5f : pVal > 0.5f;

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
                                    e = (rawH - 0.5f) * 2.0f;
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

                            int localX = x - startX;
                            int localY = y - startY;
                            chunk.TerrainMap[localY * ChunkData.CHUNK_SIZE + localX] = terrain;
                        }
                    }

                    chunks[chunkY * chunkCols + chunkX] = chunk;
                    long chunkPixels = (long)(endX - startX) * (endY - startY);
                    long done = Interlocked.Add(ref processed, chunkPixels);
                    _generationProgress = ((float)done / totalPixels) * 100f;
                }
            });

            for (int i = 0; i < chunks.Length; i++)
                map.Chunks[chunks[i].ChunkPosition] = chunks[i];

            _generationProgress = 100f;
            return map;
        }

        private static float GetRgbaValue(byte[] rgbaData, int x, int y, int width)
        {
            int index = ((y * width) + x) * 4;
            if (rgbaData == null || index + 2 >= rgbaData.Length) return 0f;
            byte max = Math.Max(rgbaData[index], Math.Max(rgbaData[index + 1], rgbaData[index + 2]));
            return max / 255f;
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

            _labelStatus.Text = "Gerando rios e recursos...";
            SetMetadataBarsVisible(true);
            await Task.Run(() => GenerateWorldMetadata(_currentMapData, seed));
            SetMetadataBarsVisible(false);
            _labelStatus.Text = "Semeando Aldeias...";
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

            int chunkCols = Mathf.CeilToInt((float)targetWidth / ChunkData.CHUNK_SIZE);
            int chunkRows = Mathf.CeilToInt((float)targetHeight / ChunkData.CHUNK_SIZE);
            ChunkData[] chunks = new ChunkData[chunkCols * chunkRows];
            long totalPixels = (long)targetWidth * targetHeight;
            long processed = 0;

            System.Threading.Tasks.Parallel.For(0, chunkRows, chunkY =>
            {
                var noiseElev = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Seed = seed, Frequency = dynamicFreq, FractalType = FastNoiseLite.FractalTypeEnum.Fbm, FractalOctaves = 5 };
                var noiseMoist = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Seed = seed + 100, Frequency = dynamicFreq * 1.5f, FractalType = FastNoiseLite.FractalTypeEnum.Fbm };
                var noiseTemp = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Seed = seed + 200, Frequency = dynamicFreq * 0.8f };

                for (int chunkX = 0; chunkX < chunkCols; chunkX++)
                {
                    Vector2I chunkPos = new Vector2I(chunkX, chunkY);
                    ChunkData chunk = new ChunkData(chunkPos);
                    int startX = chunkX * ChunkData.CHUNK_SIZE;
                    int startY = chunkY * ChunkData.CHUNK_SIZE;
                    int endX = Mathf.Min(startX + ChunkData.CHUNK_SIZE, targetWidth);
                    int endY = Mathf.Min(startY + ChunkData.CHUNK_SIZE, targetHeight);

                    for (int y = startY; y < endY; y++)
                    {
                        float lat = ((float)y / targetHeight) * Mathf.Pi - (Mathf.Pi / 2.0f);
                        float baseTemp = 1.0f - (Mathf.Abs(lat) / (Mathf.Pi / 2.0f)) * 2.0f;

                        for (int x = startX; x < endX; x++)
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
                            int localX = x - startX;
                            int localY = y - startY;
                            chunk.TerrainMap[localY * ChunkData.CHUNK_SIZE + localX] = terrain;
                        }
                    }

                    chunks[chunkY * chunkCols + chunkX] = chunk;
                    long chunkPixels = (long)(endX - startX) * (endY - startY);
                    long done = Interlocked.Add(ref processed, chunkPixels);
                    _generationProgress = ((float)done / totalPixels) * 100f;
                }
            });

            for (int i = 0; i < chunks.Length; i++)
                map.Chunks[chunks[i].ChunkPosition] = chunks[i];

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
            CallDeferred(nameof(UpdateProgressDeferred), 0f, "Gerando textura da Lua...");
            GenerateMoonTextureForMap(map, seed);
            CallDeferred(nameof(UpdateMetadataProgressDeferred), "moon", 100f);
            CallDeferred(nameof(UpdateProgressDeferred), 20f, "Gerando textura do Sol...");
            GenerateSunTextureForMap(map, seed + 577);
            CallDeferred(nameof(UpdateMetadataProgressDeferred), "sun", 100f);
            map.CelestialTextureVersion = CelestialTextureVersion;
            CallDeferred(nameof(UpdateProgressDeferred), 40f, "Gerando rios...");
            GenerateRivers(map, seed + 991);
            CallDeferred(nameof(UpdateMetadataProgressDeferred), "river", 100f);
            CallDeferred(nameof(UpdateProgressDeferred), 75f, "Distribuindo recursos...");
            GenerateResourceRegions(map, seed + 1997);
            CallDeferred(nameof(UpdateMetadataProgressDeferred), "resource", 100f);
            CallDeferred(nameof(UpdateProgressDeferred), 100f, "Metadados concluidos!");
        }

        private void EnsureWorldMetadata(MapData map, int seed)
        {
            GameManager.RehydrateMap(map);
            bool regenerateCelestial = map.CelestialTextureVersion != CelestialTextureVersion;
            if (regenerateCelestial || map.MoonTextureData == null || map.MoonTextureWidth <= 0 || map.MoonTextureHeight <= 0)
                GenerateMoonTextureForMap(map, seed);
            if (regenerateCelestial || map.SunTextureData == null || map.SunTextureWidth <= 0 || map.SunTextureHeight <= 0)
                GenerateSunTextureForMap(map, seed + 577);
            if (regenerateCelestial)
                map.CelestialTextureVersion = CelestialTextureVersion;
            if (map.Rivers == null || map.Rivers.Count == 0)
                GenerateRivers(map, seed + 991);
            if (map.ResourceRegions == null || map.ResourceRegions.Count == 0)
                GenerateResourceRegions(map, seed + 1997);
        }

        private void GenerateMoonTextureForMap(MapData map, int seed)
        {
            int width = Mathf.Max(64, map.Width);
            int height = Mathf.Max(32, map.Height);
            Image img = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
            var rng = new RandomNumberGenerator { Seed = (ulong)(uint)seed };
            var maria = CreateMoonMaria(seed);
            var rayCenters = CreateMoonRayCenters(seed + 71);

            for (int y = 0; y < img.GetHeight(); y++)
            {
                for (int x = 0; x < img.GetWidth(); x++)
                {
                    float u = (float)x / width;
                    float v = (float)y / height;
                    float large = Fbm(u * 5.0f + seed * 0.001f, v * 2.7f - seed * 0.0007f, 5);
                    float fine = Fbm(u * 55.0f + 13.1f, v * 28.0f + 3.7f, 4);
                    float shade = 0.48f + large * 0.16f + fine * 0.08f;

                    foreach (MoonPatch patch in maria)
                    {
                        float dx = WrapUnitDistance(u - patch.Center.X);
                        float dy = (v - patch.Center.Y) / patch.Aspect;
                        float d = Mathf.Sqrt((dx * dx) / (patch.Radius * patch.Radius) + (dy * dy) / (patch.Radius * patch.Radius));
                        float mask = 1f - Mathf.SmoothStep(0.72f, 1.0f, d + (Fbm(u * 14f + patch.Seed, v * 8f - patch.Seed, 3) - 0.5f) * 0.36f);
                        shade = Mathf.Lerp(shade, shade * patch.Darkness, mask);
                    }

                    foreach (MoonPatch rays in rayCenters)
                    {
                        float dx = WrapUnitDistance(u - rays.Center.X);
                        float dy = v - rays.Center.Y;
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);
                        float angle = Mathf.Atan2(dy, dx);
                        float spokes = Mathf.Pow(Mathf.Abs(Mathf.Sin(angle * rays.Seed)), 20f);
                        float ray = spokes * Mathf.Exp(-dist / rays.Radius) * 0.12f;
                        shade += ray;
                    }

                    shade = Mathf.Clamp(shade, 0.10f, 0.86f);
                    img.SetPixel(x, y, new Color(shade, shade, shade * 0.97f, 1f));
                }
                if (y % 32 == 0)
                    CallDeferred(nameof(UpdateMetadataProgressDeferred), "moon", Mathf.Min(45f, (float)y / height * 45f));
            }

            int craterCount = Mathf.Clamp(width * height / 2400, 90, 720);
            for (int i = 0; i < craterCount; i++)
            {
                Vector2 center = new Vector2(rng.RandiRange(0, width - 1), rng.RandiRange(0, height - 1));
                float radius = rng.RandfRange(Mathf.Max(1.2f, width * 0.0010f), Mathf.Max(3.5f, width * 0.010f));
                for (int y = Mathf.Max(0, (int)(center.Y - radius)); y < Mathf.Min(height, (int)(center.Y + radius)); y++)
                {
                    for (int x = Mathf.Max(0, (int)(center.X - radius)); x < Mathf.Min(width, (int)(center.X + radius)); x++)
                    {
                        float d = center.DistanceTo(new Vector2(x, y)) / radius;
                        if (d > 1f) continue;
                        Color baseColor = img.GetPixel(x, y);
                        float bowl = Mathf.Lerp(0.78f, 1.02f, Mathf.SmoothStep(0.10f, 0.78f, d));
                        float rim = Mathf.SmoothStep(0.72f, 0.90f, d) * (1.0f - Mathf.SmoothStep(0.90f, 1.0f, d));
                        float shadow = Mathf.Clamp((new Vector2(x, y) - center).Normalized().Dot(new Vector2(-0.65f, 0.45f)), -1f, 1f) * (1f - d) * 0.06f;
                        float crater = bowl + rim * 0.16f + shadow;
                        float shade = Mathf.Clamp(baseColor.R * crater, 0.08f, 0.92f);
                        img.SetPixel(x, y, new Color(shade, shade, shade * 0.97f, 1f));
                    }
                }
                if (i % 12 == 0)
                    CallDeferred(nameof(UpdateMetadataProgressDeferred), "moon", 45f + (float)i / craterCount * 55f);
            }

            map.MoonTextureWidth = img.GetWidth();
            map.MoonTextureHeight = img.GetHeight();
            map.MoonTextureData = img.GetData();
        }

        private struct MoonPatch
        {
            public Vector2 Center;
            public float Radius;
            public float Aspect;
            public float Darkness;
            public float Seed;
        }

        private List<MoonPatch> CreateMoonMaria(int seed)
        {
            var patches = new List<MoonPatch>
            {
                new MoonPatch { Center = new Vector2(0.36f, 0.34f), Radius = 0.17f, Aspect = 0.72f, Darkness = 0.48f, Seed = 1.7f },
                new MoonPatch { Center = new Vector2(0.55f, 0.38f), Radius = 0.13f, Aspect = 0.82f, Darkness = 0.54f, Seed = 2.1f },
                new MoonPatch { Center = new Vector2(0.66f, 0.52f), Radius = 0.18f, Aspect = 0.90f, Darkness = 0.50f, Seed = 3.8f },
                new MoonPatch { Center = new Vector2(0.43f, 0.62f), Radius = 0.12f, Aspect = 0.75f, Darkness = 0.56f, Seed = 4.4f },
                new MoonPatch { Center = new Vector2(0.22f, 0.52f), Radius = 0.10f, Aspect = 1.20f, Darkness = 0.60f, Seed = 5.2f },
                new MoonPatch { Center = new Vector2(0.76f, 0.32f), Radius = 0.09f, Aspect = 0.92f, Darkness = 0.58f, Seed = 6.6f },
                new MoonPatch { Center = new Vector2(0.72f, 0.70f), Radius = 0.10f, Aspect = 0.82f, Darkness = 0.61f, Seed = 7.1f }
            };

            float offset = (Hash01(seed, seed * 3) - 0.5f) * 0.04f;
            for (int i = 0; i < patches.Count; i++)
            {
                MoonPatch patch = patches[i];
                patch.Center.X = Wrap01(patch.Center.X + offset);
                patch.Center.Y = Mathf.Clamp(patch.Center.Y - offset * 0.5f, 0.08f, 0.92f);
                patches[i] = patch;
            }
            return patches;
        }

        private List<MoonPatch> CreateMoonRayCenters(int seed)
        {
            return new List<MoonPatch>
            {
                new MoonPatch { Center = new Vector2(Wrap01(0.28f + Hash01(seed, 1) * 0.08f), 0.66f), Radius = 0.20f, Seed = 13f },
                new MoonPatch { Center = new Vector2(Wrap01(0.62f + Hash01(seed, 2) * 0.08f), 0.43f), Radius = 0.16f, Seed = 11f },
                new MoonPatch { Center = new Vector2(Wrap01(0.48f + Hash01(seed, 3) * 0.08f), 0.74f), Radius = 0.15f, Seed = 17f }
            };
        }

        private static float Wrap01(float value)
        {
            value -= Mathf.Floor(value);
            return value;
        }

        private static float WrapUnitDistance(float value)
        {
            value -= Mathf.Round(value);
            return value;
        }

        private static float Hash01(int x, int y)
        {
            int n = x * 374761393 + y * 668265263;
            n = (n ^ (n >> 13)) * 1274126177;
            n ^= n >> 16;
            return (n & 0x7fffffff) / 2147483647f;
        }

        private static float Hash01(float x, float y)
        {
            return Hash01(Mathf.FloorToInt(x * 4096f), Mathf.FloorToInt(y * 4096f));
        }

        private static float ValueNoise(float x, float y)
        {
            int xi = Mathf.FloorToInt(x);
            int yi = Mathf.FloorToInt(y);
            float xf = x - xi;
            float yf = y - yi;
            float u = xf * xf * (3f - 2f * xf);
            float v = yf * yf * (3f - 2f * yf);

            float a = Hash01(xi, yi);
            float b = Hash01(xi + 1, yi);
            float c = Hash01(xi, yi + 1);
            float d = Hash01(xi + 1, yi + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        private static float Fbm(float x, float y, int octaves)
        {
            float value = 0f;
            float amplitude = 0.5f;
            float total = 0f;
            for (int i = 0; i < octaves; i++)
            {
                value += ValueNoise(x, y) * amplitude;
                total += amplitude;
                x *= 2.03f;
                y *= 2.01f;
                amplitude *= 0.52f;
            }
            return total > 0f ? value / total : 0f;
        }

        private static float Cellular(float x, float y)
        {
            int xi = Mathf.FloorToInt(x);
            int yi = Mathf.FloorToInt(y);
            float best = 10f;

            for (int oy = -1; oy <= 1; oy++)
            {
                for (int ox = -1; ox <= 1; ox++)
                {
                    int cx = xi + ox;
                    int cy = yi + oy;
                    float px = cx + Hash01(cx, cy);
                    float py = cy + Hash01(cx + 19, cy - 37);
                    float dx = x - px;
                    float dy = y - py;
                    best = Mathf.Min(best, dx * dx + dy * dy);
                }
            }

            return Mathf.Clamp(Mathf.Sqrt(best), 0f, 1f);
        }

        private void GenerateSunTextureForMap(MapData map, int seed)
        {
            int width = Mathf.Max(64, map.Width);
            int height = Mathf.Max(32, map.Height);
            Image img = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float u = (float)x / width;
                    float v = (float)y / height;
                    float lat = (v - 0.5f) * Mathf.Pi;
                    float lon = u * Mathf.Tau;
                    float cosLat = Mathf.Cos(lat);
                    Vector3 n = new Vector3(cosLat * Mathf.Cos(lon), Mathf.Sin(lat), cosLat * Mathf.Sin(lon));

                    float flowA = Fbm(n.X * 7.5f + seed * 0.001f, n.Z * 7.5f + n.Y * 1.7f, 5);
                    float flowB = Fbm(n.Z * 11.0f - seed * 0.0007f, n.Y * 9.0f + n.X * 2.0f, 4);
                    float cells = Cellular(u * 72f + flowA * 3.5f + seed * 0.013f, v * 38f + flowB * 2.2f);
                    float granules = 1f - Mathf.SmoothStep(0.20f, 0.74f, cells);
                    float mottling = Fbm(u * 22f + flowB * 2.4f, v * 14f + flowA * 2.0f, 5);
                    float active = Mathf.Pow(Mathf.Clamp(Fbm(n.X * 4.0f + 21.7f, n.Y * 4.0f + n.Z * 1.3f, 4), 0f, 1f), 3.0f);
                    float heat = Mathf.Clamp(0.42f + granules * 0.24f + mottling * 0.25f + active * 0.22f, 0f, 1f);
                    float flare = Mathf.SmoothStep(0.74f, 0.94f, active + granules * 0.22f);

                    Color color = new Color(
                        1.0f,
                        0.29f + heat * 0.48f + flare * 0.12f,
                        0.035f + heat * 0.12f + flare * 0.07f,
                        1f
                    );
                    img.SetPixel(x, y, color);
                }
                if (y % 32 == 0)
                    CallDeferred(nameof(UpdateMetadataProgressDeferred), "sun", (float)y / height * 100f);
            }

            map.SunTextureWidth = img.GetWidth();
            map.SunTextureHeight = img.GetHeight();
            map.SunTextureData = img.GetData();
        }

        private void GenerateRivers(MapData map, int seed)
        {
            map.Rivers.Clear();
            var rng = new RandomNumberGenerator { Seed = (ulong)(uint)seed };
            var starts = new List<Vector2I>();
            var fallbackStarts = new List<Vector2I>();

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
                        else if (terrain >= (byte)TerrainType.Grassland && !PlanetMeshBuilder.IsOcean(terrain) && rng.Randf() < 0.006f)
                            fallbackStarts.Add(new Vector2I(mapX, mapY));
                    }
                }
            }

            if (starts.Count < 8)
                starts.AddRange(fallbackStarts);

            int maxRivers = Mathf.Clamp(map.Width * map.Height / (220 * 220), 6, 42);
            int attempts = Mathf.Max(starts.Count, maxRivers * 18);
            for (int i = 0; i < attempts && map.Rivers.Count < maxRivers; i++)
            {
                Vector2I start = starts.Count > 0
                    ? starts[(int)(rng.Randi() % (uint)starts.Count)]
                    : new Vector2I(rng.RandiRange(0, map.Width - 1), rng.RandiRange(0, map.Height - 1));
                RiverData river = TraceRiver(map, start, rng);
                if (river.Points.Count >= 8)
                {
                    map.Rivers.Add(river);
                    CarveRiverTerrain(map, river);
                }

                if (i % 10 == 0)
                {
                    float progress = 40f + Mathf.Min(30f, (float)i / attempts * 30f);
                    CallDeferred(nameof(UpdateProgressDeferred), progress, $"Gerando rios ({map.Rivers.Count}/{maxRivers})...");
                    CallDeferred(nameof(UpdateMetadataProgressDeferred), "river", (float)i / attempts * 100f);
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
            river.Points.Add(new MapPointData { X = current.X, Y = current.Y });

                byte terrain = PlanetMeshBuilder.GetTerrainAt(map, current.X, current.Y);
            if (PlanetMeshBuilder.IsOcean(terrain)) break;

                Vector2I best = current;
                float bestScore = float.MaxValue;
                for (int oy = -1; oy <= 1; oy++)
                {
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        if (ox == 0 && oy == 0) continue;
                        int nx = WrapMapX(current.X + ox, map.Width);
                        int ny = WrapMapY(current.Y + oy, map.Height);
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
                        int y = WrapMapY(point.Y + oy, map.Height);
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
            int initialAttempts = attempts;
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

                if (map.ResourceRegions.Count % 12 == 0)
                {
                    float progress = 75f + Mathf.Min(20f, (float)(initialAttempts - attempts) / initialAttempts * 20f);
                    CallDeferred(nameof(UpdateProgressDeferred), progress, $"Distribuindo recursos ({map.ResourceRegions.Count}/{target})...");
                    CallDeferred(nameof(UpdateMetadataProgressDeferred), "resource", (float)(initialAttempts - attempts) / initialAttempts * 100f);
                }
            }
        }

        private int WrapMapX(int x, int width)
        {
            if (width <= 0) return 0;
            int wrapped = x % width;
            return wrapped < 0 ? wrapped + width : wrapped;
        }

        private int WrapMapY(int y, int height)
        {
            if (height <= 0) return 0;
            int wrapped = y % height;
            return wrapped < 0 ? wrapped + height : wrapped;
        }

        private void ScatterVillages(MapData map, int count)
        {
            map.Villages.Clear();
            
            // UI Update: Buscando Terras
            CallDeferred(nameof(UpdateProgressDeferred), 0f, "Buscando terra firme...");
            
            CallDeferred(nameof(UpdateProgressDeferred), 10f, "Sorteando Capitais...");

            CallDeferred(nameof(UpdateProgressDeferred), 20f, "Fundando ImpÃ©rios...");

            int targetCount = Mathf.Max(0, count);
            int villageIdCounter = 1;
            var occupied = new HashSet<Vector2I>();
            var rng = new Random(unchecked(map.Width * 73856093 ^ map.Height * 19349663 ^ targetCount * 83492791));
            int attempts = Math.Max(targetCount * 80, 1000);

            while (map.Villages.Count < targetCount && attempts-- > 0)
            {
                Vector2I pos = new Vector2I(rng.Next(0, map.Width), rng.Next(0, map.Height));
                if (occupied.Contains(pos)) continue;
                if (!IsHabitableTerrain(PlanetMeshBuilder.GetTerrainAt(map, pos.X, pos.Y))) continue;
                PlaceVillage(map, pos, villageIdCounter++, occupied);
            }

            if (map.Villages.Count < targetCount)
            {
                foreach (var kvp in map.Chunks)
                {
                    int startX = kvp.Key.X * ChunkData.CHUNK_SIZE;
                    int startY = kvp.Key.Y * ChunkData.CHUNK_SIZE;
                    ChunkData chunk = kvp.Value;

                    for (int ly = 0; ly < ChunkData.CHUNK_SIZE && map.Villages.Count < targetCount; ly++)
                    {
                        for (int lx = 0; lx < ChunkData.CHUNK_SIZE && map.Villages.Count < targetCount; lx++)
                        {
                            int x = startX + lx;
                            int y = startY + ly;
                            if (x >= map.Width || y >= map.Height) continue;
                            Vector2I pos = new Vector2I(x, y);
                            if (occupied.Contains(pos)) continue;
                            if (!IsHabitableTerrain(chunk.TerrainMap[ly * ChunkData.CHUNK_SIZE + lx])) continue;
                            PlaceVillage(map, pos, villageIdCounter++, occupied);
                        }
                    }

                    if (map.Villages.Count >= targetCount) break;
                }
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
                        int nx = WrapMapX(curr.X + dx[i], map.Width);
                        int ny = WrapMapY(curr.Y + dy[i], map.Height);
                        
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
            GD.Print($"Semeou {map.Villages.Count} aldeias e gerou fronteiras em {expansionLevel} iteraÃ§Ãµes.");
        }

        private static bool IsHabitableTerrain(byte terrain)
        {
            return terrain >= 3 && terrain != (byte)TerrainType.Mountain && terrain != (byte)TerrainType.HighPeak;
        }

        private void PlaceVillage(MapData map, Vector2I pos, int villageId, HashSet<Vector2I> occupied)
        {
            occupied.Add(pos);
            map.Villages.Add(new VillageData
            {
                Id = villageId,
                Name = VillageNameGenerator.GenerateName(villageId * 7919 + 31337),
                X = pos.X,
                Y = pos.Y,
                OwnerId = villageId,
                Level = 1
            });

            int chunkX = pos.X / ChunkData.CHUNK_SIZE;
            int chunkY = pos.Y / ChunkData.CHUNK_SIZE;
            if (map.Chunks.TryGetValue(new Vector2I(chunkX, chunkY), out ChunkData chunk))
            {
                int lx = pos.X % ChunkData.CHUNK_SIZE;
                int ly = pos.Y % ChunkData.CHUNK_SIZE;
                chunk.TerritoryMap[ly * ChunkData.CHUNK_SIZE + lx] = (byte)(villageId % 255 == 0 ? 1 : villageId % 255);
            }
        }

        private void UpdateProgressDeferred(float progress, string status)
        {
            _progressBar.Value = progress;
            _labelStatus.Text = status;
        }

        private void UpdateMetadataProgressDeferred(string id, float progress)
        {
            ProgressBar bar = id switch
            {
                "moon" => _moonProgressBar,
                "sun" => _sunProgressBar,
                "river" => _riverProgressBar,
                "resource" => _resourceProgressBar,
                _ => null
            };

            if (bar != null)
                bar.Value = Mathf.Clamp(progress, 0f, 100f);
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

            Image globeImg = await Task.Run(() => GenerateSelectedBodyImage());
            _planetMesh.Mesh = _selectedBody == CelestialBodyView.Planet
                ? PlanetMeshBuilder.CreatePlanetMesh(_currentMapData, 128, 64)
                : new SphereMesh { Radius = 1.0f, Height = 2.0f, RadialSegments = 128, Rings = 64 };
            Material material = CreateSelectedBodyMaterial(globeImg);
            _planetMesh.SetSurfaceOverrideMaterial(0, material);
            UpdateEditorSolarProminenceVisibility();
            _tacticalView.MapData = _currentMapData;
        }

        private void UpdateEditorSolarProminenceVisibility()
        {
            if (_selectedBody == CelestialBodyView.Sun)
            {
                if (_editorSolarProminenceRoot == null)
                    CreateEditorSolarProminences();
                _editorSolarProminenceRoot.Visible = true;
            }
            else if (_editorSolarProminenceRoot != null)
            {
                _editorSolarProminenceRoot.Visible = false;
            }
        }

        private void CreateEditorSolarProminences()
        {
            _editorSolarProminenceRoot = new Node3D();
            _planetMesh.AddChild(_editorSolarProminenceRoot);

            _editorSolarProminenceMat = new ShaderMaterial();
            Shader shader = GD.Load<Shader>("res://Shaders/SolarProminence.gdshader");
            if (shader != null)
            {
                _editorSolarProminenceMat.Shader = shader;
                _editorSolarProminenceMat.SetShaderParameter("prominence_color", new Color(1f, 0.28f, 0.03f, 0.48f));
            }

            for (int i = 0; i < 8; i++)
            {
                var arc = new MeshInstance3D
                {
                    Mesh = CreateEditorSolarProminenceMesh(1.0f, i),
                    MaterialOverride = _editorSolarProminenceMat,
                    Rotation = new Vector3(i * 0.71f, i * 1.13f, i * 0.47f)
                };
                _editorSolarProminenceRoot.AddChild(arc);
            }
        }

        private Mesh CreateEditorSolarProminenceMesh(float radius, int seed)
        {
            var surface = new SurfaceTool();
            surface.Begin(Mesh.PrimitiveType.Triangles);
            int segments = 30;
            float arcWidth = radius * 0.0008f;
            float start = -0.70f;
            float end = 0.70f;
            float height = radius * (0.0065f + (seed % 4) * 0.0012f);

            for (int i = 0; i < segments; i++)
            {
                float t0 = (float)i / segments;
                float t1 = (float)(i + 1) / segments;
                Vector3 a0 = EditorSolarProminencePoint(radius, height, Mathf.Lerp(start, end, t0), t0);
                Vector3 a1 = EditorSolarProminencePoint(radius, height, Mathf.Lerp(start, end, t1), t1);
                Vector3 side = new Vector3(0, arcWidth, 0);

                surface.AddVertex(a0 - side);
                surface.AddVertex(a0 + side);
                surface.AddVertex(a1 - side);
                surface.AddVertex(a1 - side);
                surface.AddVertex(a0 + side);
                surface.AddVertex(a1 + side);
            }

            return surface.Commit();
        }

        private Vector3 EditorSolarProminencePoint(float radius, float height, float angle, float t)
        {
            float r = radius * 1.02f + Mathf.Sin(t * Mathf.Pi) * height;
            return new Vector3(Mathf.Sin(angle) * r, 0f, Mathf.Cos(angle) * r);
        }

        private Material CreateSelectedBodyMaterial(Image bodyImage)
        {
            if (_selectedBody == CelestialBodyView.Sun)
            {
                var sunMat = new ShaderMaterial();
                Shader sunShader = GD.Load<Shader>("res://Shaders/SunSurface.gdshader");
                if (sunShader != null)
                {
                    sunMat.Shader = sunShader;
                    sunMat.SetShaderParameter("sun_texture", ImageTexture.CreateFromImage(bodyImage));
                    sunMat.SetShaderParameter("time_offset", _generationProgress * 0.01f);
                    _editorSunMaterial = sunMat;
                    return sunMat;
                }
            }

            _editorSunMaterial = null;
            var material = new StandardMaterial3D();
            material.AlbedoTexture = ImageTexture.CreateFromImage(bodyImage);
            material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            material.TextureFilter = _selectedBody == CelestialBodyView.Planet
                ? BaseMaterial3D.TextureFilterEnum.Nearest
                : BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps;
            return material;
        }

        private Image GenerateSelectedBodyImage()
        {
            EnsureWorldMetadata(_currentMapData, (_currentMapData.MapName ?? "map").GetHashCode());
            return _selectedBody switch
            {
                CelestialBodyView.Moon => Image.CreateFromData(_currentMapData.MoonTextureWidth, _currentMapData.MoonTextureHeight, false, Image.Format.Rgba8, _currentMapData.MoonTextureData),
                CelestialBodyView.Sun => Image.CreateFromData(_currentMapData.SunTextureWidth, _currentMapData.SunTextureHeight, false, Image.Format.Rgba8, _currentMapData.SunTextureData),
                _ => GenerateGlobeImage()
            };
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
                                int nx = WrapMapX(realX + dx[i], _currentMapData.Width);
                                int ny = WrapMapY(realY + dy[i], _currentMapData.Height);
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
                                EnterTacticalMode(mouseBtn.Position);
                        }
                        else
                        {
                            _tacticalView.ZoomLevel = TacticalView.ClampZoom(_tacticalView.ZoomLevel * 1.25f);
                            _camera2D.Position = _tacticalView.ClampCameraPosition(_camera2D.Position);
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
                            if (nz < TacticalView.MinZoomLevel)
                                ExitTacticalMode();
                            else
                            {
                                _tacticalView.ZoomLevel = TacticalView.ClampZoom(nz);
                                _camera2D.Position = _tacticalView.ClampCameraPosition(_camera2D.Position);
                            }
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
                    {
                        _isDraggingGlobe = true;
                        HandlePan(dragEvent.Relative);
                    }
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
                _camera2D.Position = _tacticalView.ClampCameraPosition(_camera2D.Position);
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
                    EnterTacticalMode((_finger0Pos + _finger1Pos) * 0.5f);
            }
            else
            {
                // VisÃ£o TÃ¡tica 2D
                float zoomFactor = 1.0f + delta * 0.008f;
                float newZoom = _tacticalView.ZoomLevel * zoomFactor;
                if (newZoom < TacticalView.MinZoomLevel)
                    ExitTacticalMode();
                else
                {
                    _tacticalView.ZoomLevel = TacticalView.ClampZoom(newZoom);
                    _camera2D.Position = _tacticalView.ClampCameraPosition(_camera2D.Position);
                }
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

        private void EnterTacticalMode(Vector2? focusScreenPos = null)
        {
            if (_currentMapData == null) return;

            Vector2 screenPos = focusScreenPos ?? GetViewportRect().Size * 0.5f;
            Vector2I mapPoint = TryGetEditorMapPointFromScreen(screenPos, out Vector2I hitMap)
                ? hitMap
                : PlanetMeshBuilder.SphereDirectionToMap(_planetMesh.ToLocal(_camera3D.GlobalPosition).Normalized(), _currentMapData);

            _isTacticalMode = true;
            _camera3D.Current = false;
            _camera2D.Enabled = true;
            _camera2D.MakeCurrent();
            _camera2D.Zoom = new Vector2(1, 1);

            _tacticalView.MapData = _currentMapData; // Garante que o mapa estÃ¡ setado
            _tacticalView.FlipX = true;
            _tacticalView.FlipY = true;
            _tacticalView.ZoomLevel = 3.0f;
            _tacticalView.Visible = true;
            _globeContainer.Visible = false;

            _tacticalView.CenterX = mapPoint.X;
            _tacticalView.CenterY = mapPoint.Y;
            _camera2D.Position = Vector2.Zero;
            _tacticalView.QueueRedraw();
        }

        private bool TryGetEditorMapPointFromScreen(Vector2 screenPos, out Vector2I mapPoint)
        {
            mapPoint = Vector2I.Zero;
            if (_currentMapData == null || _planetMesh == null || _camera3D == null) return false;

            Vector3 rayOriginGlobal = _camera3D.ProjectRayOrigin(screenPos);
            Vector3 rayDirectionGlobal = _camera3D.ProjectRayNormal(screenPos).Normalized();
            Transform3D inverse = _planetMesh.GlobalTransform.AffineInverse();
            Vector3 origin = inverse * rayOriginGlobal;
            Vector3 direction = (inverse.Basis * rayDirectionGlobal).Normalized();

            float b = origin.Dot(direction);
            float c = origin.LengthSquared() - GlobePickRadius * GlobePickRadius;
            float discriminant = b * b - c;
            if (discriminant < 0f) return false;

            float sqrt = Mathf.Sqrt(discriminant);
            float distance = -b - sqrt;
            if (distance < 0f) distance = -b + sqrt;
            if (distance < 0f) return false;

            Vector3 localNormal = (origin + direction * distance).Normalized();
            mapPoint = PlanetMeshBuilder.SphereDirectionToMap(localNormal, _currentMapData);
            return true;
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
