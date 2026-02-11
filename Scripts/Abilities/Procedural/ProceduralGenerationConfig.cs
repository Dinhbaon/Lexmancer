using Godot;

namespace Lexmancer.Abilities.Procedural;

/// <summary>
/// Centralized configuration for procedural ability generation creativity
/// Adjust these values to tune the variety and complexity of generated abilities
/// </summary>
public class ProceduralGenerationConfig
{
	// ==================== SINGLETON ====================

	private static ProceduralGenerationConfig _instance;
	public static ProceduralGenerationConfig Instance => _instance ??= new ProceduralGenerationConfig();

	// ==================== PROJECTILE SETTINGS ====================

	/// <summary>Minimum number of projectiles (default: 1)</summary>
	public int ProjectileCountMin { get; set; } = 1;

	/// <summary>Maximum number of projectiles (default: 4, increase to 6+ for barrages)</summary>
	public int ProjectileCountMax { get; set; } = 4;

	/// <summary>Minimum projectile speed (default: 300)</summary>
	public int ProjectileSpeedMin { get; set; } = 300;

	/// <summary>Maximum projectile speed (default: 600, increase for faster projectiles)</summary>
	public int ProjectileSpeedMax { get; set; } = 600;

	/// <summary>Piercing chance % when element prefers piercing (default: 50)</summary>
	public int PiercingChance { get; set; } = 50;

	// ==================== MELEE SETTINGS ====================

	/// <summary>Minimum melee range in tiles (default: 1.5)</summary>
	public float MeleeRangeMin { get; set; } = 1.5f;

	/// <summary>Maximum melee range in tiles (default: 3.0)</summary>
	public float MeleeRangeMax { get; set; } = 3.0f;

	/// <summary>Minimum arc angle in degrees (default: 60)</summary>
	public int ArcAngleMin { get; set; } = 60;

	/// <summary>Maximum arc angle in degrees (default: 180, increase to 270 for wide sweeps)</summary>
	public int ArcAngleMax { get; set; } = 180;

	/// <summary>Mobility chance % when element prefers mobility (default: 50)</summary>
	public int MobilityChance { get; set; } = 50;

	// ==================== AREA SETTINGS ====================

	/// <summary>Minimum area radius in tiles (default: 1.5)</summary>
	public float AreaRadiusMin { get; set; } = 1.5f;

	/// <summary>Maximum area radius in tiles (default: 3.5, increase for bigger zones)</summary>
	public float AreaRadiusMax { get; set; } = 3.5f;

	/// <summary>Minimum area duration in seconds (default: 1.0)</summary>
	public float AreaDurationMin { get; set; } = 1.0f;

	/// <summary>Maximum area duration in seconds (default: 4.0, increase for longer effects)</summary>
	public float AreaDurationMax { get; set; } = 4.0f;

	/// <summary>Lingering damage chance % when element is persistent (default: 50)</summary>
	public int LingeringDamageChance { get; set; } = 50;

	/// <summary>Lingering damage multiplier of base damage (default: 0.4 = 40%)</summary>
	public float LingeringDamageMultiplier { get; set; } = 0.4f;

	// ==================== COMPLEXITY LAYERS ====================

	/// <summary>Chaining chance % when element prefers chaining (default: 50)</summary>
	public int ChainingChance { get; set; } = 50;

	/// <summary>Minimum chain targets (default: 2)</summary>
	public int ChainTargetsMin { get; set; } = 2;

	/// <summary>Maximum chain targets (default: 4, increase to 6+ for multi-target chaos)</summary>
	public int ChainTargetsMax { get; set; } = 4;

	/// <summary>Maximum chain distance (default: 150-250 units)</summary>
	public int ChainDistanceMin { get; set; } = 150;

	/// <summary>Maximum chain distance variance (default: 100)</summary>
	public int ChainDistanceVariance { get; set; } = 100;

	/// <summary>Minimum chain damage multiplier (default: 0.6 = 60%)</summary>
	public float ChainDamageMultiplierMin { get; set; } = 0.6f;

	/// <summary>Chain damage multiplier variance (default: 0.2, so 60-80%)</summary>
	public float ChainDamageMultiplierVariance { get; set; } = 0.2f;

	/// <summary>Explosion chance % when element is explosive (default: 50)</summary>
	public int ExplosionChance { get; set; } = 50;

	/// <summary>Minimum explosion radius (default: 80)</summary>
	public int ExplosionRadiusMin { get; set; } = 80;

	/// <summary>Explosion radius variance (default: 60)</summary>
	public int ExplosionRadiusVariance { get; set; } = 60;

	/// <summary>Minimum explosion duration (default: 1.0s)</summary>
	public float ExplosionDurationMin { get; set; } = 1.0f;

	/// <summary>Explosion duration variance (default: 1.5s)</summary>
	public float ExplosionDurationVariance { get; set; } = 1.5f;

	/// <summary>Explosion lingering damage multiplier (default: 0.3 = 30% of base damage)</summary>
	public float ExplosionDamageMultiplier { get; set; } = 0.3f;

	/// <summary>On-expire effect chance % for area abilities (default: 50)</summary>
	public int OnExpireChance { get; set; } = 50;

	/// <summary>On-expire damage multiplier (default: 0.5 = 50% of base damage)</summary>
	public float OnExpireDamageMultiplier { get; set; } = 0.5f;

	/// <summary>Chance % to add a conditional to a generated action (default: 20)</summary>
	public int ConditionalChance { get; set; } = 20;

	/// <summary>Health threshold for conditional checks (default: 0.5 = 50%)</summary>
	public float ConditionalHealthThreshold { get; set; } = 0.5f;

	// ==================== STATUS EFFECTS ====================

	/// <summary>Secondary status effect chance % (default: 50)</summary>
	public int SecondaryStatusChance { get; set; } = 50;

	// ==================== COOLDOWN CALCULATION ====================

	/// <summary>Minimum cooldown in seconds (default: 0.5)</summary>
	public float CooldownMin { get; set; } = 0.5f;

	/// <summary>Maximum cooldown in seconds (default: 3.0)</summary>
	public float CooldownMax { get; set; } = 3.0f;

	/// <summary>Cooldown multiplier for complexity (chains, explosions, etc.) (default: 1.0)</summary>
	public float CooldownComplexityMultiplier { get; set; } = 1.0f;

	// ==================== PRESETS ====================

	/// <summary>Apply "High Creativity" preset (more variety, complexity, chaos)</summary>
	public void ApplyHighCreativityPreset()
	{
		ProjectileCountMax = 6;
		ProjectileSpeedMax = 700;
		PiercingChance = 80;

		ArcAngleMax = 270;
		MobilityChance = 90;

		AreaRadiusMax = 5.0f;
		AreaDurationMax = 6.0f;

		ChainingChance = 60;
		ChainTargetsMax = 6;

		ExplosionChance = 40;
		OnExpireChance = 30;

		SecondaryStatusChance = 50;

		GD.Print("Applied HIGH CREATIVITY preset - abilities will be more varied and complex!");
	}

	/// <summary>Apply "Low Creativity" preset (simple, predictable abilities)</summary>
	public void ApplyLowCreativityPreset()
	{
		ProjectileCountMax = 2;
		PiercingChance = 30;

		ArcAngleMax = 120;
		MobilityChance = 40;

		AreaRadiusMax = 2.5f;
		AreaDurationMax = 3.0f;

		ChainingChance = 10;
		ChainTargetsMax = 2;

		ExplosionChance = 5;
		OnExpireChance = 5;

		SecondaryStatusChance = 10;

		GD.Print("Applied LOW CREATIVITY preset - abilities will be simpler and more predictable");
	}

	/// <summary>Apply "Balanced" preset (default values)</summary>
	public void ApplyBalancedPreset()
	{
		// Reset to defaults (current values)
		_instance = new ProceduralGenerationConfig();
		GD.Print("Applied BALANCED preset - default creativity settings");
	}

	// ==================== UTILITY ====================

	/// <summary>Print all current settings to console</summary>
	public void PrintSettings()
	{
		GD.Print("=== Procedural Generation Settings ===");
		GD.Print($"Projectile Count: {ProjectileCountMin}-{ProjectileCountMax}");
		GD.Print($"Projectile Speed: {ProjectileSpeedMin}-{ProjectileSpeedMax}");
		GD.Print($"Piercing Chance: {PiercingChance}%");
		GD.Print($"Melee Range: {MeleeRangeMin}-{MeleeRangeMax} tiles");
		GD.Print($"Arc Angle: {ArcAngleMin}-{ArcAngleMax}°");
		GD.Print($"Area Radius: {AreaRadiusMin}-{AreaRadiusMax} tiles");
		GD.Print($"Chaining Chance: {ChainingChance}% (targets: {ChainTargetsMin}-{ChainTargetsMax})");
		GD.Print($"Explosion Chance: {ExplosionChance}%");
		GD.Print($"On-Expire Chance: {OnExpireChance}%");
		GD.Print($"Secondary Status: {SecondaryStatusChance}%");
		GD.Print($"Cooldown Range: {CooldownMin}-{CooldownMax}s");
		GD.Print("======================================");
	}
}
