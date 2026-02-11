using Godot;
using System;
using Lexmancer.Core;

namespace Lexmancer.UI;

public partial class PauseButton : Control
{
	private const int IconSize = 24;
	private const int Padding = 12;

	private TextureButton button;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;

		CustomMinimumSize = new Vector2(IconSize, IconSize);
		AnchorLeft = 1;
		AnchorRight = 1;
		AnchorTop = 0;
		AnchorBottom = 0;
		OffsetLeft = -Padding - IconSize;
		OffsetRight = -Padding;
		OffsetTop = Padding;
		OffsetBottom = Padding + IconSize;

		button = new TextureButton();
		button.Name = "PauseIconButton";
		button.TextureNormal = CreatePauseTexture(IconSize, Colors.White);
		button.TextureHover = CreatePauseTexture(IconSize, new Color(1f, 1f, 1f, 0.85f));
		button.TexturePressed = CreatePauseTexture(IconSize, new Color(1f, 1f, 1f, 0.7f));
		button.CustomMinimumSize = new Vector2(IconSize, IconSize);
		button.FocusMode = FocusModeEnum.None;
		button.MouseFilter = MouseFilterEnum.Stop;
		button.AnchorLeft = 0;
		button.AnchorRight = 1;
		button.AnchorTop = 0;
		button.AnchorBottom = 1;
		button.OffsetLeft = 0;
		button.OffsetRight = 0;
		button.OffsetTop = 0;
		button.OffsetBottom = 0;
		button.Pressed += OnPressed;
		AddChild(button);
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo && keyEvent.Keycode == Key.Escape)
		{
			var combinationPanel = GetNodeOrNull<CombinationPanel>("/root/Main/UILayer/CombinationPanel");
			if (combinationPanel != null && combinationPanel.IsOpen)
			{
				combinationPanel.ClosePanel();
			}
			else
			{
				OnPressed();
			}

			GetViewport().SetInputAsHandled();
		}
	}

	private void OnPressed()
	{
		if (GetTree().Paused)
		{
			GetTree().Paused = false;
			EventBus.Instance?.EmitSignal(EventBus.SignalName.GameUnpaused);
		}
		else
		{
			GetTree().Paused = true;
			EventBus.Instance?.EmitSignal(EventBus.SignalName.GamePaused);
		}
	}

	private static Texture2D CreatePauseTexture(int size, Color color)
	{
		var image = Image.Create(size, size, false, Image.Format.Rgba8);
		image.Fill(new Color(0f, 0f, 0f, 0f));

		int padding = Math.Max(3, size / 4);
		int barWidth = Math.Max(3, size / 5);
		int gap = Math.Max(3, size / 6);
		int barHeight = size - padding * 2;
		int top = padding;
		int left1 = (size - (barWidth * 2 + gap)) / 2;
		int left2 = left1 + barWidth + gap;

		for (int y = top; y < top + barHeight; y++)
		{
			for (int x = left1; x < left1 + barWidth; x++)
			{
				image.SetPixel(x, y, color);
			}
			for (int x = left2; x < left2 + barWidth; x++)
			{
				image.SetPixel(x, y, color);
			}
		}

		return ImageTexture.CreateFromImage(image);
	}
}
