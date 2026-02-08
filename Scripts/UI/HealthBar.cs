using Godot;
using System;
using Lexmancer.Combat;

namespace Lexmancer.UI;

/// <summary>
/// Health bar that can be attached to player or enemies
/// </summary>
public partial class HealthBar : Control
{
	[Export] public bool IsPlayerHealthBar { get; set; } = false;

	private HealthComponent healthComponent;
	private ColorRect background;
	private ColorRect fillBar;
	private Label healthLabel;

	public override void _Ready()
	{
		if (IsPlayerHealthBar)
		{
			CreatePlayerHealthBar();
		}
		else
		{
			CreateEntityHealthBar();
		}
	}

	/// <summary>
	/// Create health bar for player (top-left corner)
	/// </summary>
	private void CreatePlayerHealthBar()
	{
		// Anchor to top-left with margin
		SetAnchorsPreset(LayoutPreset.TopLeft);
		OffsetLeft = 10;
		OffsetTop = 10;
		OffsetRight = 210;   // Width = 200
		OffsetBottom = 40;   // Height = 30
		GrowHorizontal = GrowDirection.End;
		GrowVertical = GrowDirection.End;

		CreateBarComponents();

		// Wait for game manager
		CallDeferred(nameof(ConnectToPlayerHealth));
	}

	/// <summary>
	/// Create health bar for entity (above entity)
	/// </summary>
	private void CreateEntityHealthBar()
	{
		// Center above entity (relative positioning)
		AnchorLeft = 0.5f;
		AnchorTop = 0.5f;
		AnchorRight = 0.5f;
		AnchorBottom = 0.5f;
		OffsetLeft = -20;
		OffsetTop = -30;
		OffsetRight = 20;    // Width = 40
		OffsetBottom = -25;  // Height = 5
		GrowHorizontal = GrowDirection.Both;
		GrowVertical = GrowDirection.Both;
		ZIndex = 10;

		CreateBarComponents(showLabel: false);

		// Connect to parent's health component
		CallDeferred(nameof(ConnectToEntityHealth));
	}

	private void CreateBarComponents(bool showLabel = true)
	{
		// Background
		background = new ColorRect();
		background.Color = new Color(0.2f, 0.2f, 0.2f);
		background.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(background);

		// Fill bar
		fillBar = new ColorRect();
		fillBar.Color = new Color(0, 1, 0); // Green
		fillBar.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(fillBar);

		// Label (for player only)
		if (showLabel)
		{
			healthLabel = new Label();
			healthLabel.SetAnchorsPreset(LayoutPreset.FullRect);
			healthLabel.HorizontalAlignment = HorizontalAlignment.Center;
			healthLabel.VerticalAlignment = VerticalAlignment.Center;
			healthLabel.AddThemeColorOverride("font_color", Colors.White);
			AddChild(healthLabel);
		}
	}

	private void ConnectToPlayerHealth()
	{
		var gameManager = GetNode<GameManager>("/root/Main/GameManager");
		if (gameManager?.PlayerHealth != null)
		{
			healthComponent = gameManager.PlayerHealth;
			healthComponent.OnHealthChanged += UpdateHealthBar;
			UpdateHealthBar(healthComponent.Current, healthComponent.Max);
		}
	}

	private void ConnectToEntityHealth()
	{
		// Get health component from parent's children
		var parent = GetParent();
		if (parent != null)
		{
			var hc = parent.GetNodeOrNull<HealthComponent>("HealthComponent");
			if (hc != null)
			{
				healthComponent = hc;
				healthComponent.OnHealthChanged += UpdateHealthBar;
				UpdateHealthBar(healthComponent.Current, healthComponent.Max);
			}
		}
	}

	private void UpdateHealthBar(float current, float max)
	{
		if (fillBar == null)
			return;

		float percentage = max > 0 ? current / max : 0;

		// Update fill bar width using anchors
		fillBar.AnchorRight = percentage;

		// Update color based on health percentage
		if (percentage > 0.6f)
			fillBar.Color = new Color(0, 1, 0); // Green
		else if (percentage > 0.3f)
			fillBar.Color = new Color(1, 1, 0); // Yellow
		else
			fillBar.Color = new Color(1, 0, 0); // Red

		// Update label (player only)
		if (healthLabel != null)
		{
			healthLabel.Text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
		}
	}

	/// <summary>
	/// Attach health component directly
	/// </summary>
	public void SetHealthComponent(HealthComponent health)
	{
		healthComponent = health;
		healthComponent.OnHealthChanged += UpdateHealthBar;
		UpdateHealthBar(healthComponent.Current, healthComponent.Max);
	}
}
