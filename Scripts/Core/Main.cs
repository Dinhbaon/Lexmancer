using Godot;
using System;
using System.Threading.Tasks;
using Lexmancer.Elements;
using Lexmancer.Systems;
using Lexmancer.UI;
using Lexmancer.Config;
using Lexmancer.Combat;
using Lexmancer.Abilities.LLM;

public partial class Main : Node2D
{
	[Export] public bool UseLLM { get; set; } = false;
	[Export] public bool GenerateAbilitiesOnStartup { get; set; } = false;

	private CharacterBody2D player;
	private GameManager gameManager;
	private Camera2D camera;
	private LLMElementGenerator llmGenerator;

	public override void _Ready()
	{
		GD.Print("=== Starting Action Roguelike ===");

		// Set configuration
		GameConfig.UseLLM = UseLLM;
		GameConfig.GenerateElementAbilitiesOnStartup = GenerateAbilitiesOnStartup;
		GameConfig.PrintConfig();

		// If using LLM with LLamaSharp direct inference, load model first
		if (GameConfig.UseLLM && GameConfig.UseLLamaSharpDirect)
		{
			CallDeferred(nameof(InitializeModelAndContinue));
		}
		else
		{
			InitializeGameSystems();
		}
	}

	/// <summary>
	/// Load LLamaSharp model asynchronously, then continue with game initialization
	/// </summary>
	private async void InitializeModelAndContinue()
	{
		try
		{
			var modelManager = new ModelManager();
			modelManager.Name = "ModelManager";
			AddChild(modelManager);

			GD.Print("Loading LLM model from bundled assets...");
			bool success = await modelManager.InitializeAsync();

			if (!success)
			{
				GD.PrintErr("Model loading failed, falling back to Ollama HTTP mode");
				GameConfig.UseLLamaSharpDirect = false;
			}
			else
			{
				GD.Print("LLM model loaded successfully!");
			}
		}
		catch (Exception ex)
		{
			GD.PrintErr($"Model initialization error: {ex.Message}");
			GameConfig.UseLLamaSharpDirect = false;
		}

		InitializeGameSystems();
	}

	/// <summary>
	/// Initialize element system, LLM generator, and game world
	/// </summary>
	private void InitializeGameSystems()
	{
		// Initialize element system
		ElementRegistry.Initialize("player_001");

		// Initialize LLM generator if enabled
		if (GameConfig.UseLLM)
		{
			llmGenerator = new LLMElementGenerator("player_001", useLLM: true);
		}

		// Load hardcoded elements (or generate with LLM)
		if (GameConfig.UseLLM && GameConfig.GenerateElementAbilitiesOnStartup)
		{
			// Async LLM generation - will call ContinueGameInitialization when done
			CallDeferred(nameof(LoadElementsWithLLM));
		}
		else
		{
			// Synchronous hardcoded loading
			LoadHardcodedElements();
			ContinueGameInitialization();
		}
	}

	/// <summary>
	/// Continue game initialization after elements are loaded
	/// </summary>
	private void ContinueGameInitialization()
	{
		if (player != null)
		{
			// Already initialized
			return;
		}

		GD.Print("Continuing game initialization...");

		// Create the game world
		CreateLevel();

		// Create player
		player = CreatePlayer();
		AddChild(player);
		GD.Print($"Player created at position: {player.GlobalPosition}");

		// Create camera attached to player
		camera = new Camera2D();
		camera.Enabled = true;
		player.AddChild(camera);
		GD.Print("Camera attached to player");

		// Create game manager
		gameManager = new GameManager();
		gameManager.Name = "GameManager";
		gameManager.Player = player;
		AddChild(gameManager);
		GD.Print("GameManager created");

		// Create UI
		CreateUI();
		GD.Print("UI created");

		// Spawn enemies
		SpawnEnemies();
		GD.Print("WaveSpawner created");

		GD.Print("Game initialized successfully!");
		GD.Print($"Scene tree children count: {GetChildCount()}");
	}

	/// <summary>
	/// Load hardcoded elements into registry
	/// </summary>
	private void LoadHardcodedElements()
	{
		GD.Print("Loading hardcoded elements...");

		// Cache all 8 base elements (no combined elements - those are dynamic now!)
		foreach (var kvp in ElementDefinitions.BaseElements)
		{
			ElementRegistry.CacheElement(kvp.Value);
		}

		GD.Print($"Loaded {ElementDefinitions.BaseElements.Count} base elements");
		ElementRegistry.PrintStats();
	}

	/// <summary>
	/// Load elements with LLM-generated abilities
	/// </summary>
	private async void LoadElementsWithLLM()
	{
		GD.Print("Generating elements with LLM...");
		GD.Print("⏳ This may take 10-30 seconds depending on your LLM...");

		try
		{
			var allElements = ElementDefinitions.GetAllElements();
			int generated = 0;
			int cached = 0;

			foreach (var element in allElements)
			{
				GD.Print($"Generating ability for {element.Name}...");

				// Generate ability with LLM
				var ability = await llmGenerator.GenerateAbilityForElementAsync(element, forceNew: false);
				element.Ability = ability;

				// Cache element with new ability
				ElementRegistry.CacheElement(element);

				if (ability.GeneratedAt > 0)
					generated++;
				else
					cached++;
			}

			GD.Print($"✅ LLM generation complete!");
			GD.Print($"   Generated: {generated}, Cached: {cached}");
			ElementRegistry.PrintStats();
		}
		catch (Exception ex)
		{
			GD.PrintErr($"❌ LLM generation failed: {ex.Message}");
			GD.Print("Falling back to hardcoded elements...");
			LoadHardcodedElements();
		}

		// Continue with game initialization after elements are loaded
		ContinueGameInitialization();
	}

	/// <summary>
	/// Create UI elements
	/// </summary>
	private void CreateUI()
	{
		// Create a CanvasLayer for UI - this ensures UI is always drawn on top of game elements
		var uiLayer = new CanvasLayer();
		uiLayer.Name = "UILayer";
		uiLayer.Layer = 100; // High layer number to ensure it's on top
		AddChild(uiLayer);

		// Player health bar
		var healthBar = new HealthBar();
		healthBar.IsPlayerHealthBar = true;
		healthBar.Name = "PlayerHealthBar";
		uiLayer.AddChild(healthBar);

		// Element hotbar
		var hotbar = new ElementHotbar();
		hotbar.Name = "ElementHotbar";
		uiLayer.AddChild(hotbar);

		// Combination panel (includes inventory tab)
		var combinationPanel = new CombinationPanel();
		combinationPanel.Name = "CombinationPanel";
		uiLayer.AddChild(combinationPanel);

		// Game over screen
		var gameOverScreen = new GameOverScreen();
		gameOverScreen.Name = "GameOverScreen";
		uiLayer.AddChild(gameOverScreen);

		GD.Print("UI created in CanvasLayer");
	}

	/// <summary>
	/// Spawn initial wave of enemies
	/// </summary>
	private void SpawnEnemies()
	{
		var spawner = new WaveSpawner();
		spawner.EnemyCount = 5;
		spawner.Name = "WaveSpawner";
		AddChild(spawner);
	}

	public override void _ExitTree()
	{
		// Cleanup element system
		ElementRegistry.Shutdown();
		// ModelManager cleans up in its own _ExitTree
		base._ExitTree();
	}

	private CharacterBody2D CreatePlayer()
	{
		var player = new CharacterBody2D();
		player.Name = "Player";
		player.Position = new Vector2(400, 300);

		// Visual representation (colored square)
		var sprite = new ColorRect();
		sprite.Name = "Visual"; // Name it so status effects can find it
		sprite.Color = new Color(0, 1, 0); // Green
		sprite.Size = new Vector2(32, 32);
		sprite.Position = new Vector2(-16, -16); // Center it
		player.AddChild(sprite);

		// Collision shape
		var collision = new CollisionShape2D();
		var shape = new RectangleShape2D();
		shape.Size = new Vector2(32, 32);
		collision.Shape = shape;
		player.AddChild(collision);

		// Add player controller script
		var controller = new PlayerController();
		player.AddChild(controller);

		// Add to player group for targeting
		player.AddToGroup("player");

		// Add health component
		var health = new HealthComponent();
		health.Initialize(100f); // 100 HP for player
		health.Name = "HealthComponent";
		player.AddChild(health);

		return player;
	}

	private void CreateLevel()
	{
		// Create a simple room with walls
		CreateWall(new Vector2(400, 50), new Vector2(800, 20)); // Top
		CreateWall(new Vector2(400, 550), new Vector2(800, 20)); // Bottom
		CreateWall(new Vector2(50, 300), new Vector2(20, 600)); // Left
		CreateWall(new Vector2(750, 300), new Vector2(20, 600)); // Right

		// Add some obstacles
		CreateWall(new Vector2(300, 200), new Vector2(100, 20));
		CreateWall(new Vector2(500, 400), new Vector2(100, 20));

		GD.Print("Level created");
	}

	private void CreateWall(Vector2 position, Vector2 size)
	{
		var wall = new StaticBody2D();
		wall.Position = position;

		// Visual
		var visual = new ColorRect();
		visual.Color = new Color(0.3f, 0.3f, 0.3f); // Dark gray
		visual.Size = size;
		visual.Position = new Vector2(-size.X / 2, -size.Y / 2);
		wall.AddChild(visual);

		// Collision
		var collision = new CollisionShape2D();
		var shape = new RectangleShape2D();
		shape.Size = size;
		collision.Shape = shape;
		wall.AddChild(collision);

		AddChild(wall);
	}
}
