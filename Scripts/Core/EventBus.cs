using Godot;
using System;

namespace Lexmancer.Core;

/// <summary>
/// Global event bus for decoupled communication between game systems.
/// This is a Godot autoload singleton - configure in Project Settings > Autoload.
///
/// Usage:
///   Emit: EventBus.Instance.EmitSignal(EventBus.SignalName.PlayerDied);
///   Listen: EventBus.Instance.PlayerDied += OnPlayerDied;
///   Unlisten: EventBus.Instance.PlayerDied -= OnPlayerDied;
/// </summary>
public partial class EventBus : Node
{
	// Singleton instance (set by Godot autoload system)
	private static EventBus _instance;
	public static EventBus Instance
	{
		get
		{
			if (_instance == null)
			{
				GD.PrintErr("EventBus not initialized! Make sure it's configured as an autoload in Project Settings.");
			}
			return _instance;
		}
	}

	public override void _EnterTree()
	{
		if (_instance != null && _instance != this)
		{
			GD.PrintErr("Multiple EventBus instances detected! Only one should exist as autoload.");
			QueueFree();
			return;
		}
		_instance = this;
		GD.Print("EventBus initialized");
	}

	public override void _ExitTree()
	{
		if (_instance == this)
		{
			_instance = null;
		}
	}

	// ==================== PLAYER EVENTS ====================

	/// <summary>Emitted when player takes damage</summary>
	[Signal] public delegate void PlayerDamagedEventHandler(float amount, string damageType);

	/// <summary>Emitted when player dies</summary>
	[Signal] public delegate void PlayerDiedEventHandler();

	/// <summary>Emitted when player health changes</summary>
	[Signal] public delegate void PlayerHealthChangedEventHandler(float currentHealth, float maxHealth);

	/// <summary>Emitted when player casts an ability</summary>
	[Signal] public delegate void PlayerCastAbilityEventHandler(int elementId);

	// ==================== ENEMY EVENTS ====================

	/// <summary>Emitted when any enemy is spawned</summary>
	[Signal] public delegate void EnemySpawnedEventHandler(Node enemy);

	/// <summary>Emitted when any enemy dies</summary>
	[Signal] public delegate void EnemyDiedEventHandler(Node enemy, Vector2 position);

	/// <summary>Emitted when all enemies in current wave are dead</summary>
	[Signal] public delegate void AllEnemiesDefeatedEventHandler();

	/// <summary>Emitted when a new wave starts</summary>
	[Signal] public delegate void WaveStartedEventHandler(int waveNumber, int enemyCount);

	// ==================== ELEMENT/INVENTORY EVENTS ====================

	/// <summary>Emitted when an element is added to inventory</summary>
	[Signal] public delegate void ElementAddedEventHandler(int elementId, int count);

	/// <summary>Emitted when an element is consumed from inventory</summary>
	[Signal] public delegate void ElementConsumedEventHandler(int elementId, int count);

	/// <summary>Emitted when two elements are combined to create a new one</summary>
	[Signal] public delegate void ElementsCombinedEventHandler(int element1Id, int element2Id, int newElementId);

	/// <summary>Emitted when element combination fails</summary>
	[Signal] public delegate void ElementCombinationFailedEventHandler(int element1Id, int element2Id, string reason);

	/// <summary>Emitted when hotbar equipment changes</summary>
	[Signal] public delegate void HotbarEquipmentChangedEventHandler(int slotIndex, int elementId);

	/// <summary>Emitted when selected hotbar slot changes</summary>
	[Signal] public delegate void HotbarSlotSelectedEventHandler(int slotIndex);

	// ==================== COMBAT EVENTS ====================

	/// <summary>Emitted when any entity takes damage</summary>
	[Signal] public delegate void EntityDamagedEventHandler(Node entity, float amount, string damageType, Vector2 position);

	/// <summary>Emitted when any entity dies</summary>
	[Signal] public delegate void EntityDiedEventHandler(Node entity, Vector2 position);

	/// <summary>Emitted when a status effect is applied</summary>
	[Signal] public delegate void StatusEffectAppliedEventHandler(Node entity, string statusType, float duration);

	/// <summary>Emitted when a status effect expires</summary>
	[Signal] public delegate void StatusEffectExpiredEventHandler(Node entity, string statusType);

	/// <summary>Emitted when knockback is applied to an entity</summary>
	[Signal] public delegate void KnockbackAppliedEventHandler(Node entity, Vector2 direction, float force);

	// ==================== GAME STATE EVENTS ====================

	/// <summary>Emitted when game state changes</summary>
	[Signal] public delegate void GameStateChangedEventHandler(string newState); // "playing", "paused", "victory", "defeat"

	/// <summary>Emitted when game is paused</summary>
	[Signal] public delegate void GamePausedEventHandler();

	/// <summary>Emitted when game is unpaused</summary>
	[Signal] public delegate void GameUnpausedEventHandler();

	/// <summary>Emitted when game is restarting</summary>
	[Signal] public delegate void GameRestartingEventHandler();

	// ==================== UI EVENTS ====================

	/// <summary>Emitted when combination panel is opened</summary>
	[Signal] public delegate void CombinationPanelOpenedEventHandler();

	/// <summary>Emitted when combination panel is closed</summary>
	[Signal] public delegate void CombinationPanelClosedEventHandler();

	/// <summary>Emitted when game over screen should be shown</summary>
	[Signal] public delegate void ShowGameOverScreenEventHandler(string message, bool isVictory);

	// ==================== LLM EVENTS ====================

	/// <summary>Emitted when LLM generation starts</summary>
	[Signal] public delegate void LLMGenerationStartedEventHandler(int element1Id, int element2Id);

	/// <summary>Emitted when LLM generation completes successfully</summary>
	[Signal] public delegate void LLMGenerationCompletedEventHandler(int newElementId);

	/// <summary>Emitted when LLM generation fails</summary>
	[Signal] public delegate void LLMGenerationFailedEventHandler(string errorMessage);

	/// <summary>Emitted when LLM service initialization status changes</summary>
	[Signal] public delegate void LLMServiceStatusChangedEventHandler(bool isReady, string statusMessage);

	/// <summary>Emitted when LLM flavor text is updated for an element (async update)</summary>
	[Signal] public delegate void ElementFlavorUpdatedEventHandler(int elementId);

	// ==================== ABILITY EVENTS ====================

	/// <summary>Emitted when a projectile is spawned</summary>
	[Signal] public delegate void ProjectileSpawnedEventHandler(Node projectile, Vector2 position, Vector2 direction);

	/// <summary>Emitted when a projectile hits something</summary>
	[Signal] public delegate void ProjectileHitEventHandler(Node projectile, Node target, Vector2 position);

	/// <summary>Emitted when an area effect is created</summary>
	[Signal] public delegate void AreaEffectCreatedEventHandler(Node areaEffect, Vector2 position, float radius);

	/// <summary>Emitted when a melee attack is executed</summary>
	[Signal] public delegate void MeleeAttackExecutedEventHandler(Node attacker, Vector2 position, string shape);

	// ==================== HELPER METHODS ====================

	/// <summary>
	/// Safely emit a signal with error handling
	/// </summary>
	public void SafeEmit(StringName signalName, params Variant[] args)
	{
		if (!IsInsideTree())
		{
			GD.PrintErr($"EventBus: Cannot emit signal '{signalName}' - not in tree");
			return;
		}

		try
		{
			EmitSignal(signalName, args);
		}
		catch (Exception ex)
		{
			GD.PrintErr($"EventBus: Error emitting signal '{signalName}': {ex.Message}");
		}
	}

	/// <summary>
	/// Check if anyone is listening to a signal
	/// </summary>
	public bool HasListeners(StringName signalName)
	{
		var connections = GetSignalConnectionList(signalName);
		return connections.Count > 0;
	}

	/// <summary>
	/// Print debug info about all signal connections
	/// </summary>
	public void PrintConnectionStats()
	{
		GD.Print("=== EventBus Connection Statistics ===");

		var signalList = GetSignalList();
		foreach (var signalInfo in signalList)
		{
			var signalName = signalInfo["name"].AsStringName();
			var connections = GetSignalConnectionList(signalName);

			if (connections.Count > 0)
			{
				GD.Print($"{signalName}: {connections.Count} listener(s)");
			}
		}

		GD.Print("======================================");
	}
}
