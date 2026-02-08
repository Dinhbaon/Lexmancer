using Godot;
using System;

namespace Lexmancer.Combat;

/// <summary>
/// Reusable health component for player and enemies
/// </summary>
public partial class HealthComponent : Node
{
	public float Current { get; private set; }
	public float Max { get; private set; }

	public event Action<float> OnDamaged;
	public event Action OnDeath;
	public event Action<float> OnHealed;
	public event Action<float, float> OnHealthChanged;

	public bool IsAlive => Current > 0;
	public float HealthPercentage => Max > 0 ? Current / Max : 0;

	public HealthComponent()
	{
		Name = "HealthComponent";
	}

	public void Initialize(float maxHealth)
	{
		Max = maxHealth;
		Current = maxHealth;
	}

	/// <summary>
	/// Take damage
	/// </summary>
	public void TakeDamage(float amount)
	{
		if (!IsAlive || amount <= 0)
			return;

		float previousHealth = Current;
		Current -= amount;

		if (Current < 0)
			Current = 0;

		OnDamaged?.Invoke(amount);
		OnHealthChanged?.Invoke(Current, Max);

		GD.Print($"Took {amount} damage. Health: {Current}/{Max}");

		if (Current <= 0 && previousHealth > 0)
		{
			OnDeath?.Invoke();
			GD.Print("Died!");
		}
	}

	/// <summary>
	/// Heal health
	/// </summary>
	public void Heal(float amount)
	{
		if (!IsAlive || amount <= 0)
			return;

		Current += amount;

		if (Current > Max)
			Current = Max;

		OnHealed?.Invoke(amount);
		OnHealthChanged?.Invoke(Current, Max);

		GD.Print($"Healed {amount}. Health: {Current}/{Max}");
	}

	/// <summary>
	/// Set max health (and optionally heal to full)
	/// </summary>
	public void SetMaxHealth(float newMax, bool healToFull = false)
	{
		Max = newMax;

		if (healToFull)
			Current = Max;
		else if (Current > Max)
			Current = Max;

		OnHealthChanged?.Invoke(Current, Max);
	}

	/// <summary>
	/// Reset to full health
	/// </summary>
	public void ResetToFull()
	{
		Current = Max;
		OnHealthChanged?.Invoke(Current, Max);
	}
}
