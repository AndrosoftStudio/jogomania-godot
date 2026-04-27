using Godot;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Jogomania.Data;
using Jogomania.ECS;
using Jogomania.Map;

namespace Jogomania.Core
{
	public partial class Game : Node2D
	{
		private const float TacticalEntryDistance = 1.08f;
		private const float TacticalExitZoom = 1.90f;
		private const float TacticalInitialZoom = 3.0f;
		private const float MaxGlobeDistance = 10.5f;
		private const float GlobePickRadius = 1.0f;
		private const int CelestialTextureVersion = 3;
		private const float SunSpeed = 0.005f;

		private Label _labelStatus;
		private Camera2D _camera2D;
		private TacticalView _tacticalView;
		private bool _isTacticalMode;

		private MapData _loadedMapData;
		private string _currentSaveSlotPath;

		private Node3D _globeContainer;
		private MeshInstance3D _planetMesh;
		private Camera3D _camera3D;
		private Image _globeImage;
		private ImageTexture _globeTexture;
		private ImageTexture _oceanMaskTexture;
		private ShaderMaterial _planetShaderMat;
		private ShaderMaterial _atmosShaderMat;
		private MeshInstance3D _atmosMesh;
		private ShaderMaterial _cloudShaderMat;
		private MeshInstance3D _cloudMesh;

		private bool _isDraggingGlobe;
		private bool _wasDragging;
		private float _globeScaleX = 1f;
		private float _globeScaleY = 1f;

		private bool _finger0Down;
		private bool _finger1Down;
		private Vector2 _finger0Pos = Vector2.Zero;
		private Vector2 _finger1Pos = Vector2.Zero;
		private float _pinchPrevDistance;

		private float _cameraYaw;
		private float _cameraPitch = 0.3f;
		private float _cameraDistance = 2.5f;
		private float _sunAngle;
		private Vector3 _sunDirection = Vector3.Right;
		private MeshInstance3D _sunMesh;
		private ShaderMaterial _sunShaderMat;
		private Node3D _solarProminenceRoot;
		private ShaderMaterial _solarProminenceMat;
		private MeshInstance3D _moonMesh;
		private ShaderMaterial _moonShaderMat;
		private float _moonAngle;
		private DirectionalLight3D _sunLight;
		private ShaderMaterial _starShaderMat;
		private float _starTime;

		private TerritorySystem _territorySystem;
		private VillageData _playerCapital;
		private bool _isGameStarted;
		private bool _showVillageNames;
		private Button _btnToggleNames;

		public override void _Ready()
		{
			_globeContainer = GetNode<Node3D>("GlobeContainer");
			_planetMesh = GetNode<MeshInstance3D>("GlobeContainer/PlanetMesh");
			_camera3D = GetNode<Camera3D>("GlobeContainer/Camera3D");
			_camera3D.Current = true;

			_camera2D = new Camera2D { Enabled = false };
			AddChild(_camera2D);

			_tacticalView = new TacticalView { Visible = false };
			AddChild(_tacticalView);

			_labelStatus = GetNode<Label>("CanvasLayer/HUD/TopPanel/HBoxContainer/LabelStatus");
			_territorySystem = new TerritorySystem();
			AddChild(_territorySystem);

			SetupSpaceEnvironment();
			SetupPlanetCollider();
			InitializeMatch();
			SetupMobileNameButton();
		}

		private void SetupPlanetCollider()
		{
			var staticBody = new StaticBody3D();
			var collision = new CollisionShape3D();
			var shape = new SphereShape3D { Radius = GlobePickRadius };
			collision.Shape = shape;
			staticBody.AddChild(collision);
			_planetMesh.AddChild(staticBody);
		}

		private void SetupMobileNameButton()
		{
			_btnToggleNames = new Button
			{
				Text = "Nomes",
				CustomMinimumSize = new Vector2(90, 36),
				ToggleMode = true
			};
			_btnToggleNames.Toggled += ToggleVillageNames;

			var topBar = GetNodeOrNull<HBoxContainer>("CanvasLayer/HUD/TopPanel/HBoxContainer");
			topBar?.AddChild(_btnToggleNames);
		}

		private void SetupSpaceEnvironment()
		{
			var env = new Godot.Environment
			{
				BackgroundMode = Godot.Environment.BGMode.Color,
				BackgroundColor = Colors.Black,
				AmbientLightSource = Godot.Environment.AmbientSource.Color,
				AmbientLightColor = new Color(0.025f, 0.03f, 0.06f),
				AmbientLightEnergy = 0.12f,
				GlowEnabled = true,
				GlowIntensity = 1.1f,
				GlowBloom = 0.65f,
				GlowHdrThreshold = 0.65f,
				GlowBlendMode = Godot.Environment.GlowBlendModeEnum.Additive
			};
			_camera3D.Environment = env;

			_atmosMesh = new MeshInstance3D();
			var sphere = new SphereMesh { Radius = 1.105f, Height = 2.21f, RadialSegments = 128, Rings = 64 };
			_atmosMesh.Mesh = sphere;
			_atmosShaderMat = new ShaderMaterial();
			Shader atmosShader = GD.Load<Shader>("res://Shaders/Atmosphere.gdshader");
			if (atmosShader != null)
			{
				_atmosShaderMat.Shader = atmosShader;
				_atmosShaderMat.SetShaderParameter("atmosphere_color", new Color(0.34f, 0.62f, 1.0f, 1.0f));
				_atmosShaderMat.SetShaderParameter("twilight_color", new Color(1.0f, 0.45f, 0.16f, 1.0f));
				_atmosShaderMat.SetShaderParameter("falloff", 2.25f);
				_atmosShaderMat.SetShaderParameter("intensity", 0.85f);
			}
			_atmosMesh.MaterialOverride = _atmosShaderMat;
			_globeContainer.AddChild(_atmosMesh);

			_cloudMesh = new MeshInstance3D();
			_cloudMesh.Mesh = new SphereMesh { Radius = 1.072f, Height = 2.144f, RadialSegments = 128, Rings = 64 };
			_cloudShaderMat = new ShaderMaterial();
			Shader cloudShader = GD.Load<Shader>("res://Shaders/CloudLayer.gdshader");
			if (cloudShader != null)
			{
				_cloudShaderMat.Shader = cloudShader;
				_cloudShaderMat.SetShaderParameter("cloud_color", new Color(1f, 1f, 1f, 0.34f));
			}
			_cloudMesh.MaterialOverride = _cloudShaderMat;
			_globeContainer.AddChild(_cloudMesh);

			var starImg = Image.CreateEmpty(4096, 2048, false, Image.Format.Rgba8);
			starImg.Fill(new Color(0, 0, 0, 0));
			var rng = new RandomNumberGenerator { Seed = 12345 };
			for (int i = 0; i < 2500; i++)
			{
				float b = rng.Randf() * 0.7f + 0.3f;
				starImg.SetPixel(rng.RandiRange(0, 4095), rng.RandiRange(0, 2047), new Color(b, b, b, 1));
			}

			var starMesh = new MeshInstance3D();
			var starSphere = new SphereMesh { Radius = 50f, Height = 100f, RadialSegments = 96, Rings = 48 };
			starMesh.Mesh = starSphere;
			_starShaderMat = new ShaderMaterial();
			Shader starShader = GD.Load<Shader>("res://Shaders/Starfield.gdshader");
			if (starShader != null)
			{
				_starShaderMat.Shader = starShader;
				_starShaderMat.SetShaderParameter("star_texture", ImageTexture.CreateFromImage(starImg));
			}
			starMesh.MaterialOverride = _starShaderMat;
			_globeContainer.AddChild(starMesh);

			_sunMesh = new MeshInstance3D();
			var sunSphere = new SphereMesh { Radius = 0.18f, Height = 0.36f, RadialSegments = 48, Rings = 24 };
			_sunMesh.Mesh = sunSphere;
			_sunShaderMat = new ShaderMaterial();
			Shader sunShader = GD.Load<Shader>("res://Shaders/SunSurface.gdshader");
			if (sunShader != null)
			{
				_sunShaderMat.Shader = sunShader;
				_sunShaderMat.SetShaderParameter("sun_texture", ImageTexture.CreateFromImage(GenerateSunTexture(13579, 512, 256)));
			}
			_sunMesh.MaterialOverride = _sunShaderMat;
			_globeContainer.AddChild(_sunMesh);
			CreateSolarProminences(_sunMesh, 0.18f);

			_moonMesh = new MeshInstance3D();
			_moonMesh.Mesh = new SphereMesh { Radius = 0.07f, Height = 0.14f, RadialSegments = 48, Rings = 24 };
			_moonShaderMat = new ShaderMaterial();
			Shader moonShader = GD.Load<Shader>("res://Shaders/MoonSurface.gdshader");
			if (moonShader != null)
			{
				_moonShaderMat.Shader = moonShader;
				_moonShaderMat.SetShaderParameter("moon_texture", ImageTexture.CreateFromImage(GenerateMoonTexture(24680)));
				_moonShaderMat.SetShaderParameter("sun_direction", _sunDirection);
			}
			_moonMesh.MaterialOverride = _moonShaderMat;
			_globeContainer.AddChild(_moonMesh);

			_sunLight = new DirectionalLight3D
			{
				LightColor = new Color(1f, 0.95f, 0.85f),
				LightEnergy = 2.0f
			};
			_globeContainer.AddChild(_sunLight);

			UpdateSunPosition();
			UpdateMoonPosition();
			UpdateCameraOrbit();
		}

		public override void _Process(double delta)
		{
			if (_sunMesh != null)
			{
				_sunAngle += SunSpeed * (float)delta;
				if (_sunAngle > Mathf.Tau) _sunAngle -= Mathf.Tau;
				_moonAngle += SunSpeed * 0.35f * (float)delta;
				if (_moonAngle > Mathf.Tau) _moonAngle -= Mathf.Tau;
				UpdateSunPosition();
				UpdateMoonPosition();
				_starTime += (float)delta;
				if (_starShaderMat != null) _starShaderMat.SetShaderParameter("time_offset", _starTime);
				if (_cloudShaderMat != null) _cloudShaderMat.SetShaderParameter("time_offset", _starTime);
				if (_atmosShaderMat != null) _atmosShaderMat.SetShaderParameter("time_offset", _starTime);
				if (_sunShaderMat != null) _sunShaderMat.SetShaderParameter("time_offset", _starTime);
				UpdateSolarProminences();
			}

			if (_planetShaderMat != null) _planetShaderMat.SetShaderParameter("time_offset", _starTime);
			_tacticalView.TimeOffset = _starTime;
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
			Vector3 sunPos = new Vector3(8f * Mathf.Cos(_sunAngle), 1.5f, 8f * Mathf.Sin(_sunAngle));
			_sunDirection = sunPos.Normalized();
			if (_sunMesh != null) _sunMesh.Position = sunPos;
			if (_sunLight != null)
			{
				_sunLight.Position = sunPos;
				_sunLight.LookAt(Vector3.Zero, Vector3.Up);
			}

			_planetShaderMat?.SetShaderParameter("sun_direction", _sunDirection);
			_sunShaderMat?.SetShaderParameter("time_offset", _starTime);
			_atmosShaderMat?.SetShaderParameter("sun_direction", _sunDirection);
			_cloudShaderMat?.SetShaderParameter("sun_direction", _sunDirection);
			_moonShaderMat?.SetShaderParameter("sun_direction", _sunDirection);
			_tacticalView.SunDirection = _sunDirection;
		}

		private void UpdateMoonPosition()
		{
			if (_moonMesh == null) return;
			Vector3 moonPos = new Vector3(
				8.0f * Mathf.Cos(_moonAngle + Mathf.Pi * 0.65f),
				0.85f * Mathf.Sin(_moonAngle * 0.7f),
				8.0f * Mathf.Sin(_moonAngle + Mathf.Pi * 0.65f)
			);
			_moonMesh.Position = moonPos;
			if (moonPos.LengthSquared() > 0.001f)
				_moonMesh.LookAt(Vector3.Zero, Vector3.Up);
		}

		private Image GenerateMoonTexture(int seed, int width = 512, int height = 256)
		{
			Image img = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
			var rng = new RandomNumberGenerator { Seed = (ulong)(uint)seed };
			var maria = new (Vector2 center, float radius, float aspect, float darkness, float seed)[]
			{
				(new Vector2(0.36f, 0.34f), 0.17f, 0.72f, 0.48f, 1.7f),
				(new Vector2(0.55f, 0.38f), 0.13f, 0.82f, 0.54f, 2.1f),
				(new Vector2(0.66f, 0.52f), 0.18f, 0.90f, 0.50f, 3.8f),
				(new Vector2(0.43f, 0.62f), 0.12f, 0.75f, 0.56f, 4.4f),
				(new Vector2(0.22f, 0.52f), 0.10f, 1.20f, 0.60f, 5.2f),
				(new Vector2(0.76f, 0.32f), 0.09f, 0.92f, 0.58f, 6.6f),
				(new Vector2(0.72f, 0.70f), 0.10f, 0.82f, 0.61f, 7.1f)
			};
			var rayCenters = new (Vector2 center, float radius, float seed)[]
			{
				(new Vector2(Wrap01(0.28f + HashNoise(seed, 1) * 0.08f), 0.66f), 0.20f, 13f),
				(new Vector2(Wrap01(0.62f + HashNoise(seed, 2) * 0.08f), 0.43f), 0.16f, 11f),
				(new Vector2(Wrap01(0.48f + HashNoise(seed, 3) * 0.08f), 0.74f), 0.15f, 17f)
			};

			for (int y = 0; y < img.GetHeight(); y++)
			{
				for (int x = 0; x < img.GetWidth(); x++)
				{
					float u = (float)x / width;
					float v = (float)y / height;
					float shade = 0.48f + FbmNoise(u * 5.0f + seed * 0.001f, v * 2.7f - seed * 0.0007f, 5) * 0.16f + FbmNoise(u * 55.0f + 13.1f, v * 28.0f + 3.7f, 4) * 0.08f;
					foreach (var patch in maria)
					{
						float dx = WrapUnitDistance(u - patch.center.X);
						float dy = (v - patch.center.Y) / patch.aspect;
						float d = Mathf.Sqrt((dx * dx) / (patch.radius * patch.radius) + (dy * dy) / (patch.radius * patch.radius));
						float mask = 1f - Mathf.SmoothStep(0.72f, 1.0f, d + (FbmNoise(u * 14f + patch.seed, v * 8f - patch.seed, 3) - 0.5f) * 0.36f);
						shade = Mathf.Lerp(shade, shade * patch.darkness, mask);
					}
					foreach (var rays in rayCenters)
					{
						float dx = WrapUnitDistance(u - rays.center.X);
						float dy = v - rays.center.Y;
						float dist = Mathf.Sqrt(dx * dx + dy * dy);
						float angle = Mathf.Atan2(dy, dx);
						float spokes = Mathf.Pow(Mathf.Abs(Mathf.Sin(angle * rays.seed)), 20f);
						shade += spokes * Mathf.Exp(-dist / rays.radius) * 0.12f;
					}
					shade = Mathf.Clamp(shade, 0.10f, 0.86f);
					img.SetPixel(x, y, new Color(shade, shade, shade * 0.96f, 1f));
				}
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
			}

			return img;
		}

		private static float WrapUnitDistance(float value)
		{
			return value - Mathf.Round(value);
		}

		private static float Wrap01(float value)
		{
			value -= Mathf.Floor(value);
			return value;
		}

		private static float HashNoise(int x, int y)
		{
			int n = x * 374761393 + y * 668265263;
			n = (n ^ (n >> 13)) * 1274126177;
			n ^= n >> 16;
			return (n & 0x7fffffff) / 2147483647f;
		}

		private static float ValueNoise(float x, float y)
		{
			int xi = Mathf.FloorToInt(x);
			int yi = Mathf.FloorToInt(y);
			float xf = x - xi;
			float yf = y - yi;
			float u = xf * xf * (3f - 2f * xf);
			float v = yf * yf * (3f - 2f * yf);
			return Mathf.Lerp(
				Mathf.Lerp(HashNoise(xi, yi), HashNoise(xi + 1, yi), u),
				Mathf.Lerp(HashNoise(xi, yi + 1), HashNoise(xi + 1, yi + 1), u),
				v
			);
		}

		private static float FbmNoise(float x, float y, int octaves)
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

		private static float CellularNoise(float x, float y)
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
					float px = cx + HashNoise(cx, cy);
					float py = cy + HashNoise(cx + 19, cy - 37);
					float dx = x - px;
					float dy = y - py;
					best = Mathf.Min(best, dx * dx + dy * dy);
				}
			}

			return Mathf.Clamp(Mathf.Sqrt(best), 0f, 1f);
		}

		private Image GenerateSunTexture(int seed, int width = 512, int height = 256)
		{
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

					float flowA = FbmNoise(n.X * 7.5f + seed * 0.001f, n.Z * 7.5f + n.Y * 1.7f, 5);
					float flowB = FbmNoise(n.Z * 11.0f - seed * 0.0007f, n.Y * 9.0f + n.X * 2.0f, 4);
					float cells = CellularNoise(u * 72f + flowA * 3.5f + seed * 0.013f, v * 38f + flowB * 2.2f);
					float granules = 1f - Mathf.SmoothStep(0.20f, 0.74f, cells);
					float mottling = FbmNoise(u * 22f + flowB * 2.4f, v * 14f + flowA * 2.0f, 5);
					float active = Mathf.Pow(Mathf.Clamp(FbmNoise(n.X * 4.0f + 21.7f, n.Y * 4.0f + n.Z * 1.3f, 4), 0f, 1f), 3.0f);
					float heat = Mathf.Clamp(0.42f + granules * 0.24f + mottling * 0.25f + active * 0.22f, 0f, 1f);
					float flare = Mathf.SmoothStep(0.74f, 0.94f, active + granules * 0.22f);
					Color color = new Color(1.0f, 0.29f + heat * 0.48f + flare * 0.12f, 0.035f + heat * 0.12f + flare * 0.07f, 1f);
					img.SetPixel(x, y, color);
				}
			}
			return img;
		}

		private void CreateSolarProminences(Node3D parent, float radius)
		{
			_solarProminenceRoot = new Node3D();
			parent.AddChild(_solarProminenceRoot);

			_solarProminenceMat = new ShaderMaterial();
			Shader shader = GD.Load<Shader>("res://Shaders/SolarProminence.gdshader");
			if (shader != null)
			{
				_solarProminenceMat.Shader = shader;
				_solarProminenceMat.SetShaderParameter("prominence_color", new Color(1f, 0.28f, 0.03f, 0.48f));
			}

			for (int i = 0; i < 7; i++)
			{
				var arc = new MeshInstance3D
				{
					Mesh = CreateSolarProminenceMesh(radius, i),
					MaterialOverride = _solarProminenceMat,
					Rotation = new Vector3(i * 0.71f, i * 1.13f, i * 0.47f)
				};
				_solarProminenceRoot.AddChild(arc);
			}
		}

		private Mesh CreateSolarProminenceMesh(float radius, int seed)
		{
			var surface = new SurfaceTool();
			surface.Begin(Mesh.PrimitiveType.Triangles);
			int segments = 28;
			float arcWidth = radius * 0.0012f;
			float start = -0.70f;
			float end = 0.70f;
			float height = radius * (0.007f + (seed % 4) * 0.0015f);

			for (int i = 0; i < segments; i++)
			{
				AddProminenceQuad(surface, radius, arcWidth, height, start, end, segments, i);
			}

			return surface.Commit();
		}

		private void AddProminenceQuad(SurfaceTool surface, float radius, float width, float height, float start, float end, int segments, int i)
		{
			float t0 = (float)i / segments;
			float t1 = (float)(i + 1) / segments;
			Vector3 a0 = SolarProminencePoint(radius, height, Mathf.Lerp(start, end, t0), t0);
			Vector3 a1 = SolarProminencePoint(radius, height, Mathf.Lerp(start, end, t1), t1);
			Vector3 side0 = new Vector3(0, width, 0);
			Vector3 side1 = new Vector3(0, width, 0);

			surface.AddVertex(a0 - side0);
			surface.AddVertex(a0 + side0);
			surface.AddVertex(a1 - side1);
			surface.AddVertex(a1 - side1);
			surface.AddVertex(a0 + side0);
			surface.AddVertex(a1 + side1);
		}

		private Vector3 SolarProminencePoint(float radius, float height, float angle, float t)
		{
			float r = radius * 1.02f + Mathf.Sin(t * Mathf.Pi) * height;
			return new Vector3(Mathf.Sin(angle) * r, 0f, Mathf.Cos(angle) * r);
		}

		private void UpdateSolarProminences()
		{
			if (_solarProminenceRoot == null) return;
			_solarProminenceRoot.RotateY(0.0025f);
			float pulse = 0.72f + 0.28f * Mathf.Sin(_starTime * 2.4f);
			_solarProminenceRoot.Scale = Vector3.One * (0.94f + pulse * 0.10f);
			_solarProminenceMat?.SetShaderParameter("pulse", pulse);
		}

		private void EnterTacticalMode(Vector2? focusScreenPos = null)
		{
			if (_loadedMapData == null) return;

			Vector2 screenPos = focusScreenPos ?? GetViewportRect().Size * 0.5f;
			Vector3 focusNormal;
			Vector2I focusMap;
			if (TryGetMapPointFromScreen(screenPos, out Vector2I hitMap, out Vector3 hitNormal))
			{
				focusMap = hitMap;
				focusNormal = hitNormal;
			}
			else
			{
				focusNormal = _planetMesh.ToLocal(_camera3D.GlobalPosition).Normalized();
				focusMap = PlanetMeshBuilder.SphereDirectionToMap(focusNormal, _loadedMapData);
			}

			_isTacticalMode = true;
			_camera3D.Current = false;
			_camera2D.Enabled = true;
			_camera2D.MakeCurrent();
			_camera2D.Zoom = Vector2.One;
			_camera2D.Position = Vector2.Zero;

			_tacticalView.MapData = _loadedMapData;
			_tacticalView.CenterX = focusMap.X;
			_tacticalView.CenterY = focusMap.Y;
			_tacticalView.FlipX = false;
			_tacticalView.FlipY = false;
			_tacticalView.ZoomLevel = TacticalInitialZoom;
			_tacticalView.ShowVillageNames = _showVillageNames;
			_tacticalView.SunDirection = _sunDirection;
			_tacticalView.UseDayNight = true;
			_tacticalView.Visible = true;
			_globeContainer.Visible = false;
			_tacticalView.QueueRedraw();
		}

		private void ExitTacticalMode()
		{
			if (_loadedMapData != null)
			{
				Vector3 dir = _tacticalView.GetSphereDirectionFromCamera(_camera2D);
				_cameraYaw = Mathf.Atan2(dir.X, dir.Z);
				_cameraPitch = Mathf.Clamp(Mathf.Asin(dir.Y), -1.4f, 1.4f);
			}

			_isTacticalMode = false;
			_camera3D.Current = true;
			_camera2D.Enabled = false;
			_tacticalView.Visible = false;
			_globeContainer.Visible = true;
			_cameraDistance = TacticalEntryDistance + 0.02f;
			UpdateCameraOrbit();
		}

		private bool TryGetMapPointFromScreen(Vector2 screenPos, out Vector2I mapPoint, out Vector3 localNormal)
		{
			mapPoint = Vector2I.Zero;
			localNormal = Vector3.Zero;
			if (_loadedMapData == null) return false;

			Vector3 from = _camera3D.ProjectRayOrigin(screenPos);
			Vector3 rayNormal = _camera3D.ProjectRayNormal(screenPos).Normalized();
			if (TryRaycastPlanetMesh(from, rayNormal, out mapPoint, out localNormal))
				return true;

			Vector3 to = from + rayNormal * 1000f;
			var spaceState = _globeContainer.GetWorld3D().DirectSpaceState;
			var query = PhysicsRayQueryParameters3D.Create(from, to);
			var result = spaceState.IntersectRay(query);
			if (result.Count == 0) return false;

			Vector3 hitPos = (Vector3)result["position"];
			localNormal = _planetMesh.ToLocal(hitPos).Normalized();
			mapPoint = PlanetMeshBuilder.SphereDirectionToMap(localNormal, _loadedMapData);
			return true;
		}

		private bool TryRaycastPlanetMesh(Vector3 rayOriginGlobal, Vector3 rayDirectionGlobal, out Vector2I mapPoint, out Vector3 localNormal)
		{
			mapPoint = Vector2I.Zero;
			localNormal = Vector3.Zero;
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

			localNormal = (origin + direction * distance).Normalized();
			mapPoint = PlanetMeshBuilder.SphereDirectionToMap(localNormal, _loadedMapData);
			return true;
		}

		private bool ShouldFlipTacticalY(Vector2I focusMap, Vector3 focusNormal)
		{
			if (_loadedMapData == null || focusNormal.LengthSquared() < 0.001f) return false;

			Vector3 cameraUpLocal = (_planetMesh.GlobalTransform.Basis.Inverse() * _camera3D.GlobalTransform.Basis.Y).Normalized();
			Vector3 tangentUp = cameraUpLocal - focusNormal * cameraUpLocal.Dot(focusNormal);
			if (tangentUp.LengthSquared() < 0.001f) return false;
			tangentUp = tangentUp.Normalized();

			Vector3 northSample = PlanetMeshBuilder.MapToSphereDirection(focusMap.X + 0.5f, focusMap.Y - 0.5f, _loadedMapData.Width, _loadedMapData.Height);
			Vector3 tangentNorth = northSample - focusNormal * northSample.Dot(focusNormal);
			if (tangentNorth.LengthSquared() < 0.001f) return false;
			tangentNorth = tangentNorth.Normalized();

			return tangentUp.Dot(tangentNorth) > 0f;
		}

		private bool ShouldFlipTacticalX(Vector2I focusMap, Vector3 focusNormal)
		{
			if (_loadedMapData == null || focusNormal.LengthSquared() < 0.001f) return false;

			Vector3 cameraRightLocal = (_planetMesh.GlobalTransform.Basis.Inverse() * _camera3D.GlobalTransform.Basis.X).Normalized();
			Vector3 tangentRight = cameraRightLocal - focusNormal * cameraRightLocal.Dot(focusNormal);
			if (tangentRight.LengthSquared() < 0.001f) return false;
			tangentRight = tangentRight.Normalized();

			Vector3 eastSample = PlanetMeshBuilder.MapToSphereDirection(focusMap.X + 1.5f, focusMap.Y + 0.5f, _loadedMapData.Width, _loadedMapData.Height);
			Vector3 tangentEast = eastSample - focusNormal * eastSample.Dot(focusNormal);
			if (tangentEast.LengthSquared() < 0.001f) return false;
			tangentEast = tangentEast.Normalized();

			return tangentRight.Dot(tangentEast) > 0f;
		}

		private void ToggleVillageNames(bool on)
		{
			_showVillageNames = on;
			_tacticalView.ShowVillageNames = on;
		}

		private async void InitializeMatch()
		{
			_currentSaveSlotPath = Path.Combine(GameManager.Instance.GetPartidasDir(), "Slot_1");
			string currentMatchFile = Path.Combine(_currentSaveSlotPath, "save_atual.json");

			if (!File.Exists(currentMatchFile))
			{
				_labelStatus.Text = "NENHUM MAPA ENCONTRADO!";
				GD.PrintErr("Arquivo da partida nao encontrado.");
				return;
			}

			_labelStatus.Text = "Carregando Mundo...";
			_loadedMapData = await Task.Run(() => GameManager.LoadMap(currentMatchFile));
			if (_loadedMapData == null)
			{
				_labelStatus.Text = "ERRO AO CARREGAR MAPA!";
				return;
			}

			RenderCurrentMapData();
			_labelStatus.Text = "Pronto para Desembarcar!";
			SpawnInitialCaravan();
		}

		private async void RenderCurrentMapData()
		{
			_labelStatus.Text = "Costurando Globo 3D...";
			_globeImage = await Task.Run(GenerateGlobeImage);
			Image oceanMask = await Task.Run(GenerateOceanMaskImage);

			_globeTexture = ImageTexture.CreateFromImage(_globeImage);
			_oceanMaskTexture = ImageTexture.CreateFromImage(oceanMask);
			_planetMesh.Mesh = PlanetMeshBuilder.CreatePlanetMesh(_loadedMapData);

			_planetShaderMat = new ShaderMaterial();
			Shader shader = GD.Load<Shader>("res://Shaders/PlanetSurface.gdshader");
			if (shader != null)
			{
				_planetShaderMat.Shader = shader;
				_planetShaderMat.SetShaderParameter("planet_texture", _globeTexture);
				_planetShaderMat.SetShaderParameter("ocean_mask", _oceanMaskTexture);
				_planetShaderMat.SetShaderParameter("sun_direction", _sunDirection);
			}
			_planetMesh.SetSurfaceOverrideMaterial(0, _planetShaderMat);
			if (_loadedMapData.CelestialTextureVersion != CelestialTextureVersion)
			{
				_loadedMapData.MoonTextureData = null;
				_loadedMapData.SunTextureData = null;
			}
			ApplyMoonTexture();
			ApplySunTexture();

			_tacticalView.MapData = _loadedMapData;
			_tacticalView.SunDirection = _sunDirection;
		}

		private void ApplyMoonTexture()
		{
			if (_moonShaderMat == null) return;

			Image moonImage;
			if (_loadedMapData.CelestialTextureVersion == CelestialTextureVersion && _loadedMapData.MoonTextureData != null && _loadedMapData.MoonTextureWidth > 0 && _loadedMapData.MoonTextureHeight > 0)
			{
				moonImage = Image.CreateFromData(_loadedMapData.MoonTextureWidth, _loadedMapData.MoonTextureHeight, false, Image.Format.Rgba8, _loadedMapData.MoonTextureData);
			}
			else
			{
				moonImage = GenerateMoonTexture((_loadedMapData.MapName ?? "moon").GetHashCode());
				_loadedMapData.MoonTextureWidth = moonImage.GetWidth();
				_loadedMapData.MoonTextureHeight = moonImage.GetHeight();
				_loadedMapData.MoonTextureData = moonImage.GetData();
				_loadedMapData.CelestialTextureVersion = CelestialTextureVersion;
			}

			_moonShaderMat.SetShaderParameter("moon_texture", ImageTexture.CreateFromImage(moonImage));
		}

		private void ApplySunTexture()
		{
			if (_sunShaderMat == null) return;

			Image sunImage;
			if (_loadedMapData.CelestialTextureVersion == CelestialTextureVersion && _loadedMapData.SunTextureData != null && _loadedMapData.SunTextureWidth > 0 && _loadedMapData.SunTextureHeight > 0)
			{
				sunImage = Image.CreateFromData(_loadedMapData.SunTextureWidth, _loadedMapData.SunTextureHeight, false, Image.Format.Rgba8, _loadedMapData.SunTextureData);
			}
			else
			{
				sunImage = GenerateSunTexture((_loadedMapData.MapName ?? "sun").GetHashCode());
				_loadedMapData.SunTextureWidth = sunImage.GetWidth();
				_loadedMapData.SunTextureHeight = sunImage.GetHeight();
				_loadedMapData.SunTextureData = sunImage.GetData();
				_loadedMapData.CelestialTextureVersion = CelestialTextureVersion;
			}

			_sunShaderMat.SetShaderParameter("sun_texture", ImageTexture.CreateFromImage(sunImage));
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
					int realX = Mathf.Clamp((int)(x / _globeScaleX), 0, mapW - 1);
					int realY = Mathf.Clamp((int)(y / _globeScaleY), 0, mapH - 1);
					EvaluateAndPaintPixelDirect(realX, realY, x, y, img);
				}
			}
			return img;
		}

		private Image GenerateOceanMaskImage()
		{
			int w = _globeImage.GetWidth();
			int h = _globeImage.GetHeight();
			Image img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
			for (int y = 0; y < h; y++)
			{
				for (int x = 0; x < w; x++)
				{
					int mapX = Mathf.Clamp((int)(x / _globeScaleX), 0, _loadedMapData.Width - 1);
					int mapY = Mathf.Clamp((int)(y / _globeScaleY), 0, _loadedMapData.Height - 1);
					byte terrain = PlanetMeshBuilder.GetTerrainAt(_loadedMapData, mapX, mapY);
					float mask = PlanetMeshBuilder.IsOcean(terrain) ? 1f : 0f;
					img.SetPixel(x, y, new Color(mask, mask, mask, 1f));
				}
			}
			return img;
		}

		public void UpdateGlobePixel(int mapX, int mapY, byte territoryId)
		{
			if (_globeImage == null || _globeTexture == null) return;
			EvaluateAndPaintPixelDirect(mapX, mapY, (int)(mapX * _globeScaleX), (int)(mapY * _globeScaleY), _globeImage);
			EvaluateAndPaintPixelDirect(mapX + 1, mapY, (int)((mapX + 1) * _globeScaleX), (int)(mapY * _globeScaleY), _globeImage);
			EvaluateAndPaintPixelDirect(mapX - 1, mapY, (int)((mapX - 1) * _globeScaleX), (int)(mapY * _globeScaleY), _globeImage);
			EvaluateAndPaintPixelDirect(mapX, mapY + 1, (int)(mapX * _globeScaleX), (int)((mapY + 1) * _globeScaleY), _globeImage);
			EvaluateAndPaintPixelDirect(mapX, mapY - 1, (int)(mapX * _globeScaleX), (int)((mapY - 1) * _globeScaleY), _globeImage);
			_globeTexture.Update(_globeImage);
		}

		private byte GetOwnerAt(int x, int y)
		{
			if (_loadedMapData == null || _loadedMapData.Width <= 0 || _loadedMapData.Height <= 0) return 0;
			x = WrapMapX(x, _loadedMapData.Width);
			y = WrapMapY(y, _loadedMapData.Height);
			int cx = x / ChunkData.CHUNK_SIZE;
			int cy = y / ChunkData.CHUNK_SIZE;
			if (_loadedMapData.Chunks.TryGetValue(new Vector2I(cx, cy), out ChunkData chunk))
				return chunk.TerritoryMap[(y % ChunkData.CHUNK_SIZE) * ChunkData.CHUNK_SIZE + (x % ChunkData.CHUNK_SIZE)];
			return 0;
		}

		private void EvaluateAndPaintPixelDirect(int mapX, int mapY, int globeX, int globeY, Image img)
		{
			if (globeX < 0 || globeX >= img.GetWidth() || globeY < 0 || globeY >= img.GetHeight()) return;
			if (_loadedMapData == null || _loadedMapData.Width <= 0 || _loadedMapData.Height <= 0) return;
			mapX = WrapMapX(mapX, _loadedMapData.Width);
			mapY = WrapMapY(mapY, _loadedMapData.Height);

			int chunkX = mapX / ChunkData.CHUNK_SIZE;
			int chunkY = mapY / ChunkData.CHUNK_SIZE;
			if (!_loadedMapData.Chunks.TryGetValue(new Vector2I(chunkX, chunkY), out ChunkData chunk))
			{
				img.SetPixel(globeX, globeY, Colors.Black);
				return;
			}

			int localX = mapX % ChunkData.CHUNK_SIZE;
			int localY = mapY % ChunkData.CHUNK_SIZE;
			int flatIndex = localY * ChunkData.CHUNK_SIZE + localX;
			byte terrainType = chunk.TerrainMap[flatIndex];
			byte ownerId = chunk.TerritoryMap[flatIndex];
			Color terrainColor = GetTerrainColor(terrainType);

			if (ownerId > 0 && ownerId != 255)
			{
				bool isBorder = false;
				int[] dx = { 1, -1, 0, 0 };
				int[] dy = { 0, 0, 1, -1 };
				for (int i = 0; i < 4; i++)
				{
					byte neighborOwner = GetOwnerAt(mapX + dx[i], mapY + dy[i]);
					if (neighborOwner != ownerId && neighborOwner != 255)
					{
						isBorder = true;
						break;
					}
				}

				img.SetPixel(globeX, globeY, isBorder ? new Color(0.18f, 0.18f, 0.18f, 1f) :
					ownerId == 1 ? terrainColor.Lerp(GetTerritoryColor(ownerId), 0.35f) : terrainColor);
			}
			else if (ownerId == 255)
			{
				img.SetPixel(globeX, globeY, new Color(1.0f, 0.9f, 0.05f, 1.0f));
			}
			else
			{
				img.SetPixel(globeX, globeY, terrainColor);
			}
		}

		private static int WrapMapX(int x, int width)
		{
			if (width <= 0) return 0;
			int wrapped = x % width;
			return wrapped < 0 ? wrapped + width : wrapped;
		}

		private static int WrapMapY(int y, int height)
		{
			if (height <= 0) return 0;
			int wrapped = y % height;
			return wrapped < 0 ? wrapped + height : wrapped;
		}

		public static Color GetTerrainColor(byte type)
		{
			return type switch
			{
				0 => new Color(0.0f, 0.09f, 0.42f),
				1 => new Color(0.06f, 0.28f, 0.68f),
				2 => new Color(0.88f, 0.78f, 0.50f),
				3 => new Color(0.30f, 0.66f, 0.22f),
				4 => new Color(0.08f, 0.42f, 0.10f),
				5 => new Color(0.03f, 0.26f, 0.05f),
				6 => new Color(0.67f, 0.56f, 0.30f),
				7 => new Color(0.86f, 0.64f, 0.18f),
				8 => new Color(0.58f, 0.70f, 0.70f),
				9 => new Color(0.90f, 0.95f, 1.0f),
				10 => new Color(0.45f, 0.45f, 0.44f),
				11 => new Color(0.74f, 0.74f, 0.74f),
				_ => Colors.Black
			};
		}

		public static Color GetTerritoryColor(byte ownerId)
		{
			if (ownerId == 255) return new Color(1.0f, 1.0f, 0.0f, 1.0f);
			if (ownerId == 1) return new Color(0.2f, 0.4f, 0.9f, 1.0f);
			if (ownerId == 2) return new Color(0.9f, 0.2f, 0.2f, 1.0f);
			return Colors.White;
		}

		public override void _UnhandledInput(InputEvent @event)
		{
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
				HandleMouseButton(mouseBtn);
			}
			else if (@event is InputEventMouseMotion mouseMotion)
			{
				if (_isDraggingGlobe) HandlePan(mouseMotion.Relative, false);
			}
			else if (@event is InputEventScreenTouch touchEvent)
			{
				HandleScreenTouch(touchEvent);
			}
			else if (@event is InputEventScreenDrag dragEvent)
			{
				if (dragEvent.Index == 0) _finger0Pos = dragEvent.Position;
				else if (dragEvent.Index == 1) _finger1Pos = dragEvent.Position;

				if (_finger0Down && _finger1Down) HandlePinch();
				else
				{
					_isDraggingGlobe = true;
					HandlePan(dragEvent.Relative, true);
				}
			}
		}

		private void HandleMouseButton(InputEventMouseButton mouseBtn)
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
						SelectStartingVillage(mouseBtn.Position);
					_wasDragging = false;
				}
			}
			else if (mouseBtn.ButtonIndex == MouseButton.WheelUp && mouseBtn.Pressed)
			{
				if (!_isTacticalMode)
				{
					_cameraDistance = Mathf.Max(TacticalEntryDistance, _cameraDistance - 0.15f);
					UpdateCameraOrbit();
					if (_cameraDistance <= TacticalEntryDistance) EnterTacticalMode(mouseBtn.Position);
				}
				else
				{
					float oldZoom = _tacticalView.ZoomLevel;
					float newZoom = TacticalView.ClampZoom(oldZoom * 1.25f);
					_camera2D.Position *= newZoom / oldZoom;
					_camera2D.Position = _tacticalView.ClampCameraPosition(_camera2D.Position);
					_tacticalView.ZoomLevel = newZoom;
				}
			}
			else if (mouseBtn.ButtonIndex == MouseButton.WheelDown && mouseBtn.Pressed)
			{
				if (!_isTacticalMode)
				{
					_cameraDistance = Mathf.Min(MaxGlobeDistance, _cameraDistance + 0.15f);
					UpdateCameraOrbit();
				}
				else
				{
					float oldZoom = _tacticalView.ZoomLevel;
					float newZoom = oldZoom * 0.8f;
					if (newZoom < TacticalExitZoom) ExitTacticalMode();
					else
					{
						_camera2D.Position *= newZoom / oldZoom;
						_camera2D.Position = _tacticalView.ClampCameraPosition(_camera2D.Position);
						_tacticalView.ZoomLevel = newZoom;
					}
				}
			}
		}

		private void HandleScreenTouch(InputEventScreenTouch touchEvent)
		{
			if (touchEvent.Index == 0)
			{
				_finger0Down = touchEvent.Pressed;
				_finger0Pos = touchEvent.Position;
				if (touchEvent.Pressed)
				{
					_isDraggingGlobe = true;
					_wasDragging = false;
				}
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
				else
				{
					_pinchPrevDistance = 0f;
				}
			}
		}

		private void HandlePan(Vector2 relative, bool fromTouch)
		{
			if (relative.LengthSquared() > 4.0f) _wasDragging = true;
			if (!_isTacticalMode)
			{
				_cameraYaw -= relative.X * 0.005f;
				_cameraPitch = Mathf.Clamp(_cameraPitch + relative.Y * 0.005f, -1.4f, 1.4f);
				UpdateCameraOrbit();
			}
			else
			{
				_camera2D.Position -= relative;
				_camera2D.Position = _tacticalView.ClampCameraPosition(_camera2D.Position);
			}
		}

		private void HandlePinch()
		{
			float currentDist = _finger0Pos.DistanceTo(_finger1Pos);
			if (_pinchPrevDistance <= 0f)
			{
				_pinchPrevDistance = currentDist;
				return;
			}

			float delta = currentDist - _pinchPrevDistance;
			_pinchPrevDistance = currentDist;
			if (Mathf.Abs(delta) < 2f) return;

			Vector2 pinchCenter = (_finger0Pos + _finger1Pos) * 0.5f;
			if (!_isTacticalMode)
			{
				_cameraDistance = Mathf.Clamp(_cameraDistance - delta * 0.005f, TacticalEntryDistance, MaxGlobeDistance);
				UpdateCameraOrbit();
				if (_cameraDistance <= TacticalEntryDistance) EnterTacticalMode(pinchCenter);
			}
			else
			{
				float oldZoom = _tacticalView.ZoomLevel;
				float newZoom = oldZoom * (1f + delta * 0.008f);
				if (newZoom < TacticalExitZoom) ExitTacticalMode();
				else
				{
					newZoom = TacticalView.ClampZoom(newZoom);
					_camera2D.Position *= newZoom / oldZoom;
					_camera2D.Position = _tacticalView.ClampCameraPosition(_camera2D.Position);
					_tacticalView.ZoomLevel = newZoom;
				}
			}
		}

		private void SelectStartingVillage(Vector2 screenPos)
		{
			if (!TryGetMapPointFromScreen(screenPos, out Vector2I mapPoint, out _)) return;

			VillageData closest = null;
			float minDist = float.MaxValue;
			foreach (VillageData village in _loadedMapData.Villages)
			{
				float dist = (village.X - mapPoint.X) * (village.X - mapPoint.X) + (village.Y - mapPoint.Y) * (village.Y - mapPoint.Y);
				if (dist < minDist)
				{
					minDist = dist;
					closest = village;
				}
			}

			if (closest != null) StartGameAs(closest);
		}

		private void SpawnInitialCaravan()
		{
			_labelStatus.Text = "Selecione uma Aldeia Amarela clicando nela!";
		}

		private void StartGameAs(VillageData village)
		{
			_playerCapital = village;
			_playerCapital.OwnerId = 1;
			_isGameStarted = true;
			GetNode<Control>("CanvasLayer/HUD/BottomPanel").Visible = false;
			_labelStatus.Text = $"Voce assumiu o controle de {village.Name}! Expansao iniciada.";
			_territorySystem.StartSimulation(_loadedMapData, UpdateGlobePixel);
			_territorySystem.RegisterVillage(new Vector2I(village.X, village.Y), 1);
		}

		private Vector3 GetSpherePosition(int mapX, int mapY)
		{
			return PlanetMeshBuilder.MapToSphereDirection(mapX, mapY, _loadedMapData.Width, _loadedMapData.Height);
		}

		private void SpawnVillagePin(VillageData village)
		{
			Vector3 pos3D = GetSpherePosition(village.X, village.Y);
			var pinSprite = new Sprite3D
			{
				Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
				PixelSize = 0.003f,
				Position = pos3D * 1.03f
			};
			Image img = Image.CreateEmpty(32, 32, false, Image.Format.Rgba8);
			img.Fill(new Color(1f, 1f, 0f, 1f));
			pinSprite.Texture = ImageTexture.CreateFromImage(img);
			_planetMesh.AddChild(pinSprite);
		}

		private void OnBtnSettlePressed()
		{
		}

		private void SaveMatch()
		{
			if (_loadedMapData == null) return;
			string currentMatchFile = Path.Combine(_currentSaveSlotPath, "save_atual.json");
			GameManager.SaveMap(currentMatchFile, _loadedMapData);
		}

		private void OnBtnBackPressed()
		{
			SaveMatch();
			GameManager.Instance?.GoToMainMenu();
		}
	}
}
