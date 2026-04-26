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

		// ── Pinch-to-Zoom (dois dedos) ──
		private bool _finger0Down = false;
		private bool _finger1Down = false;
		private Vector2 _finger0Pos = Vector2.Zero;
		private Vector2 _finger1Pos = Vector2.Zero;
		private float _pinchPrevDistance = 0f;

			// ── Câmera Orbital ──
		private float _cameraYaw = 0f;
		private float _cameraPitch = 0.3f;
		private float _cameraDistance = 2.5f;
		// ── Sol ──
		private float _sunAngle = 0f;
		private const float SUN_SPEED = 0.005f; // ~21 minutos para 1 dia completo
		private MeshInstance3D _sunMesh;
		private DirectionalLight3D _sunLight;
		private ShaderMaterial _starShaderMat;
		private float _starTime = 0f;

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
			var env = new Godot.Environment();
			env.BackgroundMode = Godot.Environment.BGMode.Color;
			env.BackgroundColor = new Color(0.0f, 0.0f, 0.0f, 1.0f);
			// Luz ambiente baixa para ter contraste dia/noite
			env.AmbientLightSource = Godot.Environment.AmbientSource.Color;
			env.AmbientLightColor = new Color(0.05f, 0.06f, 0.12f);
			env.AmbientLightEnergy = 0.3f;
			// Bloom para o sol e atmosfera
			env.GlowEnabled = true;
			env.GlowIntensity = 1.0f;
			env.GlowBloom = 0.5f;
			env.GlowHdrThreshold = 0.7f;
			env.GlowBlendMode = Godot.Environment.GlowBlendModeEnum.Additive;
			_camera3D.Environment = env;

			// Atmosfera
			var atmosMesh = new MeshInstance3D();
			var sphere = new SphereMesh(); sphere.Radius = 1.05f; sphere.Height = 2.10f;
			atmosMesh.Mesh = sphere;
			var atmosMat = new ShaderMaterial();
			var shader = GD.Load<Shader>("res://Shaders/Atmosphere.gdshader");
			if (shader != null)
			{
				atmosMat.Shader = shader;
				atmosMat.SetShaderParameter("atmosphere_color", new Color(0.3f, 0.6f, 1.0f, 1.0f));
				atmosMat.SetShaderParameter("falloff", 3.0f);
				atmosMat.SetShaderParameter("intensity", 1.2f);
			}
			atmosMesh.MaterialOverride = atmosMat;
			_globeContainer.AddChild(atmosMesh);

			// Estrelas — 4096x2048 com 1px por estrela para pontos minúsculos
			var starImg = Image.CreateEmpty(4096, 2048, false, Image.Format.Rgba8);
			starImg.Fill(new Color(0,0,0,0));
			var rng = new RandomNumberGenerator(); rng.Seed = 12345;
			for (int i = 0; i < 2500; i++)
			{
				float b = rng.Randf() * 0.7f + 0.3f;
				starImg.SetPixel(rng.RandiRange(0,4095), rng.RandiRange(0,2047), new Color(b,b,b,1));
			}
			var starTex = ImageTexture.CreateFromImage(starImg);
			var starMesh = new MeshInstance3D();
			var starSphere = new SphereMesh(); starSphere.Radius = 50f; starSphere.Height = 100f;
			starMesh.Mesh = starSphere;
			_starShaderMat = new ShaderMaterial();
			var starShader = GD.Load<Shader>("res://Shaders/Starfield.gdshader");
			if (starShader != null) { _starShaderMat.Shader = starShader; _starShaderMat.SetShaderParameter("star_texture", starTex); }
			starMesh.MaterialOverride = _starShaderMat;
			_globeContainer.AddChild(starMesh);

			// Sol decorativo
			_sunMesh = new MeshInstance3D();
			var sunSphere = new SphereMesh(); sunSphere.Radius = 0.4f; sunSphere.Height = 0.8f;
			_sunMesh.Mesh = sunSphere;
			var sunMat = new StandardMaterial3D();
			sunMat.AlbedoColor = new Color(1f, 0.95f, 0.3f);
			sunMat.EmissionEnabled = true;
			sunMat.Emission = new Color(1f, 0.9f, 0.2f);
			sunMat.EmissionEnergyMultiplier = 3f;
			sunMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
			_sunMesh.MaterialOverride = sunMat;
			_globeContainer.AddChild(_sunMesh);

			// Luz direcional (dia/noite)
			_sunLight = new DirectionalLight3D();
			_sunLight.LightColor = new Color(1f, 0.95f, 0.85f);
			_sunLight.LightEnergy = 1.8f;
			_globeContainer.AddChild(_sunLight);

			UpdateSunPosition();
			UpdateCameraOrbit();
		}

		public override void _Process(double delta)
		{
			if (!_isTacticalMode && _sunMesh != null)
			{
				_sunAngle += SUN_SPEED * (float)delta;
				if (_sunAngle > Mathf.Pi * 2f) _sunAngle -= Mathf.Pi * 2f;
				UpdateSunPosition();
				// Twinkle das estrelas
				_starTime += (float)delta;
				if (_starShaderMat != null) _starShaderMat.SetShaderParameter("time_offset", _starTime);
			}
		}

		private void UpdateCameraOrbit()
		{
			float x = _cameraDistance * Mathf.Cos(_cameraPitch) * Mathf.Sin(_cameraYaw);
			float y = _cameraDistance * Mathf.Sin(_cameraPitch);
			float z = _cameraDistance * Mathf.Cos(_cameraPitch) * Mathf.Cos(_cameraYaw);
			_camera3D.Position = new Vector3(x, y, z);
			if (_camera3D.Position.LengthSquared() > 0.001f)
				_camera3D.LookAt(Vector3.Zero, Vector3.Up);
		}

		private void UpdateSunPosition()
		{
			float sx = 8f * Mathf.Cos(_sunAngle);
			float sz = 8f * Mathf.Sin(_sunAngle);
			var sunPos = new Vector3(sx, 1.5f, sz);
			if (_sunMesh != null) _sunMesh.Position = sunPos;
			if (_sunLight != null)
			{
				_sunLight.Position = sunPos;
				if (sunPos.LengthSquared() > 0.001f) _sunLight.LookAt(Vector3.Zero, Vector3.Up);
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

			// Planeta fixo, câmera orbita - o ponto que a câmera vê é sua direção normalizada
			Vector3 camDir = _camera3D.Position.Normalized();
			float u = 0.5f + Mathf.Atan2(camDir.X, -camDir.Z) / (Mathf.Pi * 2.0f);
			float v = Mathf.Acos(Mathf.Clamp(camDir.Y, -1f, 1f)) / Mathf.Pi;
			int mapCX = Mathf.Clamp((int)(u * _loadedMapData.Width),  0, _loadedMapData.Width  - 1);
			int mapCY = Mathf.Clamp((int)(v * _loadedMapData.Height), 0, _loadedMapData.Height - 1);

			// CenterX/CenterY é a âncora: tile (CenterX, CenterY) fica em world (0, 0).
			// A câmera começa em world (0, 0) => exibimos exatamente o tile central.
			_tacticalView.CenterX = mapCX;
			_tacticalView.CenterY = mapCY;
			_tacticalView.ZoomLevel = 10; // Zoom inicial mais afastado
			_tacticalView.ShowVillageNames = _showVillageNames;
			_camera2D.Position = Vector2.Zero;
			_tacticalView.QueueRedraw();
		}

		private void ExitTacticalMode()
		{
			// Calcular ponto do mapa que está no centro da tela tática
			if (_loadedMapData != null)
			{
				float mapCenterX = _tacticalView.CenterX + _camera2D.Position.X / Mathf.Max(1, _tacticalView.ZoomLevel);
				float mapCenterY = _tacticalView.CenterY + _camera2D.Position.Y / Mathf.Max(1, _tacticalView.ZoomLevel);
				float normU = mapCenterX / _loadedMapData.Width;
				float normV = mapCenterY / _loadedMapData.Height;
				// Converter UV de mapa para yaw/pitch na esfera
				float phi = (normU - 0.5f) * Mathf.Pi * 2.0f;
				float theta = normV * Mathf.Pi;
				// phi = longitude (yaw), theta = colatitude (pitch a partir do polo)
				_cameraYaw = -phi; // negativo porque Atan2(X,-Z) inverte o sinal
				_cameraPitch = Mathf.Pi * 0.5f - theta; // pitch = 90° - colatitude
				_cameraPitch = Mathf.Clamp(_cameraPitch, -1.4f, 1.4f);
			}
			_isTacticalMode = false;
			_camera3D.Current = true;
			_camera2D.Enabled = false;
			_tacticalView.Visible = false;
			_globeContainer.Visible = true;
			_cameraDistance = 2.5f;
			UpdateCameraOrbit();
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
			material.Roughness = 1.0f;
			material.Metallic = 0.0f;
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
		// Tecla N — mostrar/ocultar nomes (Tab é reservado para navegação de UI)
		if (@event is InputEventKey key && key.Pressed && !key.Echo)
		{
			if (key.Keycode == Key.N)
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
				if (mouseBtn.Pressed) { _isDraggingGlobe = true; _wasDragging = false; }
				else
				{
					_isDraggingGlobe = false;
					if (!_wasDragging && !_isGameStarted && !_isTacticalMode)
						SelectStartingVillage(mouseBtn.Position);
					_wasDragging = false;
				}
			}
			else if (mouseBtn.ButtonIndex == MouseButton.WheelUp && mouseBtn.Pressed)
			{
				if (!_isTacticalMode)
				{
					_cameraDistance = Mathf.Max(1.08f, _cameraDistance - 0.15f);
					UpdateCameraOrbit();
					if (_cameraDistance <= 1.08f) EnterTacticalMode();
				}
				else
				{
					int oldZu = _tacticalView.ZoomLevel;
					int newZu = Mathf.Min(128, (int)(oldZu * 1.25f));
					if (newZu != oldZu) { _camera2D.Position *= (float)newZu / oldZu; _tacticalView.ZoomLevel = newZu; }
				}
			}
			else if (mouseBtn.ButtonIndex == MouseButton.WheelDown && mouseBtn.Pressed)
			{
				if (!_isTacticalMode)
				{
					_cameraDistance = Mathf.Min(5.0f, _cameraDistance + 0.15f);
					UpdateCameraOrbit();
				}
				else
				{
					int oldZd = _tacticalView.ZoomLevel;
					int nz = (int)(oldZd * 0.8f);
					if (nz < 8) ExitTacticalMode();
					else { _camera2D.Position *= (float)nz / oldZd; _tacticalView.ZoomLevel = nz; }
				}
			}
		}
		else if (@event is InputEventMouseMotion mouseMotion)
		{
			if (_isDraggingGlobe) HandlePan(mouseMotion.Relative);
		}
		else if (@event is InputEventScreenTouch touchEvent)
		{
			if (touchEvent.Index == 0)
			{
				_finger0Down = touchEvent.Pressed;
				_finger0Pos = touchEvent.Position;
				if (touchEvent.Pressed) { _isDraggingGlobe = true; _wasDragging = false; }
				else
				{
					if (!_finger1Down && !_wasDragging && !_isGameStarted && !_isTacticalMode)
						SelectStartingVillage(touchEvent.Position);
					_isDraggingGlobe = false;
					_wasDragging = false;
					_pinchPrevDistance = 0f;
				}
			}
			else if (touchEvent.Index == 1)
			{
				_finger1Down = touchEvent.Pressed;
				_finger1Pos = touchEvent.Position;
				if (touchEvent.Pressed)
				{
					_pinchPrevDistance = _finger0Pos.DistanceTo(_finger1Pos);
					_isDraggingGlobe = false;
					_wasDragging = true;
				}
				else _pinchPrevDistance = 0f;
			}
		}
		else if (@event is InputEventScreenDrag dragEvent)
		{
			if (dragEvent.Index == 0) _finger0Pos = dragEvent.Position;
			else if (dragEvent.Index == 1) _finger1Pos = dragEvent.Position;
			if (_finger0Down && _finger1Down) HandlePinch();
			else HandlePan(dragEvent.Relative);
		}
	}

	private void HandlePan(Vector2 relative)
	{
		if (relative.LengthSquared() > 4.0f) _wasDragging = true;
		if (!_isTacticalMode)
		{
			// Câmera orbita o planeta — planeta fica fixo (dia/noite independente)
			_cameraYaw   -= relative.X * 0.005f;
			_cameraPitch  = Mathf.Clamp(_cameraPitch + relative.Y * 0.005f, -1.4f, 1.4f);
			UpdateCameraOrbit();
		}
		else
		{
			_camera2D.Position -= relative * (_camera2D.Zoom.X > 0 ? 1.0f / _camera2D.Zoom.X : 1.0f);
		}
	}

	/// <summary>Processa o gesto de pinça (dois dedos) para dar zoom.</summary>
	private void HandlePinch()
	{
		float currentDist = _finger0Pos.DistanceTo(_finger1Pos);
		if (_pinchPrevDistance <= 0f) { _pinchPrevDistance = currentDist; return; }
		float delta = currentDist - _pinchPrevDistance;
		_pinchPrevDistance = currentDist;
		if (Mathf.Abs(delta) < 2f) return;
		if (!_isTacticalMode)
		{
			_cameraDistance = Mathf.Clamp(_cameraDistance - delta * 0.005f, 1.08f, 5.0f);
			UpdateCameraOrbit();
			if (_cameraDistance <= 1.08f) EnterTacticalMode();
		}
		else
		{
			float zf = 1f + delta * 0.008f;
			int nz = (int)(_tacticalView.ZoomLevel * zf);
			if (nz < 8) ExitTacticalMode();
			else _tacticalView.ZoomLevel = Mathf.Clamp(nz, 8, 128);
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
