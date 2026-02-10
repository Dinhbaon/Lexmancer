using Godot;
using System;
using System.Collections.Generic;
using Lexmancer.Elements;
using Lexmancer.Combat;
using Lexmancer.Core;
using Lexmancer.Services;

public partial class GameManager : Node
{
	public enum GameState { Playing, Victory, Defeat }

	public CharacterBody2D Player { get; set; }
	public HealthComponent PlayerHealth { get; private set; }
	public PlayerElementInventory Inventory { get; private set; }
	public GameState State { get; private set; }

	private int enemiesAlive = 0;
	private int totalEnemiesInWave = 0;

	public override void _Ready()
	{
		GD.Print("GameManager initialized");

		// Initialize player inventory with all 8 base primitives
		Inventory = new PlayerElementInventory();

		// Get all tier 1 (base) elements and add them to inventory
		var baseElements = ServiceLocator.Instance.Elements.GetElementsByTier(1);
		foreach (var element in baseElements)
		{
			Inventory.AddElement(element.Id, 2);
		}

		GD.Print("Starting inventory:");
		Inventory.PrintInventory();

		// Subscribe to EventBus events
		SubscribeToEvents();

		// Wait for player to be set
		CallDeferred(nameof(InitializePlayerHealth));

		State = GameState.Playing;
	}

	public override void _ExitTree()
	{
		// Unsubscribe from events to prevent memory leaks
		UnsubscribeFromEvents();
		base._ExitTree();
	}

	private void SubscribeToEvents()
	{
		if (EventBus.Instance != null)
		{
			EventBus.Instance.EnemyDied += OnEnemyDied;
			EventBus.Instance.EnemySpawned += OnEnemySpawned;
			GD.Print("GameManager subscribed to EventBus");
		}
		else
		{
			GD.PrintErr("EventBus not available! Make sure it's configured as autoload.");
		}
	}

	private void UnsubscribeFromEvents()
	{
		if (EventBus.Instance != null)
		{
			EventBus.Instance.EnemyDied -= OnEnemyDied;
			EventBus.Instance.EnemySpawned -= OnEnemySpawned;
		}
	}

	private void InitializePlayerHealth()
	{
		if (Player != null)
		{
			// Reuse existing player health component if present
			PlayerHealth = Player.GetNodeOrNull<HealthComponent>("HealthComponent");
			if (PlayerHealth == null)
			{
				PlayerHealth = new HealthComponent();
				PlayerHealth.Initialize(100f);
				PlayerHealth.Name = "HealthComponent";
				Player.AddChild(PlayerHealth);
			}

			PlayerHealth.OnDeath += OnPlayerDeath;
			PlayerHealth.OnDamaged += (amount) =>
			{
				GD.Print($"Player took {amount} damage!");
				// Emit event for UI to listen to
				EventBus.Instance?.EmitSignal(EventBus.SignalName.PlayerDamaged, amount, "physical");
				EventBus.Instance?.EmitSignal(EventBus.SignalName.PlayerHealthChanged, PlayerHealth.Current, PlayerHealth.Max);
			};

			// Add player to group
			Player.AddToGroup("player");

			// Emit initial health state
			EventBus.Instance?.EmitSignal(EventBus.SignalName.PlayerHealthChanged, PlayerHealth.Current, PlayerHealth.Max);
		}
	}

	// EventBus callback - note signature matches the delegate
	private void OnEnemyDied(Node enemy, Vector2 position)
	{
		enemiesAlive--;
		GD.Print($"Enemy died. Remaining: {enemiesAlive}/{totalEnemiesInWave}");

		// TODO: Drop element pickup

		// Check if all enemies are dead
		if (enemiesAlive <= 0 && State == GameState.Playing)
		{
			OnAllEnemiesDead();
		}
	}

	// EventBus callback
	private void OnEnemySpawned(Node enemy)
	{
		enemiesAlive++;
		totalEnemiesInWave++;
	}

	private void OnPlayerDeath()
	{
		if (State != GameState.Playing)
			return;

		State = GameState.Defeat;
		GD.Print("=== PLAYER DIED ===");

		// Emit event instead of directly calling UI
		EventBus.Instance?.EmitSignal(EventBus.SignalName.PlayerDied);
		EventBus.Instance?.EmitSignal(EventBus.SignalName.GameStateChanged, "defeat");

		// Pause the game
		GetTree().Paused = true;
		EventBus.Instance?.EmitSignal(EventBus.SignalName.GamePaused);

		// Request game over screen via event
		EventBus.Instance?.EmitSignal(EventBus.SignalName.ShowGameOverScreen, "DEFEAT", false);
	}

	private void OnAllEnemiesDead()
	{
		if (State != GameState.Playing)
			return;

		State = GameState.Victory;
		GD.Print("=== VICTORY ===");

		// Emit event instead of directly calling UI
		EventBus.Instance?.EmitSignal(EventBus.SignalName.AllEnemiesDefeated);
		EventBus.Instance?.EmitSignal(EventBus.SignalName.GameStateChanged, "victory");

		// Pause the game
		GetTree().Paused = true;
		EventBus.Instance?.EmitSignal(EventBus.SignalName.GamePaused);

		// Request game over screen via event
		EventBus.Instance?.EmitSignal(EventBus.SignalName.ShowGameOverScreen, "VICTORY!", true);
	}

	public void RestartGame()
	{
		GD.Print("Restarting game...");

		// Emit event
		EventBus.Instance?.EmitSignal(EventBus.SignalName.GameRestarting);

		GetTree().Paused = false;
		EventBus.Instance?.EmitSignal(EventBus.SignalName.GameUnpaused);

		GetTree().ReloadCurrentScene();
	}
}
