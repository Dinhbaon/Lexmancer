using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lexmancer.Elements;

/// <summary>
/// Player's consumable element inventory
/// Elements are consumed when combined (not when cast)
/// </summary>
public class PlayerElementInventory
{
	private Dictionary<string, int> quantities = new();
	private List<string> equippedElements = new(); // Max 3 equipped elements

	public const int MaxEquippedSlots = 3;

	public event Action<string, int> OnElementAdded;
	public event Action<string, int> OnElementConsumed;
	public event Action<string> OnElementCombined;
	public event Action OnEquipmentChanged; // Fired when equipped elements change

	/// <summary>
	/// Add elements to inventory
	/// </summary>
	public void AddElement(string elementId, int count = 1)
	{
		if (string.IsNullOrEmpty(elementId) || count <= 0)
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
			OnEquipmentChanged?.Invoke();
		}

		OnElementAdded?.Invoke(elementId, count);
		GD.Print($"Added {count}x {elementId} (now have {quantities[elementId]})");
	}

	/// <summary>
	/// Check if player has element(s)
	/// </summary>
	public bool HasElement(string elementId, int count = 1)
	{
		if (string.IsNullOrEmpty(elementId))
			return false;

		return quantities.TryGetValue(elementId, out int current) && current >= count;
	}

	/// <summary>
	/// Consume element(s) from inventory
	/// Returns true if successful, false if not enough
	/// </summary>
	public bool ConsumeElement(string elementId, int count = 1)
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
				equippedElements.Remove(elementId);
				OnEquipmentChanged?.Invoke();
			}
		}

		OnElementConsumed?.Invoke(elementId, count);
		GD.Print($"Consumed {count}x {elementId}");
		return true;
	}

	/// <summary>
	/// Get quantity of specific element
	/// </summary>
	public int GetQuantity(string elementId)
	{
		return quantities.TryGetValue(elementId, out int count) ? count : 0;
	}

	/// <summary>
	/// Check if two elements can be combined
	/// Any two elements can be combined (no hardcoded recipes)
	/// </summary>
	public bool CanCombine(string element1, string element2)
	{
		if (string.IsNullOrEmpty(element1) || string.IsNullOrEmpty(element2))
			return false;

		// Check if we have both elements
		return HasElement(element1) && HasElement(element2);
	}

	/// <summary>
	/// Combine two elements (consumes originals, creates new element)
	/// Returns the created element, or null if failed
	/// NOTE: Result element is provided by caller (usually from LLM)
	/// </summary>
	public Element CombineElements(string element1, string element2, Element resultElement)
	{
		if (!CanCombine(element1, element2))
		{
			GD.PrintErr($"Cannot combine {element1} + {element2}");
			return null;
		}

		if (resultElement == null)
		{
			GD.PrintErr($"No result element provided for {element1} + {element2}");
			return null;
		}

		// Consume ingredients
		ConsumeElement(element1, 1);
		ConsumeElement(element2, 1);

		// Add result to inventory
		AddElement(resultElement.Id, 1);

		OnElementCombined?.Invoke(resultElement.Id);
		GD.Print($"Combined {element1} + {element2} = {resultElement.Name}!");

		return resultElement;
	}

	/// <summary>
	/// Get all elements and their quantities
	/// </summary>
	public List<(string id, int count)> GetAll()
	{
		return quantities
			.OrderBy(kvp => kvp.Key)
			.Select(kvp => (kvp.Key, kvp.Value))
			.ToList();
	}

	/// <summary>
	/// Get all element IDs
	/// </summary>
	public List<string> GetElementIds()
	{
		return quantities.Keys.OrderBy(k => k).ToList();
	}

	/// <summary>
	/// Equip an element to the hotbar
	/// </summary>
	public bool EquipElement(string elementId)
	{
		if (string.IsNullOrEmpty(elementId))
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
			GD.Print($"Cannot equip {elementId}: all {MaxEquippedSlots} slots full");
			return false;
		}

		equippedElements.Add(elementId);
		OnEquipmentChanged?.Invoke();
		GD.Print($"Equipped {elementId}");
		return true;
	}

	/// <summary>
	/// Unequip an element from the hotbar
	/// </summary>
	public bool UnequipElement(string elementId)
	{
		if (string.IsNullOrEmpty(elementId))
			return false;

		if (!equippedElements.Contains(elementId))
			return false;

		equippedElements.Remove(elementId);
		OnEquipmentChanged?.Invoke();
		GD.Print($"Unequipped {elementId}");
		return true;
	}

	/// <summary>
	/// Check if an element is equipped
	/// </summary>
	public bool IsEquipped(string elementId)
	{
		return equippedElements.Contains(elementId);
	}

	/// <summary>
	/// Get equipped elements (max 3)
	/// </summary>
	public List<string> GetEquippedElements()
	{
		return new List<string>(equippedElements);
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
