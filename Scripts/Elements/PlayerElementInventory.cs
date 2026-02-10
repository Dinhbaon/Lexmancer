using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Lexmancer.Core;

namespace Lexmancer.Elements;

/// <summary>
/// Player's consumable element inventory
/// Elements are consumed when combined (not when cast)
///
/// Migration note: This class now emits EventBus signals in addition to C# events
/// for backward compatibility. New code should listen to EventBus instead.
/// </summary>
public class PlayerElementInventory
{
	private Dictionary<int, int> quantities = new();
	private List<int> equippedElements = new(); // Max 3 equipped elements

	public const int MaxEquippedSlots = 3;

	// Legacy C# events - kept for backward compatibility during migration
	[Obsolete("Use EventBus.ElementAdded instead")]
	public event Action<int, int> OnElementAdded;

	[Obsolete("Use EventBus.ElementConsumed instead")]
	public event Action<int, int> OnElementConsumed;

	[Obsolete("Use EventBus.ElementsCombined instead")]
	public event Action<int> OnElementCombined;

	[Obsolete("Use EventBus.HotbarEquipmentChanged instead")]
	public event Action OnEquipmentChanged; // Fired when equipped elements change

	/// <summary>
	/// Add elements to inventory
	/// </summary>
	public void AddElement(int elementId, int count = 1)
	{
		if (elementId <= 0 || count <= 0)
			return;

		bool isNew = !quantities.ContainsKey(elementId);

		if (quantities.ContainsKey(elementId))
			quantities[elementId] += count;
		else
			quantities[elementId] = count;

		// Auto-equip new elements if there's room
		if (isNew && equippedElements.Count < MaxEquippedSlots)
		{
			equippedElements.Add(elementId);
			OnEquipmentChanged?.Invoke(); // Legacy
			EventBus.Instance?.EmitSignal(EventBus.SignalName.HotbarEquipmentChanged, equippedElements.Count - 1, elementId);
		}

		OnElementAdded?.Invoke(elementId, count); // Legacy
		EventBus.Instance?.EmitSignal(EventBus.SignalName.ElementAdded, elementId, count);
		GD.Print($"Added {count}x element ID {elementId} (now have {quantities[elementId]})");
	}

	/// <summary>
	/// Check if player has element(s)
	/// </summary>
	public bool HasElement(int elementId, int count = 1)
	{
		if (elementId <= 0)
			return false;

		return quantities.TryGetValue(elementId, out int current) && current >= count;
	}

	/// <summary>
	/// Consume element(s) from inventory
	/// Returns true if successful, false if not enough
	/// </summary>
	public bool ConsumeElement(int elementId, int count = 1)
	{
		if (!HasElement(elementId, count))
			return false;

		quantities[elementId] -= count;

		if (quantities[elementId] <= 0)
		{
			quantities.Remove(elementId);
			// Auto-unequip if removed from inventory
			if (equippedElements.Contains(elementId))
			{
				int slotIndex = equippedElements.IndexOf(elementId);
				equippedElements.Remove(elementId);
				OnEquipmentChanged?.Invoke(); // Legacy
				EventBus.Instance?.EmitSignal(EventBus.SignalName.HotbarEquipmentChanged, slotIndex, -1);
			}
		}

		OnElementConsumed?.Invoke(elementId, count); // Legacy
		EventBus.Instance?.EmitSignal(EventBus.SignalName.ElementConsumed, elementId, count);
		GD.Print($"Consumed {count}x element ID {elementId}");
		return true;
	}

	/// <summary>
	/// Get quantity of specific element
	/// </summary>
	public int GetQuantity(int elementId)
	{
		return quantities.TryGetValue(elementId, out int count) ? count : 0;
	}

	/// <summary>
	/// Check if two elements can be combined
	/// Any two elements can be combined (no hardcoded recipes)
	/// </summary>
	public bool CanCombine(int element1Id, int element2Id)
	{
		if (element1Id <= 0 || element2Id <= 0)
			return false;

		// Check if we have both elements
		return HasElement(element1Id) && HasElement(element2Id);
	}

	/// <summary>
	/// Combine two elements (consumes originals, creates new element)
	/// Returns the created element, or null if failed
	/// NOTE: Result element is provided by caller (usually from LLM)
	/// </summary>
	public Element CombineElements(int element1Id, int element2Id, Element resultElement)
	{
		if (!CanCombine(element1Id, element2Id))
		{
			GD.PrintErr($"Cannot combine element {element1Id} + {element2Id}");
			return null;
		}

		if (resultElement == null)
		{
			GD.PrintErr($"No result element provided for {element1Id} + {element2Id}");
			return null;
		}

		// Consume ingredients
		ConsumeElement(element1Id, 1);
		ConsumeElement(element2Id, 1);

		// Add result to inventory
		AddElement(resultElement.Id, 1);

		OnElementCombined?.Invoke(resultElement.Id); // Legacy
		EventBus.Instance?.EmitSignal(EventBus.SignalName.ElementsCombined, element1Id, element2Id, resultElement.Id);
		GD.Print($"Combined element {element1Id} + {element2Id} = {resultElement.Name}!");

		return resultElement;
	}

	/// <summary>
	/// Get all elements and their quantities
	/// </summary>
	public List<(int id, int count)> GetAll()
	{
		return quantities
			.OrderBy(kvp => kvp.Key)
			.Select(kvp => (kvp.Key, kvp.Value))
			.ToList();
	}

	/// <summary>
	/// Get all element IDs
	/// </summary>
	public List<int> GetElementIds()
	{
		return quantities.Keys.OrderBy(k => k).ToList();
	}

	/// <summary>
	/// Equip an element to the hotbar
	/// </summary>
	public bool EquipElement(int elementId)
	{
		if (elementId <= 0)
			return false;

		// Must have the element in inventory
		if (!HasElement(elementId))
			return false;

		// Already equipped?
		if (equippedElements.Contains(elementId))
			return true;

		// Check if we have room
		if (equippedElements.Count >= MaxEquippedSlots)
		{
			GD.Print($"Cannot equip element {elementId}: all {MaxEquippedSlots} slots full");
			return false;
		}

		equippedElements.Add(elementId);
		OnEquipmentChanged?.Invoke(); // Legacy
		EventBus.Instance?.EmitSignal(EventBus.SignalName.HotbarEquipmentChanged, equippedElements.Count - 1, elementId);
		GD.Print($"Equipped element {elementId}");
		return true;
	}

	/// <summary>
	/// Unequip an element from the hotbar
	/// </summary>
	public bool UnequipElement(int elementId)
	{
		if (elementId <= 0)
			return false;

		if (!equippedElements.Contains(elementId))
			return false;

		int slotIndex = equippedElements.IndexOf(elementId);
		equippedElements.Remove(elementId);
		OnEquipmentChanged?.Invoke(); // Legacy
		EventBus.Instance?.EmitSignal(EventBus.SignalName.HotbarEquipmentChanged, slotIndex, -1);
		GD.Print($"Unequipped element {elementId}");
		return true;
	}

	/// <summary>
	/// Check if an element is equipped
	/// </summary>
	public bool IsEquipped(int elementId)
	{
		return equippedElements.Contains(elementId);
	}

	/// <summary>
	/// Get equipped elements (max 3)
	/// </summary>
	public List<int> GetEquippedElements()
	{
		return new List<int>(equippedElements);
	}

	/// <summary>
	/// Clear all elements
	/// </summary>
	public void Clear()
	{
		quantities.Clear();
		equippedElements.Clear();
		GD.Print("Inventory cleared");
	}

	/// <summary>
	/// Print inventory to console
	/// </summary>
	public void PrintInventory()
	{
		GD.Print("=== Player Inventory ===");
		if (quantities.Count == 0)
		{
			GD.Print("  (empty)");
		}
		else
		{
			foreach (var (id, count) in GetAll())
			{
				GD.Print($"  {id}: {count}");
			}
		}
		GD.Print("========================");
	}

	/// <summary>
	/// Get total element count
	/// </summary>
	public int GetTotalCount()
	{
		return quantities.Values.Sum();
	}
}
