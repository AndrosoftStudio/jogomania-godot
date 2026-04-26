using Godot;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using Jogomania.Data;
using Jogomania.Map;
using Jogomania.ECS;

namespace Jogomania.Core
{
	public partial class Game : Node2D
	{
		private Label _labelStatus;
		private Camera2D _camera2D;
		private TacticalView _tacticalView;
		private bool _isTacticalMode = false;

		private MapData _loadedMapData;
		private string _currentSaveSlotPath;
		
		private Node3D _globeContainer;
		private MeshInstance3D _planetMesh;
		private Camera3D _camera3D;
		private Image _globeImage;
		private ImageTexture _globeTexture;
		
		private bool _isDraggingGlobe = false;
		private bool _wasDragging = false; // Detectar click vs drag
		private float _globeScaleX;
		private float _globeScaleY;

		private TerritorySystem _territorySystem;
		private VillageData _playerCapital;
		private bool _isGameStarted = false;
		private bool _showVillageNames = false;
		private Button _btnToggleNames;

		public override void _Ready()
		{
			_globeContainer = GetNode<Node3D>("GlobeContainer");
			_planetMesh = GetNode<MeshInstance3D>("GlobeContainer/PlanetMesh");
			_camera3D = GetNode<Camera3D>("GlobeContainer/Camera3D");
			_camera3D.Current = true;

			_camera2D = new Camera2D();
			_camera2D.Enabled = false;
			AddChild(_camera2D);

			_tacticalView = new TacticalView();
			_tacticalView.Visible = false;
			AddChild(_tacticalView);

			_labelStatus = GetNode<Label>("CanvasLayer/HUD/TopPanel/HBoxContainer/LabelStatus");
			
			_territorySystem = new TerritorySystem();
			AddChild(_territorySystem);

			SetupSpaceEnvironment();

			// Adicionar colisor para o Raycast
			var staticBody = new StaticBody3D();
			var collision = new CollisionShape3D();
			var shape = new SphereShape3D();
			shape.Radius = 1.0f; 
			collision.Shape = shape;
			staticBody.AddChild(collision);
			_planetMesh.AddChild(staticBody);

			InitializeMatch();

			// Botão mobile para nomes de aldeias
			_btnToggleNames = new Button();
			_btnToggleNames.Text = "🏘 Nomes";
			_btnToggleNames.CustomMinimumSize = new Vector2(90, 36);
			_btnToggleNames.ToggleMode = true;
			_btnToggleNames.Toggled += (on) => ToggleVillageNames(on);

			var topBar = GetNodeOrNull<HBoxContainer>("CanvasLayer/HUD/TopPanel/HBoxContainer");
			if (topBar != null)
				topBar.AddChild(_btnToggleNames);
		}

		private void SetupSpaceEnvironment()
		{
			// Configurar Fundo Espacial Escuro
			var env = new Godot.Environment();
			env.BackgroundMode = Godot.Environment.BGMode.Color;
			env.BackgroundColor = new Color(0.01f, 0.01f, 0.03f, 1.0f); // Azul super escuro
			_camera3D.Environment = env;

			// Instanciar Atmosfera Brilhante
			var atmosMesh = new MeshInstance3D();
			var sphere = new SphereMesh();
			sphere.Radius = 1.05f; // Levemente maior que o planeta (1.0)
			sphere.Height = 2.10f;
			atmosMesh.Mesh = sphere;

			var atmosMat = new ShaderMaterial();
			var shader = GD.Load<Shader>("res://Shaders/Atmosphere.gdshader");
			if (shader != null)
			{
				atmosMat.Shader = shader;
				// Configurações padrão do shader
				atmosMat.SetShaderParameter("atmosphere_color", new Color(0.3f, 0.6f, 1.0f, 1.0f));
				atmosMat.SetShaderParameter("falloff", 3.0f);
				atmosMat.SetShaderParameter("intensity", 1.2f);
			}
			atmosMesh.MaterialOverride = atmosMat;

			_globeContainer.AddChild(atmosMesh);
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

			// Calcula qual ponto do mapa está no centro da câmera 3D
			Vector3 localCenter = _planetMesh.ToLocal(new Vector3(0, 0, 1)).Normalized();
			
			float u = 0.5f + Mathf.Atan2(localCenter.X, -localCenter.Z) / (Mathf.Pi * 2.0f);
			float v = Mathf.Acos(localCenter.Y) / Mathf.Pi;
			
			int mapCX = Mathf.Clamp((int)(u * _loadedMapData.Width),  0, _loadedMapData.Width  - 1);
			int mapCY = Mathf.Clamp((int)(v * _loadedMapData.Height), 0, _loadedMapData.Height - 1);

			// CenterX/CenterY é a âncora: tile (CenterX, CenterY) fica em world (0, 0).
			// A câmera começa em world (0, 0) => exibimos exatamente o tile central.
			_tacticalView.CenterX = mapCX;
			_tacticalView.CenterY = mapCY;
			_tacticalView.ZoomLevel = 32;
			_tacticalView.ShowVillageNames = _showVillageNames;
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
			_camera3D.Position = new Vector3(0, 0, 1.4f); // Zoom de saída: razoável, não no buraco
		}

		private void ToggleVillageNames(bool on)
		{
			_showVillageNames = on;
			_tacticalView.ShowVillageNames = on;
		}

		private async void InitializeMatch()
		{
			_currentSaveSlotPath = GameManager.Instance.GetPartidasDir() + "/Slot_1";
			string currentMatchFile = _currentSaveSlotPath + "/save_atual.json";

			if (File.Exists(currentMatchFile))
			{
				_labelStatus.Text = "Carregando Mundo...";
				
				_loadedMapData = await Task.Run(() => 
				{
					string jsonString = File.ReadAllText(currentMatchFile);
					var map = JsonSerializer.Deserialize<MapData>(jsonString);
					if (map != null)
					{
						map.Dimensions = new Vector2I(map.Width, map.Height);
						if (map.ChunksList != null)
						{
							foreach (var c in map.ChunksList)
							{
								c.ChunkPosition = new Vector2I(c.PosX, c.PosY);
								map.Chunks[c.ChunkPosition] = c;
							}
						}
					}
					return map;
				});

				if (_loadedMapData != null)
				{
					RenderCurrentMapData();
					_labelStatus.Text = "Pronto para Desembarcar!";
					SpawnInitialCaravan();
				}
			}
			else
			{
				_labelStatus.Text = "NENHUM MAPA ENCONTRADO!";
				GD.PrintErr("Arquivo da partida não encontrado. O GameManager falhou na cópia?");
			}
		}

		private async void RenderCurrentMapData()
		{
			_labelStatus.Text = "Costurando Globo 3D...";
			_globeImage = await Task.Run(() => GenerateGlobeImage());
			
			_globeTexture = ImageTexture.CreateFromImage(_globeImage);
			var material = new StandardMaterial3D();
			material.AlbedoTexture = _globeTexture;
			material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
			material.TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest; // Borda nítida estilo Unciv!
			_planetMesh.SetSurfaceOverrideMaterial(0, material);
			
			_tacticalView.MapData = _loadedMapData;
		}

		private Image GenerateGlobeImage()
		{
			int mapW = _loadedMapData.Dimensions.X;
			int mapH = _loadedMapData.Dimensions.Y;
			
			int globeW = System.Math.Min(mapW, 2048);
			int globeH = System.Math.Min(mapH, 1024);
			
			_globeScaleX = (float)globeW / mapW;
			_globeScaleY = (float)globeH / mapH;

			Image img = Image.CreateEmpty(globeW, globeH, false, Image.Format.Rgba8);

			for (int y = 0; y < globeH; y++)
			{
				for (int x = 0; x < globeW; x++)
				{
					int realX = (int)(x / _globeScaleX);
					int realY = (int)(y / _globeScaleY);

					EvaluateAndPaintPixelDirect(realX, realY, x, y, img);
				}
			}
			return img;
		}

		public void UpdateGlobePixel(int mapX, int mapY, byte territoryId)
		{
			if (_globeImage == null || _globeTexture == null) return;
			
			// Atualiza o pixel atual e os 4 vizinhos (para recalcular bordas que viraram interior)
			EvaluateAndPaintPixelDirect(mapX, mapY, (int)(mapX * _globeScaleX), (int)(mapY * _globeScaleY), _globeImage);
			EvaluateAndPaintPixelDirect(mapX + 1, mapY, (int)((mapX + 1) * _globeScaleX), (int)(mapY * _globeScaleY), _globeImage);
			EvaluateAndPaintPixelDirect(mapX - 1, mapY, (int)((mapX - 1) * _globeScaleX), (int)(mapY * _globeScaleY), _globeImage);
			EvaluateAndPaintPixelDirect(mapX, mapY + 1, (int)(mapX * _globeScaleX), (int)((mapY + 1) * _globeScaleY), _globeImage);
			EvaluateAndPaintPixelDirect(mapX, mapY - 1, (int)(mapX * _globeScaleX), (int)((mapY - 1) * _globeScaleY), _globeImage);
			
			_globeTexture.Update(_globeImage);
		}

		private byte GetOwnerAt(int x, int y)
		{
			if (x < 0 || x >= _loadedMapData.Width || y < 0 || y >= _loadedMapData.Height) return 0;
			int cx = x / ChunkData.CHUNK_SIZE;
			int cy = y / ChunkData.CHUNK_SIZE;
			if (_loadedMapData.Chunks.TryGetValue(new Vector2I(cx, cy), out ChunkData chunk))
			{
				return chunk.TerritoryMap[(y % ChunkData.CHUNK_SIZE) * ChunkData.CHUNK_SIZE + (x % ChunkData.CHUNK_SIZE)];
			}
			return 0;
		}

		private void EvaluateAndPaintPixelDirect(int mapX, int mapY, int globeX, int globeY, Image img)
		{
			if (globeX < 0 || globeX >= img.GetWidth() || globeY < 0 || globeY >= img.GetHeight()) return;
			if (mapX < 0 || mapX >= _loadedMapData.Width || mapY < 0 || mapY >= _loadedMapData.Height) return;

			int chunkX = mapX / ChunkData.CHUNK_SIZE;
			int chunkY = mapY / ChunkData.CHUNK_SIZE;
			Vector2I chunkPos = new Vector2I(chunkX, chunkY);
			
			if (_loadedMapData.Chunks.TryGetValue(chunkPos, out ChunkData chunk))
			{
				int localX = mapX % ChunkData.CHUNK_SIZE;
				int localY = mapY % ChunkData.CHUNK_SIZE;
				byte terrainType = chunk.TerrainMap[localY * ChunkData.CHUNK_SIZE + localX];
				byte ownerId = chunk.TerritoryMap[localY * ChunkData.CHUNK_SIZE + localX];
				
				Color terrainColor = GetTerrainColor(terrainType);

				if (ownerId > 0 && ownerId != 255)
				{
					Color terrColor = GetTerritoryColor(ownerId);
					
					bool isBorder = false;
					int[] dx = { 1, -1, 0, 0 };
					int[] dy = { 0, 0, 1, -1 };
					for (int i=0; i<4; i++)
					{
						byte neighborOwner = GetOwnerAt(mapX + dx[i], mapY + dy[i]);
						// Se o vizinho for oceano (owner == 0 e terreno de água), consideramos borda também?
						// Age of History costuma desenhar borda na costa marítima também.
						if (neighborOwner != ownerId && neighborOwner != 255) 
						{
							isBorder = true;
							break;
						}
					}

					if (isBorder)
					{
						// Linha Cinza Fina para Fronteiras
						img.SetPixel(globeX, globeY, new Color(0.2f, 0.2f, 0.2f, 1.0f));
					}
					else
					{
						if (ownerId == 1) 
						{
							// Azul translúcido para o Jogador
							img.SetPixel(globeX, globeY, terrainColor.Lerp(terrColor, 0.35f));
						}
						else
						{
							// Apenas o terreno natural para outras aldeias/nações não conquistadas
							img.SetPixel(globeX, globeY, terrainColor);
						}
					}
				}
				else if (ownerId == 255)
				{
					img.SetPixel(globeX, globeY, new Color(1.0f, 1.0f, 0.0f, 1.0f)); // Aldeia Neutra
				}
				else
				{
					img.SetPixel(globeX, globeY, terrainColor);
				}
			}
			else
			{
				img.SetPixel(globeX, globeY, new Color(0,0,0));
			}
		}

		public static Color GetTerrainColor(byte type)
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

		public static Color GetTerritoryColor(byte ownerId)
		{
			if (ownerId == 255) return new Color(1.0f, 1.0f, 0.0f, 1.0f); // Amarelo (Aldeia Neutra)
			if (ownerId == 1) return new Color(0.2f, 0.4f, 0.9f, 1.0f); // Azul Player
			if (ownerId == 2) return new Color(0.9f, 0.2f, 0.2f, 1.0f); // Vermelho Inimigo
			return new Color(1, 1, 1, 1.0f);
		}

		public override void _UnhandledInput(InputEvent @event)
		{
			// Tecla Tab — mostrar/ocultar nomes
			if (@event is InputEventKey key && key.Pressed && !key.Echo)
			{
				if (key.Keycode == Key.Tab)
				{
					_showVillageNames = !_showVillageNames;
					_tacticalView.ShowVillageNames = _showVillageNames;
					if (_btnToggleNames != null) _btnToggleNames.ButtonPressed = _showVillageNames;
					return;
				}
				if (key.Keycode == Key.Escape && _isTacticalMode)
				{
					ExitTacticalMode();
					return;
				}
			}

			if (@event is InputEventMouseButton mouseBtn)
			{
				if (mouseBtn.ButtonIndex == MouseButton.Left)
				{
					if (mouseBtn.Pressed)
					{
						_isDraggingGlobe = true;
						_wasDragging = false;
					}
					else
					{
						_isDraggingGlobe = false;
						if (!_wasDragging && !_isGameStarted && !_isTacticalMode)
						{
							SelectStartingVillage(mouseBtn.Position);
						}
						_wasDragging = false;
					}
				}
				
				if (mouseBtn.ButtonIndex == MouseButton.WheelUp && mouseBtn.Pressed)
				{
					if (!_isTacticalMode)
					{
						_camera3D.Position = new Vector3(0, 0, Mathf.Max(1.02f, _camera3D.Position.Z - 0.15f));
						if (_camera3D.Position.Z <= 1.08f)
							EnterTacticalMode();
					}
					else
					{
						// Zoom IN: aumenta ZoomLevel dos tiles
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
						// Zoom OUT: diminui ZoomLevel dos tiles; ao chegar em 8 volta ao globo
						int newZoom = (int)(_tacticalView.ZoomLevel * 0.8f);
						if (newZoom < 8)
							ExitTacticalMode();
						else
							_tacticalView.ZoomLevel = newZoom;
					}
				}
			}
				else if (@event is InputEventScreenTouch touchEvent)
				{
					if (touchEvent.Index == 0)
					{
						if (touchEvent.Pressed)
						{
							_isDraggingGlobe = true;
							_wasDragging = false;
						}
						else
						{
							_isDraggingGlobe = false;
							if (!_wasDragging && !_isGameStarted && !_isTacticalMode)
							{
								SelectStartingVillage(touchEvent.Position);
							}
							_wasDragging = false;
						}
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
			}

		private void HandlePan(Vector2 relative)
		{
			if (relative.LengthSquared() > 4.0f) _wasDragging = true; // Mais de ~2px = foi arrastado
			
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

		private void SelectStartingVillage(Vector2 screenPos)
		{
			var spaceState = _globeContainer.GetWorld3D().DirectSpaceState;
			var from = _camera3D.ProjectRayOrigin(screenPos);
			var to = from + _camera3D.ProjectRayNormal(screenPos) * 1000f;
			var query = PhysicsRayQueryParameters3D.Create(from, to);
			var result = spaceState.IntersectRay(query);
			
			if (result.Count > 0)
			{
				Vector3 hitPos = (Vector3)result["position"];
				Vector3 localPos = _planetMesh.ToLocal(hitPos).Normalized();
					
				float u = 0.5f + Mathf.Atan2(localPos.X, -localPos.Z) / (Mathf.Pi * 2.0f);
				float v = Mathf.Acos(localPos.Y) / Mathf.Pi;
					
				int mapX = Mathf.Clamp((int)(u * _loadedMapData.Width), 0, _loadedMapData.Width - 1);
				int mapY = Mathf.Clamp((int)(v * _loadedMapData.Height), 0, _loadedMapData.Height - 1);
				
				Data.VillageData closest = null;
				float minDist = float.MaxValue;
				
				foreach(var village in _loadedMapData.Villages)
				{
					float dist = (village.X - mapX)*(village.X - mapX) + (village.Y - mapY)*(village.Y - mapY);
					if (dist < minDist)
					{
						minDist = dist;
						closest = village;
					}
				}
				
				if (closest != null)
				{
					StartGameAs(closest);
				}
			}
		}

		private void SpawnInitialCaravan()
		{
			// Substituído pelo sistema de selecionar aldeia nativa
			_labelStatus.Text = "Selecione uma Aldeia Amarela clicando nela!";
		}

		private void StartGameAs(VillageData village)
		{
			_playerCapital = village;
			_playerCapital.OwnerId = 1;
			_isGameStarted = true;
			
			GetNode<Control>("CanvasLayer/HUD/BottomPanel").Visible = false;
			_labelStatus.Text = $"Você assumiu o controle de {village.Name}! Expansão Iniciada.";
			
			_territorySystem.StartSimulation(_loadedMapData, UpdateGlobePixel);
			_territorySystem.RegisterVillage(new Vector2I(village.X, village.Y), 1);
			
			GD.Print($"Jogo Iniciado na aldeia {village.Name}");
			
			// Colocar um Pin 3D na Aldeia (Removido o amarelão)
			// SpawnVillagePin(village);
		}

		private Vector3 GetSpherePosition(int mapX, int mapY)
		{
			float normX = (float)mapX / _loadedMapData.Width;
			float normY = (float)mapY / _loadedMapData.Height;
			
			float theta = normY * Mathf.Pi;
			float phi = normX * Mathf.Pi * 2.0f;
			
			float x = Mathf.Sin(theta) * Mathf.Cos(phi);
			float y = Mathf.Cos(theta);
			float z = Mathf.Sin(theta) * Mathf.Sin(phi);
			
			return new Vector3(x, y, z).Normalized();
		}

		private void SpawnVillagePin(VillageData village)
		{
			Vector3 pos3D = GetSpherePosition(village.X, village.Y);
			
			var pinSprite = new Sprite3D();
			pinSprite.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
			pinSprite.PixelSize = 0.003f;
			
			// Criar uma textura provisória colorida (Bandeira)
			Image img = Image.CreateEmpty(32, 32, false, Image.Format.Rgba8);
			img.Fill(new Color(1f, 1f, 0f, 1f)); // Ponto Amarelo Neon da Capital
			pinSprite.Texture = ImageTexture.CreateFromImage(img);
			
			// Elevar um pouco (2%) acima da superfície para não clipar no chão
			pinSprite.Position = pos3D * 1.02f;
			
			_planetMesh.AddChild(pinSprite);
		}

		private void OnBtnSettlePressed()
		{
			// Obsoleto. Ocultado da UI
		}

		private void SaveMatch()
		{
			if (_loadedMapData == null) return;
			string currentMatchFile = _currentSaveSlotPath + "/save_atual.json";
			_loadedMapData.ChunksList = new List<ChunkData>(_loadedMapData.Chunks.Values);
			string jsonString = JsonSerializer.Serialize(_loadedMapData, new JsonSerializerOptions { WriteIndented = false });
			File.WriteAllText(currentMatchFile, jsonString);
		}

		private void OnBtnBackPressed()
		{
			SaveMatch();
			GameManager.Instance?.GoToMainMenu();
		}
	}
}
