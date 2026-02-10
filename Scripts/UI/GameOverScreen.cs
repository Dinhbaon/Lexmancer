using Godot;
using System;
using Lexmancer.Core;

namespace Lexmancer.UI;

/// <summary>
/// Game over screen showing victory or defeat with restart button
/// </summary>
public partial class GameOverScreen : Control
{
	private Label titleLabel;
	private Label messageLabel;
	private Button restartButton;
	private ColorRect background;

	public override void _Ready()
	{
		// Full screen - fill the entire viewport
		SetAnchorsPreset(LayoutPreset.FullRect);
		OffsetLeft = 0;
		OffsetTop = 0;
		OffsetRight = 0;
		OffsetBottom = 0;
		Visible = false;

		// IMPORTANT: Allow processing when game is paused
		ProcessMode = ProcessModeEnum.Always;

		CreateUI();

		// Subscribe to EventBus
		SubscribeToEvents();
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
			EventBus.Instance.ShowGameOverScreen += OnShowGameOverScreen;
			GD.Print("GameOverScreen subscribed to EventBus");
		}
		else
		{
			GD.PrintErr("EventBus not available in GameOverScreen!");
		}
	}

	private void UnsubscribeFromEvents()
	{
		if (EventBus.Instance != null)
		{
			EventBus.Instance.ShowGameOverScreen -= OnShowGameOverScreen;
		}
	}

	// EventBus callback
	private void OnShowGameOverScreen(string message, bool isVictory)
	{
		ShowScreen(message, isVictory);
	}

	private void CreateUI()
	{
		// Semi-transparent background
		background = new ColorRect();
		background.Color = new Color(0, 0, 0, 0.8f);
		background.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(background);

		// Create a centered container for all content
		var centerContainer = new CenterContainer();
		centerContainer.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(centerContainer);

		// Vertical box to stack elements
		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 30);
		centerContainer.AddChild(vbox);

		// Title label (VICTORY or DEFEAT)
		titleLabel = new Label();
		titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
		titleLabel.AddThemeFontSizeOverride("font_size", 64);
		vbox.AddChild(titleLabel);

		// Message label
		messageLabel = new Label();
		messageLabel.HorizontalAlignment = HorizontalAlignment.Center;
		messageLabel.AddThemeFontSizeOverride("font_size", 24);
		messageLabel.AddThemeColorOverride("font_color", Colors.White);
		vbox.AddChild(messageLabel);

		// Restart button
		restartButton = new Button();
		restartButton.Text = "Restart (Press R)";
		restartButton.CustomMinimumSize = new Vector2(250, 60);
		restartButton.AddThemeFontSizeOverride("font_size", 24);
		restartButton.Pressed += OnRestartPressed;
		vbox.AddChild(restartButton);
	}

	public override void _Input(InputEvent @event)
	{
		// Allow restart with R key
		if (Visible && @event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo && keyEvent.Keycode == Key.R)
		{
			OnRestartPressed();
			GetViewport().SetInputAsHandled();
		}
	}

	/// <summary>
	/// Show the game over screen
	/// </summary>
	public void ShowScreen(string message, bool isVictory)
	{
		Visible = true;

		titleLabel.Text = message;

		if (isVictory)
		{
			titleLabel.AddThemeColorOverride("font_color", Colors.Green);
			messageLabel.Text = "All enemies defeated!";
		}
		else
		{
			titleLabel.AddThemeColorOverride("font_color", Colors.Red);
			messageLabel.Text = "You were defeated...";
		}
	}

	private void OnRestartPressed()
	{
		// Emit restart event instead of calling GameManager directly
		EventBus.Instance?.EmitSignal(EventBus.SignalName.GameRestarting);

		// For now, still reload the scene directly
		// TODO: Let GameManager handle this via event listener
		GetTree().Paused = false;
		GetTree().ReloadCurrentScene();
	}
}
