using Godot;
using System;
using System.Collections.Generic;
using Lexmancer.Elements;
using Lexmancer.Combat;
using Lexmancer.UI;

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
		Inventory.AddElement("fire", 2);
		Inventory.AddElement("water", 2);
		Inventory.AddElement("earth", 2);
		Inventory.AddElement("lightning", 2);
		Inventory.AddElement("poison", 2);
		Inventory.AddElement("wind", 2);
		Inventory.AddElement("shadow", 2);
		Inventory.AddElement("light", 2);

		GD.Print("Starting inventory:");
		Inventory.PrintInventory();

		// Wait for player to be set
		CallDeferred(nameof(InitializePlayerHealth));

		State = GameState.Playing;
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
			PlayerHealth.OnDamaged += (amount) => GD.Print($"Player took {amount} damage!");

			// Add player to group
			Player.AddToGroup("player");
		}
	}

	public void OnEnemyDied(Node enemy)
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

	public void OnEnemySpawned()
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
		ShowGameOverScreen("DEFEAT");
	}

	private void OnAllEnemiesDead()
	{
		if (State != GameState.Playing)
			return;

		State = GameState.Victory;
		GD.Print("=== VICTORY ===");
		ShowGameOverScreen("VICTORY!");
	}

	private void ShowGameOverScreen(string message)
	{
		// Pause the game
		GetTree().Paused = true;

		// Show game over UI (note: it's inside the UILayer)
		var gameOverScreen = GetNodeOrNull<GameOverScreen>("/root/Main/UILayer/GameOverScreen");
		if (gameOverScreen != null)
		{
			gameOverScreen.ShowScreen(message, State == GameState.Victory);
		}
		else
		{
			GD.PrintErr("GameOverScreen not found! Restarting in 3 seconds...");
			GetTree().CreateTimer(3.0).Timeout += RestartGame;
		}
	}

	public void RestartGame()
	{
		GD.Print("Restarting game...");
		GetTree().Paused = false;
		GetTree().ReloadCurrentScene();
	}
}
