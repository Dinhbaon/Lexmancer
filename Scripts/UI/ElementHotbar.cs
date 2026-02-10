using Godot;
using System;
using System.Collections.Generic;
using Lexmancer.Elements;
using Lexmancer.Core;
using Lexmancer.Services;

namespace Lexmancer.UI;

/// <summary>
/// Bottom hotbar showing equipped elements with key bindings (1-3)
/// </summary>
public partial class ElementHotbar : Control
{
	private PlayerElementInventory inventory;
	private List<Button> elementButtons = new();
	private Node player;
	private Node worldNode;

	private const int MaxSlots = 3;
	private List<int> equippedElements = new();
	private int activeSlot = 0; // Currently selected slot (0-2)

	public override void _Ready()
	{
		// Anchor to bottom-left of screen with margin
		SetAnchorsPreset(LayoutPreset.BottomLeft);
		OffsetLeft = 10;
		OffsetRight = 350;  // Width = 340
		OffsetTop = -70;    // 70px from bottom
		OffsetBottom = -10; // 10px from bottom
		GrowHorizontal = GrowDirection.End;
		GrowVertical = GrowDirection.Begin;

		// Subscribe to EventBus
		SubscribeToEvents();

		// Wait for initialization
		CallDeferred(nameof(Initialize));
	}

	public override void _ExitTree()
	{
		UnsubscribeFromEvents();
		base._ExitTree();
	}

	private void SubscribeToEvents()
	{
		if (EventBus.Instance != null)
		{
			EventBus.Instance.ElementAdded += OnElementChanged;
			EventBus.Instance.ElementConsumed += OnElementChanged;
			EventBus.Instance.ElementsCombined += OnElementsCombined;
			EventBus.Instance.HotbarEquipmentChanged += OnHotbarEquipmentChanged;
			GD.Print("ElementHotbar subscribed to EventBus");
		}
	}

	private void UnsubscribeFromEvents()
	{
		if (EventBus.Instance != null)
		{
			EventBus.Instance.ElementAdded -= OnElementChanged;
			EventBus.Instance.ElementConsumed -= OnElementChanged;
			EventBus.Instance.ElementsCombined -= OnElementsCombined;
			EventBus.Instance.HotbarEquipmentChanged -= OnHotbarEquipmentChanged;
		}
	}

	// Event handlers
	private void OnElementChanged(int elementId, int count)
	{
		RefreshHotbar();
	}

	private void OnElementsCombined(int element1Id, int element2Id, int newElementId)
	{
		RefreshHotbar();
	}

	private void OnHotbarEquipmentChanged(int slotIndex, int elementId)
	{
		RefreshHotbar();
	}

	private void Initialize()
	{
		// Get inventory from GameManager (temporary - will be refactored to service)
		var gameManager = GetNode<GameManager>("/root/Main/GameManager");
		if (gameManager != null)
		{
			inventory = gameManager.Inventory;
			player = gameManager.Player;
			worldNode = GetNode("/root/Main");

			RefreshHotbar();
		}
		else
		{
			// Fallback: find player and world directly
			player = GetTree().GetFirstNodeInGroup("player");
			worldNode = GetTree().Root.GetNode("Main");
		}
	}

	public override void _Input(InputEvent @event)
	{
		// Fire on left mouse click
		if (@event is InputEventMouseButton mouseButton &&
		    mouseButton.Pressed &&
		    mouseButton.ButtonIndex == MouseButton.Left)
		{
			// Fire the currently selected element
			if (equippedElements.Count > 0 && activeSlot < equippedElements.Count)
			{
				UseElementSlot(activeSlot);
				GetViewport().SetInputAsHandled();
			}
		}
		// Number keys select active slot (doesn't fire, just switches)
		else if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
		{
			if (keyEvent.Keycode == Key.Key1 && equippedElements.Count > 0)
			{
				activeSlot = 0;
				RefreshHotbar();
				EventBus.Instance?.EmitSignal(EventBus.SignalName.HotbarSlotSelected, 0);
				GD.Print("Selected element slot 1");
			}
			else if (keyEvent.Keycode == Key.Key2 && equippedElements.Count > 1)
			{
				activeSlot = 1;
				RefreshHotbar();
				EventBus.Instance?.EmitSignal(EventBus.SignalName.HotbarSlotSelected, 1);
				GD.Print("Selected element slot 2");
			}
			else if (keyEvent.Keycode == Key.Key3 && equippedElements.Count > 2)
			{
				activeSlot = 2;
				RefreshHotbar();
				EventBus.Instance?.EmitSignal(EventBus.SignalName.HotbarSlotSelected, 2);
				GD.Print("Selected element slot 3");
			}
		}
	}

	private void RefreshHotbar()
	{
		// Clear existing buttons
		foreach (var button in elementButtons)
		{
			button.QueueFree();
		}
		elementButtons.Clear();

		// Get equipped elements from inventory
		equippedElements = inventory.GetEquippedElements();

		// Create buttons for equipped elements (up to 3)
		for (int i = 0; i < equippedElements.Count; i++)
		{
			int elementId = equippedElements[i];
			int count = inventory.GetQuantity(elementId);

			var button = CreateElementButton(elementId, count, i);
			elementButtons.Add(button);
			AddChild(button);
		}

		// Clamp active slot to valid range
		if (activeSlot >= equippedElements.Count && equippedElements.Count > 0)
		{
			activeSlot = equippedElements.Count - 1;
		}
	}

	private Button CreateElementButton(int elementId, int count, int slotIndex)
	{
		// Get element data
		Element element = ServiceLocator.Instance.Elements.GetElement(elementId);

		var button = new Button();
		button.CustomMinimumSize = new Vector2(100, 50);
		button.Position = new Vector2(slotIndex * 110, 0);
		button.SizeFlagsHorizontal = SizeFlags.ExpandFill;

		// Set text with key binding
		string keyLabel = $"[{slotIndex + 1}]";
		string activeMarker = (slotIndex == activeSlot) ? "► " : "";
		button.Text = $"{activeMarker}{keyLabel} {element?.Name ?? "Unknown Element"}\n∞"; // Infinity symbol for unlimited

		// Set color based on element and highlight if active
		if (element != null)
		{
			var style = new StyleBoxFlat();
			float brightness = (slotIndex == activeSlot) ? 1.0f : 0.7f;
			style.BgColor = element.Color * brightness;
			style.BorderColor = (slotIndex == activeSlot) ? Colors.White : element.Color;
			style.BorderWidthLeft = (slotIndex == activeSlot) ? 3 : 2;
			style.BorderWidthRight = (slotIndex == activeSlot) ? 3 : 2;
			style.BorderWidthTop = (slotIndex == activeSlot) ? 3 : 2;
			style.BorderWidthBottom = (slotIndex == activeSlot) ? 3 : 2;
			button.AddThemeStyleboxOverride("normal", style);
		}

		// Connect press signal to select this slot (not fire)
		int index = slotIndex;
		button.Pressed += () => {
			activeSlot = index;
			RefreshHotbar();
			GD.Print($"Selected element slot {index + 1}");
		};

		return button;
	}

	private void UseElementSlot(int slotIndex)
	{
		if (slotIndex < 0 || slotIndex >= equippedElements.Count)
			return;

		int elementId = equippedElements[slotIndex];

		// Get element
		Element element = ServiceLocator.Instance.Elements.GetElement(elementId);

		if (element?.Ability == null)
		{
			GD.PrintErr($"❌ Element ID {elementId} has no ability!");
			GD.PrintErr($"   Element: {element}");
			GD.PrintErr($"   AbilityJson length: {element?.AbilityJson?.Length ?? 0}");
			if (!string.IsNullOrEmpty(element?.AbilityJson))
			{
				GD.PrintErr($"   First 200 chars: {element.AbilityJson.Substring(0, Math.Min(200, element.AbilityJson.Length))}");
			}
			return;
		}

		// Check if we have the element
		if (!inventory.HasElement(elementId))
		{
			GD.Print($"No {element.Name} (ID: {elementId}) remaining");
			return;
		}

		// Get direction from player position to mouse position
		Vector2 playerPos = player is Node2D p ? p.GlobalPosition : Vector2.Zero;

		// Use world-space mouse position when possible (ElementHotbar is a Control)
		Vector2 mousePos = player is Node2D p2 ? p2.GetGlobalMousePosition() : GetGlobalMousePosition();

		Vector2 direction = (mousePos - playerPos).Normalized();

		// Fallback to right if direction is zero
		if (direction.Length() < 0.1f)
			direction = Vector2.Right;

		// Cast ability (no element consumption - unlimited casts!)
		GD.Print($"✨ Using {element.Name} ability");
		GD.Print($"   Ability description: {element.Ability.Description}");
		GD.Print($"   Effects count: {element.Ability.Effects?.Count ?? 0}");

		// Emit event before casting
		EventBus.Instance?.EmitSignal(EventBus.SignalName.PlayerCastAbility, elementId);

		element.Ability.Execute(
			playerPos,
			direction,
			player,
			worldNode
		);
		GD.Print($"✓ Ability.Execute() completed");

		// NOTE: Elements are no longer consumed on cast - only on combine!
	}
}
