using Godot;
using System;
using Lexmancer.Enemies;

namespace Lexmancer.Systems;

/// <summary>
/// Spawns waves of enemies for the vertical slice
/// </summary>
public partial class WaveSpawner : Node2D
{
	[Export] public int EnemyCount { get; set; } = 5;
	[Export] public float SpawnRadius { get; set; } = 300f;

	private Random random = new Random();
	private GameManager gameManager;
	private Node2D player;

	public override void _Ready()
	{
		// Get game manager and player
		CallDeferred(nameof(Initialize));
	}

	private void Initialize()
	{
		gameManager = GetNode<GameManager>("/root/Main/GameManager");
		player = gameManager?.Player;

		GD.Print($"WaveSpawner Initialize - GameManager: {gameManager != null}, Player: {player != null}");

		// Spawn initial wave (even if player is null, use default position)
		SpawnWave();
	}

	private void SpawnWave()
	{
		GD.Print($"Spawning wave of {EnemyCount} enemies...");

		for (int i = 0; i < EnemyCount; i++)
		{
			SpawnEnemy();
		}

		GD.Print($"Wave spawned: {EnemyCount} enemies");
	}

	private void SpawnEnemy()
	{
		Vector2 spawnPos = GetRandomSpawnPosition();
		var enemy = new BasicEnemy();
		enemy.GlobalPosition = spawnPos;
		enemy.Name = $"Enemy_{random.Next(1000, 9999)}";

		// Add to scene
		GetParent().AddChild(enemy);

		// Notify game manager
		gameManager?.OnEnemySpawned();

		GD.Print($"Enemy spawned at {spawnPos}");
	}

	private Vector2 GetRandomSpawnPosition()
	{
		// Spawn around the player at a safe distance
		Vector2 playerPos = player?.GlobalPosition ?? new Vector2(400, 300);

		// Random angle
		float angle = (float)(random.NextDouble() * Math.PI * 2);

		// Random distance from SpawnRadius to SpawnRadius * 1.5
		float distance = SpawnRadius + (float)(random.NextDouble() * SpawnRadius * 0.5f);

		// Calculate position
		Vector2 offset = new Vector2(
			Mathf.Cos(angle) * distance,
			Mathf.Sin(angle) * distance
		);

		Vector2 spawnPos = playerPos + offset;

		// Clamp to room bounds (100 to 700 for X, 100 to 500 for Y)
		spawnPos.X = Mathf.Clamp(spawnPos.X, 100, 700);
		spawnPos.Y = Mathf.Clamp(spawnPos.Y, 100, 500);

		return spawnPos;
	}
}
