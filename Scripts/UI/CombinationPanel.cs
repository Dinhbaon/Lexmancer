using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lexmancer.Elements;

namespace Lexmancer.UI;

/// <summary>
/// Panel for combining elements (opened with TAB key)
/// </summary>
public partial class CombinationPanel : Control
{
	private PlayerElementInventory inventory;
	private LLMElementGenerator llmGenerator;
	private int selectedElement1;
	private int selectedElement2;

	// Tab container
	private TabContainer tabContainer;

	// Combination tab elements
	private Label titleLabel;
	private VBoxContainer elementList1;
	private VBoxContainer elementList2;
	private Button combineButton;
	private Label resultLabel;
	private Label llmOutputLabel;
	private CheckBox useLLMCheckbox;

	// Cached combination UI
	private VBoxContainer cachedCombinationContainer;
	private Label cachedElementLabel;
	private Button useCachedButton;
	private Button generateNewButton;

	// Inventory tab elements
	private VBoxContainer inventoryElementList;
	private ScrollContainer inventoryScrollContainer;

	// Test tab elements
	private TextEdit jsonInput;
	private Label testResultLabel;
	private Button testJsonButton;
	private Button clearDatabaseButton;

	private bool isOpen = false;
	private bool useLLM = true; // Toggle for LLM generation

	public override void _Ready()
	{
		// Position in center of screen (will be updated in CreateUI)
		Visible = false;

		// Allow processing while game is paused
		ProcessMode = ProcessModeEnum.Always;

		// Create UI
		CreateUI();

		// Wait for game manager
		CallDeferred(nameof(Initialize));
	}

	private void Initialize()
	{
		// Get inventory from GameManager
		var gameManager = GetNode<GameManager>("/root/Main/GameManager");
		if (gameManager != null)
		{
			inventory = gameManager.Inventory;

			// Subscribe to inventory changes
			inventory.OnElementAdded += (id, count) => { if (isOpen) RefreshAll(); };
			inventory.OnElementConsumed += (id, count) => { if (isOpen) RefreshAll(); };
			inventory.OnElementCombined += (id) => { /* Handled in OnCombinePressed */ };
			inventory.OnEquipmentChanged += () => { if (isOpen) RefreshInventoryList(); };
		}

		// Initialize LLM generator
		try
		{
			llmGenerator = new LLMElementGenerator(
				playerId: "player_001",
				useLLM: true,
				llmBaseUrl: "http://localhost:11434",
				llmModel: "qwen2.5:7b"
			);
			GD.Print("LLM Generator initialized successfully");
		}
		catch (Exception ex)
		{
			GD.PrintErr($"Failed to initialize LLM Generator: {ex.Message}");
			useLLM = false;
		}
	}

	public override void _Input(InputEvent @event)
	{
		// Toggle panel with TAB
		if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo && keyEvent.Keycode == Key.Tab)
		{
			TogglePanel();
			GetViewport().SetInputAsHandled();
		}
	}

	private void TogglePanel()
	{
		isOpen = !isOpen;
		Visible = isOpen;

		// Pause/unpause game
		GetTree().Paused = isOpen;

		if (isOpen)
		{
			RefreshAll();
		}
	}

	private void CreateUI()
	{
		// Center the entire panel on screen
		SetAnchorsPreset(LayoutPreset.Center);
		CustomMinimumSize = new Vector2(600, 550);
		// Center the control properly by offsetting by half its size
		OffsetLeft = -300;
		OffsetTop = -275;
		OffsetRight = 300;
		OffsetBottom = 275;
		GrowHorizontal = GrowDirection.Both;
		GrowVertical = GrowDirection.Both;

		// Background
		var bg = new ColorRect();
		bg.Color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
		bg.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(bg);

		// Main vertical layout container with margin
		var marginContainer = new MarginContainer();
		marginContainer.SetAnchorsPreset(LayoutPreset.FullRect);
		marginContainer.AddThemeConstantOverride("margin_left", 10);
		marginContainer.AddThemeConstantOverride("margin_right", 10);
		marginContainer.AddThemeConstantOverride("margin_top", 10);
		marginContainer.AddThemeConstantOverride("margin_bottom", 10);
		AddChild(marginContainer);

		var mainVBox = new VBoxContainer();
		mainVBox.AddThemeConstantOverride("separation", 10);
		marginContainer.AddChild(mainVBox);

		// Title
		titleLabel = new Label();
		titleLabel.Text = "Element Manager";
		titleLabel.AddThemeColorOverride("font_color", Colors.White);
		titleLabel.AddThemeFontSizeOverride("font_size", 24);
		titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
		mainVBox.AddChild(titleLabel);

		// Tab container
		tabContainer = new TabContainer();
		tabContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
		tabContainer.TabAlignment = TabBar.AlignmentMode.Center;
		mainVBox.AddChild(tabContainer);

		// Create tabs
		CreateCombineTab();
		CreateInventoryTab();
		CreateTestTab();
	}

	private void CreateCombineTab()
	{
		// Create a scroll container for the entire tab
		var combineTabScroll = new ScrollContainer();
		combineTabScroll.Name = "Combine";
		combineTabScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
		tabContainer.AddChild(combineTabScroll);

		var combineTab = new VBoxContainer();
		combineTab.AddThemeConstantOverride("separation", 10);
		combineTab.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		combineTabScroll.AddChild(combineTab);

		// Top row: LLM checkbox
		var topHBox = new HBoxContainer();
		topHBox.AddThemeConstantOverride("separation", 20);
		combineTab.AddChild(topHBox);

		useLLMCheckbox = new CheckBox();
		useLLMCheckbox.Text = "Use LLM Generation";
		useLLMCheckbox.ButtonPressed = true;
		useLLMCheckbox.AddThemeColorOverride("font_color", Colors.White);
		useLLMCheckbox.Toggled += (bool enabled) => { useLLM = enabled; };
		topHBox.AddChild(useLLMCheckbox);

		// Element selection row
		var selectionHBox = new HBoxContainer();
		selectionHBox.AddThemeConstantOverride("separation", 20);
		selectionHBox.SizeFlagsVertical = SizeFlags.ExpandFill;
		combineTab.AddChild(selectionHBox);

		// Element list 1 container
		var list1VBox = new VBoxContainer();
		list1VBox.AddThemeConstantOverride("separation", 5);
		list1VBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		selectionHBox.AddChild(list1VBox);

		var label1 = new Label();
		label1.Text = "Select Element 1:";
		label1.AddThemeColorOverride("font_color", Colors.White);
		list1VBox.AddChild(label1);

		var scroll1 = new ScrollContainer();
		scroll1.CustomMinimumSize = new Vector2(0, 150);
		scroll1.SizeFlagsVertical = SizeFlags.ExpandFill;
		list1VBox.AddChild(scroll1);

		elementList1 = new VBoxContainer();
		elementList1.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		scroll1.AddChild(elementList1);

		// Element list 2 container
		var list2VBox = new VBoxContainer();
		list2VBox.AddThemeConstantOverride("separation", 5);
		list2VBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		selectionHBox.AddChild(list2VBox);

		var label2 = new Label();
		label2.Text = "Select Element 2:";
		label2.AddThemeColorOverride("font_color", Colors.White);
		list2VBox.AddChild(label2);

		var scroll2 = new ScrollContainer();
		scroll2.CustomMinimumSize = new Vector2(0, 150);
		scroll2.SizeFlagsVertical = SizeFlags.ExpandFill;
		list2VBox.AddChild(scroll2);

		elementList2 = new VBoxContainer();
		elementList2.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		scroll2.AddChild(elementList2);

		// Cached combination container (shows previous combinations)
		cachedCombinationContainer = new VBoxContainer();
		cachedCombinationContainer.AddThemeConstantOverride("separation", 10);
		cachedCombinationContainer.Visible = false; // Hidden by default
		combineTab.AddChild(cachedCombinationContainer);

		var cachedHeaderLabel = new Label();
		cachedHeaderLabel.Text = "Previously Generated:";
		cachedHeaderLabel.AddThemeColorOverride("font_color", Colors.Cyan);
		cachedHeaderLabel.HorizontalAlignment = HorizontalAlignment.Center;
		cachedCombinationContainer.AddChild(cachedHeaderLabel);

		// Scroll container for cached element label (prevents overflow)
		var cachedScrollContainer = new ScrollContainer();
		cachedScrollContainer.CustomMinimumSize = new Vector2(0, 60);
		cachedScrollContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
		cachedCombinationContainer.AddChild(cachedScrollContainer);

		cachedElementLabel = new Label();
		cachedElementLabel.HorizontalAlignment = HorizontalAlignment.Center;
		cachedElementLabel.AddThemeColorOverride("font_color", Colors.White);
		cachedElementLabel.AddThemeFontSizeOverride("font_size", 16);
		cachedElementLabel.AutowrapMode = TextServer.AutowrapMode.Word;
		cachedElementLabel.CustomMinimumSize = new Vector2(550, 0); // Force wrapping
		cachedScrollContainer.AddChild(cachedElementLabel);

		// Buttons row for cached combination
		var cachedButtonsHBox = new HBoxContainer();
		cachedButtonsHBox.AddThemeConstantOverride("separation", 20);
		cachedButtonsHBox.Alignment = BoxContainer.AlignmentMode.Center;
		cachedCombinationContainer.AddChild(cachedButtonsHBox);

		useCachedButton = new Button();
		useCachedButton.Text = "Use Previous";
		useCachedButton.CustomMinimumSize = new Vector2(150, 40);
		useCachedButton.Pressed += OnUseCachedPressed;
		var useCachedStyle = new StyleBoxFlat();
		useCachedStyle.BgColor = new Color(0.3f, 0.7f, 0.3f);
		useCachedButton.AddThemeStyleboxOverride("normal", useCachedStyle);
		cachedButtonsHBox.AddChild(useCachedButton);

		generateNewButton = new Button();
		generateNewButton.Text = "Generate New";
		generateNewButton.CustomMinimumSize = new Vector2(150, 40);
		generateNewButton.Pressed += () => OnCombinePressed(forceNew: true);
		var generateNewStyle = new StyleBoxFlat();
		generateNewStyle.BgColor = new Color(0.7f, 0.5f, 0.2f);
		generateNewButton.AddThemeStyleboxOverride("normal", generateNewStyle);
		cachedButtonsHBox.AddChild(generateNewButton);

		// Combine button (centered) - only shown when no cached version exists
		var buttonContainer = new CenterContainer();
		combineTab.AddChild(buttonContainer);

		combineButton = new Button();
		combineButton.Text = "Combine!";
		combineButton.CustomMinimumSize = new Vector2(150, 40);
		combineButton.Pressed += () => OnCombinePressed(forceNew: false);
		buttonContainer.AddChild(combineButton);

		// Result label
		resultLabel = new Label();
		resultLabel.HorizontalAlignment = HorizontalAlignment.Center;
		resultLabel.AddThemeColorOverride("font_color", Colors.Yellow);
		resultLabel.AddThemeFontSizeOverride("font_size", 16);
		combineTab.AddChild(resultLabel);

		// LLM output section with background
		var llmOutputContainer = new PanelContainer();
		combineTab.AddChild(llmOutputContainer);

		var outputStyle = new StyleBoxFlat();
		outputStyle.BgColor = new Color(0.05f, 0.05f, 0.05f, 0.8f);
		llmOutputContainer.AddThemeStyleboxOverride("panel", outputStyle);

		var outputMargin = new MarginContainer();
		outputMargin.AddThemeConstantOverride("margin_left", 5);
		outputMargin.AddThemeConstantOverride("margin_right", 5);
		outputMargin.AddThemeConstantOverride("margin_top", 5);
		outputMargin.AddThemeConstantOverride("margin_bottom", 5);
		llmOutputContainer.AddChild(outputMargin);

		llmOutputLabel = new Label();
		llmOutputLabel.Text = "LLM Output will appear here...";
		llmOutputLabel.AddThemeColorOverride("font_color", Colors.Cyan);
		llmOutputLabel.AutowrapMode = TextServer.AutowrapMode.Word;
		llmOutputLabel.VerticalAlignment = VerticalAlignment.Top;
		outputMargin.AddChild(llmOutputLabel);
	}

	private void CreateInventoryTab()
	{
		var inventoryTab = new VBoxContainer();
		inventoryTab.Name = "Inventory";
		inventoryTab.AddThemeConstantOverride("separation", 10);
		tabContainer.AddChild(inventoryTab);

		// Instructions
		var instructionLabel = new Label();
		instructionLabel.Text = "Click to equip/unequip elements (max 3 equipped)";
		instructionLabel.HorizontalAlignment = HorizontalAlignment.Center;
		instructionLabel.AddThemeColorOverride("font_color", Colors.White);
		instructionLabel.AddThemeFontSizeOverride("font_size", 14);
		inventoryTab.AddChild(instructionLabel);

		// Scroll container for element list
		inventoryScrollContainer = new ScrollContainer();
		inventoryScrollContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
		inventoryScrollContainer.CustomMinimumSize = new Vector2(0, 400);
		inventoryTab.AddChild(inventoryScrollContainer);

		// Element list
		inventoryElementList = new VBoxContainer();
		inventoryElementList.AddThemeConstantOverride("separation", 5);
		inventoryScrollContainer.AddChild(inventoryElementList);
	}

	private void RefreshAll()
	{
		RefreshCombineElementLists();
		RefreshInventoryList();
	}

	private void RefreshCombineElementLists()
	{
		// Clear lists
		foreach (Node child in elementList1.GetChildren())
			child.QueueFree();
		foreach (Node child in elementList2.GetChildren())
			child.QueueFree();

		// Get elements
		var elements = inventory.GetAll();

		// Populate both lists
		foreach (var (elementId, count) in elements)
		{
			// Get element data
			Element element = ElementRegistry.GetElement(elementId)
				;

			// Add to first list
			var button1 = CreateElementButton(elementId, element, count, 1);
			elementList1.AddChild(button1);

			// Add to second list
			var button2 = CreateElementButton(elementId, element, count, 2);
			elementList2.AddChild(button2);
		}

		resultLabel.Text = "";
	}

	private void RefreshInventoryList()
	{
		// Clear existing elements
		foreach (Node child in inventoryElementList.GetChildren())
		{
			child.QueueFree();
		}

		// Get all elements
		var elements = inventory.GetAll();
		if (elements.Count == 0)
		{
			var emptyLabel = new Label();
			emptyLabel.Text = "(No elements in inventory)";
			emptyLabel.HorizontalAlignment = HorizontalAlignment.Center;
			emptyLabel.AddThemeColorOverride("font_color", Colors.Gray);
			inventoryElementList.AddChild(emptyLabel);
			return;
		}

		// Create row for each element
		foreach (var (elementId, count) in elements)
		{
			var row = CreateInventoryElementRow(elementId, count);
			inventoryElementList.AddChild(row);
		}
	}

	private HBoxContainer CreateInventoryElementRow(int elementId, int count)
	{
		// Get element data
		Element element = ElementRegistry.GetElement(elementId);

		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 10);

		// Color indicator
		var colorRect = new ColorRect();
		colorRect.CustomMinimumSize = new Vector2(20, 40);
		colorRect.Color = element?.Color ?? Colors.Gray;
		row.AddChild(colorRect);

		// Element name and count
		var nameLabel = new Label();
		nameLabel.Text = $"{element?.Name ?? $"Element {elementId}"} x{count}";
		nameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		nameLabel.VerticalAlignment = VerticalAlignment.Center;
		nameLabel.AddThemeColorOverride("font_color", Colors.White);
		row.AddChild(nameLabel);

		// Equip/Unequip button
		var equipButton = new Button();
		equipButton.CustomMinimumSize = new Vector2(100, 40);

		bool isEquipped = inventory.IsEquipped(elementId);
		equipButton.Text = isEquipped ? "Unequip" : "Equip";

		// Style button based on equipped state
		var normalStyle = new StyleBoxFlat();
		var hoverStyle = new StyleBoxFlat();
		var pressedStyle = new StyleBoxFlat();

		if (isEquipped)
		{
			normalStyle.BgColor = new Color(0.3f, 0.7f, 0.3f); // Green
			hoverStyle.BgColor = new Color(0.4f, 0.8f, 0.4f); // Lighter green
			pressedStyle.BgColor = new Color(0.2f, 0.6f, 0.2f); // Darker green
		}
		else
		{
			normalStyle.BgColor = new Color(0.5f, 0.5f, 0.5f); // Gray
			hoverStyle.BgColor = new Color(0.6f, 0.6f, 0.6f); // Lighter gray
			pressedStyle.BgColor = new Color(0.4f, 0.4f, 0.4f); // Darker gray
		}

		equipButton.AddThemeStyleboxOverride("normal", normalStyle);
		equipButton.AddThemeStyleboxOverride("hover", hoverStyle);
		equipButton.AddThemeStyleboxOverride("pressed", pressedStyle);

		// Connect button signal
		equipButton.Pressed += () => OnEquipButtonPressed(elementId);

		row.AddChild(equipButton);

		return row;
	}

	private void OnEquipButtonPressed(int elementId)
	{
		bool isEquipped = inventory.IsEquipped(elementId);

		if (isEquipped)
		{
			// Unequip
			inventory.UnequipElement(elementId);
			GD.Print($"Unequipped {elementId}");
		}
		else
		{
			// Try to equip
			bool success = inventory.EquipElement(elementId);
			if (!success)
			{
				GD.Print("Cannot equip: all slots full. Unequip an element first.");
			}
			else
			{
				GD.Print($"Equipped {elementId}");
			}
		}

		// Refresh the inventory list
		RefreshInventoryList();
	}

	private Button CreateElementButton(int elementId, Element element, int count, int listNumber)
	{
		var button = new Button();
		button.Text = $"{element?.Name ?? $"Element {elementId}"} x{count}";
		button.CustomMinimumSize = new Vector2(0, 30);
		button.SizeFlagsHorizontal = SizeFlags.ExpandFill;

		// Set color
		if (element != null)
		{
			var style = new StyleBoxFlat();
			style.BgColor = element.Color * 0.5f;
			button.AddThemeStyleboxOverride("normal", style);
		}

		// Connect signal
		button.Pressed += () => SelectElement(elementId, listNumber);

		return button;
	}

	private void SelectElement(int elementId, int listNumber)
	{
		if (listNumber == 1)
		{
			selectedElement1 = elementId;
			GD.Print($"Selected element 1: {elementId}");
		}
		else
		{
			selectedElement2 = elementId;
			GD.Print($"Selected element 2: {elementId}");
		}

		// Update result label to show combination preview
		if (selectedElement1 > 0 && selectedElement2 > 0)
		{
			// Get element data for display (check registry first for combined elements)
			Element elem1 = ElementRegistry.GetElement(selectedElement1)
				;
			Element elem2 = ElementRegistry.GetElement(selectedElement2)
				;

			// Check if this combination has been seen before
			Element cachedCombination = llmGenerator?.GetCachedCombination(selectedElement1, selectedElement2);

			if (cachedCombination != null)
			{
				// Show cached combination UI
				cachedCombinationContainer.Visible = true;
				combineButton.Visible = false;

				cachedElementLabel.Text = $"✨ {cachedCombination.Name}\n{cachedCombination.Description}";

				resultLabel.Text = $"{elem1?.Name ?? selectedElement1.ToString()} + {elem2?.Name ?? selectedElement2.ToString()}";
				resultLabel.AddThemeColorOverride("font_color", Colors.Cyan);
			}
			else
			{
				// Show regular combine button
				cachedCombinationContainer.Visible = false;
				combineButton.Visible = true;

				resultLabel.Text = $"{elem1?.Name ?? selectedElement1.ToString()} + {elem2?.Name ?? selectedElement2.ToString()} = ???";
				resultLabel.AddThemeColorOverride("font_color", Colors.Yellow);
			}
		}
	}

	private async void OnUseCachedPressed()
	{
		if (selectedElement1 <= 0 || selectedElement2 <= 0)
		{
			resultLabel.Text = "Select two elements first!";
			resultLabel.AddThemeColorOverride("font_color", Colors.Red);
			return;
		}

		// Get cached combination
		Element cachedElement = llmGenerator?.GetCachedCombination(selectedElement1, selectedElement2);

		if (cachedElement == null)
		{
			resultLabel.Text = "Cached element not found!";
			resultLabel.AddThemeColorOverride("font_color", Colors.Red);
			return;
		}

		// Use the inventory's combine method with the cached element
		var result = inventory.CombineElements(selectedElement1, selectedElement2, cachedElement);

		if (result != null)
		{
			// Show success
			resultLabel.Text = $"Created: {cachedElement.Name}! (from cache)";
			resultLabel.AddThemeColorOverride("font_color", Colors.LightGreen);

			// Display cached element info
			var output = $"♻️ Using Previous Combination!\n\n";
			output += $"Element: {cachedElement.Name}\n";
			output += $"Description: {cachedElement.Description}\n";
			output += $"Color: {cachedElement.ColorHex}\n";
			output += $"Tier: {cachedElement.Tier}\n\n";

			if (cachedElement.Ability != null)
			{
				output += "Ability: Cached\n";
				output += $"{cachedElement.Ability.Description}\n";
				output += $"Cooldown: {cachedElement.Ability.Cooldown}s\n";
			}

			llmOutputLabel.Text = output;
			llmOutputLabel.AddThemeColorOverride("font_color", Colors.Cyan);

			// Clear selections
			selectedElement1 = 0;
			selectedElement2 = 0;

			// Refresh lists
			RefreshAll();
		}
	}

	private async void OnCombinePressed(bool forceNew = false)
	{
		if (selectedElement1 <= 0 || selectedElement2 <= 0)
		{
			resultLabel.Text = "Select two elements first!";
			resultLabel.AddThemeColorOverride("font_color", Colors.Red);
			return;
		}

		// Show loading message
		resultLabel.Text = "Combining...";
		resultLabel.AddThemeColorOverride("font_color", Colors.Yellow);
		llmOutputLabel.Text = "🔮 Generating element with LLM...";

		var startTime = DateTime.Now;

		try
		{
			Element newElement = null;

			// Use LLM to generate the element (name + ability)
			if (useLLM && llmGenerator != null)
			{
				llmOutputLabel.Text = forceNew
					? "🔮 Generating NEW element variation..."
					: "🔮 Asking LLM to create new element...";
				newElement = await llmGenerator.GenerateElementFromCombinationAsync(selectedElement1, selectedElement2, forceNew: true);
			}
			else
			{
				resultLabel.Text = "LLM is disabled - cannot generate dynamic elements!";
				resultLabel.AddThemeColorOverride("font_color", Colors.Red);
				llmOutputLabel.Text = "Enable LLM to combine elements.";
				llmOutputLabel.AddThemeColorOverride("font_color", Colors.Red);
				return;
			}

			if (newElement != null)
			{
				var elapsed = (DateTime.Now - startTime).TotalSeconds;

				// Ensure element is cached and has an ID before adding to inventory
				if (newElement.Id <= 0)
				{
					ElementRegistry.CacheElement(newElement);
					GD.Print($"Cached new element: {newElement.Name}");
				}

				// Use the inventory's combine method
				var result = inventory.CombineElements(selectedElement1, selectedElement2, newElement);

				if (result != null)
				{
					// Show success
					resultLabel.Text = $"Created: {newElement.Name}!";
					resultLabel.AddThemeColorOverride("font_color", Colors.LightGreen);

					// Display LLM output
					var llmOutput = forceNew
						? $"✨ New Variation Generated! ({elapsed:F2}s)\n\n"
						: $"✨ Element Created! ({elapsed:F2}s)\n\n";
					llmOutput += $"Element: {newElement.Name}\n";
					llmOutput += $"Description: {newElement.Description}\n";
					llmOutput += $"Color: {newElement.ColorHex}\n";
					llmOutput += $"Tier: {newElement.Tier}\n\n";

					if (newElement.Ability != null)
					{
						llmOutput += "Ability: Generated\n";
						llmOutput += $"{newElement.Ability.Description}\n";
						llmOutput += $"Cooldown: {newElement.Ability.Cooldown}s\n";
					}

					llmOutputLabel.Text = llmOutput;
					llmOutputLabel.AddThemeColorOverride("font_color", Colors.LightGreen);

					// Clear selections
					selectedElement1 = 0;
					selectedElement2 = 0;

					// Refresh lists
					RefreshAll();
				}
			}
			else
			{
				resultLabel.Text = "Cannot combine these elements!";
				resultLabel.AddThemeColorOverride("font_color", Colors.Red);
				llmOutputLabel.Text = "No valid combination found.";
				llmOutputLabel.AddThemeColorOverride("font_color", Colors.Red);
			}
		}
		catch (Exception ex)
		{
			resultLabel.Text = "Combination failed!";
			resultLabel.AddThemeColorOverride("font_color", Colors.Red);
			llmOutputLabel.Text = $"❌ Error:\n{ex.Message}\n\nStack:\n{ex.StackTrace}";
			llmOutputLabel.AddThemeColorOverride("font_color", Colors.OrangeRed);
			GD.PrintErr($"Combination error: {ex}");
		}
	}

	private void CreateTestTab()
	{
		var testTab = new VBoxContainer();
		testTab.Name = "Test";
		testTab.AddThemeConstantOverride("separation", 10);
		tabContainer.AddChild(testTab);

		// Instructions
		var instructionLabel = new Label();
		instructionLabel.Text = "Paste your custom JSON element here for testing:";
		instructionLabel.HorizontalAlignment = HorizontalAlignment.Center;
		instructionLabel.AddThemeColorOverride("font_color", Colors.White);
		instructionLabel.AddThemeFontSizeOverride("font_size", 14);
		testTab.AddChild(instructionLabel);

		// JSON input field
		jsonInput = new TextEdit();
		jsonInput.SizeFlagsVertical = SizeFlags.ExpandFill;
		jsonInput.CustomMinimumSize = new Vector2(0, 300);
		jsonInput.SyntaxHighlighter = null;
		jsonInput.PlaceholderText = @"{
  ""name"": ""Test Element"",
  ""description"": ""A test element"",
  ""color"": ""#FF5500"",
  ""ability"": {
    ""description"": ""Test ability"",
    ""primitives"": [""fire"", ""water""],
    ""effects"": [...],
    ""cooldown"": 1.5
  }
}";
		testTab.AddChild(jsonInput);

		// Buttons row
		var buttonsHBox = new HBoxContainer();
		buttonsHBox.AddThemeConstantOverride("separation", 20);
		buttonsHBox.Alignment = BoxContainer.AlignmentMode.Center;
		testTab.AddChild(buttonsHBox);

		// Test JSON button
		testJsonButton = new Button();
		testJsonButton.Text = "Test JSON";
		testJsonButton.CustomMinimumSize = new Vector2(150, 40);
		testJsonButton.Pressed += OnTestJsonPressed;
		var testStyle = new StyleBoxFlat();
		testStyle.BgColor = new Color(0.3f, 0.6f, 0.9f);
		testJsonButton.AddThemeStyleboxOverride("normal", testStyle);
		buttonsHBox.AddChild(testJsonButton);

		// Clear database button
		clearDatabaseButton = new Button();
		clearDatabaseButton.Text = "Clear Database";
		clearDatabaseButton.CustomMinimumSize = new Vector2(150, 40);
		clearDatabaseButton.Pressed += OnClearDatabasePressed;
		var clearStyle = new StyleBoxFlat();
		clearStyle.BgColor = new Color(0.8f, 0.2f, 0.2f);
		clearDatabaseButton.AddThemeStyleboxOverride("normal", clearStyle);
		buttonsHBox.AddChild(clearDatabaseButton);

		// Result label
		testResultLabel = new Label();
		testResultLabel.HorizontalAlignment = HorizontalAlignment.Center;
		testResultLabel.AddThemeColorOverride("font_color", Colors.White);
		testResultLabel.AddThemeFontSizeOverride("font_size", 14);
		testResultLabel.AutowrapMode = TextServer.AutowrapMode.Word;
		testTab.AddChild(testResultLabel);
	}

	private void OnTestJsonPressed()
	{
		var jsonText = jsonInput.Text.Trim();

		if (string.IsNullOrEmpty(jsonText))
		{
			testResultLabel.Text = "Please enter some JSON first!";
			testResultLabel.AddThemeColorOverride("font_color", Colors.Red);
			return;
		}

		try
		{
			if (!ElementJsonParser.TryParseElement(
				jsonText,
				out var name,
				out var description,
				out var colorHex,
				out var ability,
				out var parseError))
			{
				testResultLabel.Text = $"Failed to parse JSON: {parseError}";
				testResultLabel.AddThemeColorOverride("font_color", Colors.Red);
				return;
			}

			if (ability == null)
			{
				testResultLabel.Text = "Failed to parse ability from JSON!";
				testResultLabel.AddThemeColorOverride("font_color", Colors.Red);
				return;
			}

			if (string.IsNullOrWhiteSpace(name))
				name = "Test Element";
			if (string.IsNullOrWhiteSpace(description))
				description = "A test element";
			if (string.IsNullOrWhiteSpace(colorHex))
				colorHex = "#808080";

			// Create element from response (ID will be auto-assigned by database)
			var newElement = new Element
			{
				// Id is auto-generated by database - don't set it
				Primitive = null,
				Name = name,
				Description = description,
				ColorHex = colorHex,
				Tier = 2,
				Recipe = new List<int>(),
				Ability = ability
			};

			// Add to inventory
			if (inventory != null)
			{
				// Cache element in database (this assigns the ID)
				int elementId = ElementRegistry.CacheElement(newElement);

				// Add to player inventory
				inventory.AddElement(elementId, 1);

				GD.Print($"Cached test element: {newElement.Name} (ID: {elementId})");

				testResultLabel.Text = $"✅ Created element: {newElement.Name}\nAdded to inventory!";
				testResultLabel.AddThemeColorOverride("font_color", Colors.LightGreen);

				// Refresh all lists
				RefreshAll();
			}
			else
			{
				testResultLabel.Text = "Inventory not initialized!";
				testResultLabel.AddThemeColorOverride("font_color", Colors.Red);
			}
		}
		catch (Exception ex)
		{
			testResultLabel.Text = $"❌ Error parsing JSON:\n{ex.Message}";
			testResultLabel.AddThemeColorOverride("font_color", Colors.Red);
			GD.PrintErr($"JSON parse error: {ex}");
		}
	}

	private void OnClearDatabasePressed()
	{
		try
		{
			// Clear element registry cache
			ElementRegistry.ClearAllCache();

			testResultLabel.Text = "✅ Database cleared successfully!";
			testResultLabel.AddThemeColorOverride("font_color", Colors.LightGreen);

			GD.Print("SQLite database cleared");
		}
		catch (Exception ex)
		{
			testResultLabel.Text = $"❌ Error clearing database:\n{ex.Message}";
			testResultLabel.AddThemeColorOverride("font_color", Colors.Red);
			GD.PrintErr($"Database clear error: {ex}");
		}
	}

	public override void _ExitTree()
	{
		// Nothing to clean up
	}
}
