using Godot;
using System;
using System.Collections.Generic;
using Lexmancer.Abilities.V2;
using Lexmancer.Abilities.Visuals;

namespace Lexmancer.Abilities.Execution;

/// <summary>
/// Raycast-based beam attack that hits all enemies in a line
/// </summary>
public partial class BeamNode : Node2D
{
	public float Length { get; set; } = 400f;
	public float Width { get; set; } = 20f;
	public float Duration { get; set; } = 1f;
	public float TravelTime { get; set; } = 0f; // 0 = instant, >0 = beam grows over time
	public Vector2 Direction { get; set; } = Vector2.Right;
	public List<EffectAction> OnHitActions { get; set; } = new();
	public EffectContext Context { get; set; }

	private Line2D beamLine;
	private float timeAlive = 0f;
	private float currentLength = 0f; // Current length for animation
	private HashSet<Node> hitEnemies = new(); // Track enemies already hit to prevent multi-hits
	private Color elementColor;

	public override void _Ready()
	{
		// Determine element color
		elementColor = GetElementColor();

		// Initialize length (instant or growing)
		currentLength = TravelTime > 0 ? 0f : Length;

		// Create visual representation
		beamLine = new Line2D();
		beamLine.Width = Width;
		beamLine.DefaultColor = new Color(elementColor.R, elementColor.G, elementColor.B, 0.8f);
		beamLine.AddPoint(Vector2.Zero);
		beamLine.AddPoint(Direction.Normalized() * currentLength);

		// Add gradient effect with element color
		var gradient = new Gradient();
		gradient.AddPoint(0f, elementColor); // Bright at origin
		gradient.AddPoint(1f, new Color(elementColor.R, elementColor.G, elementColor.B, 0.2f)); // Fade at tip
		beamLine.Gradient = gradient;

		AddChild(beamLine);

		// Add glow effect
		VisualSystem.AddGlow(this, elementColor, 1.0f);

		// If instant beam, perform raycast immediately
		if (TravelTime <= 0)
		{
			CastBeam();
		}

		GD.Print($"BeamNode spawned: length={Length}, width={Width}, duration={Duration}, travelTime={TravelTime}, color={elementColor}");
	}

	/// <summary>
	/// Get element color from on-hit actions or ability primitives
	/// </summary>
	private Color GetElementColor()
	{
		// Try to get element from on-hit damage action
		if (OnHitActions != null)
		{
			foreach (var action in OnHitActions)
			{
				if (action.Action.ToLower() == "damage" && action.Args != null && action.Args.ContainsKey("element"))
				{
					var element = action.Args["element"].ToString();
					GD.Print($"[Beam] Found element from damage action: {element}");
					return VisualSystem.GetElementColor(element);
				}
			}
		}

		// Try to get from context ability primitives
		if (Context?.Ability?.Primitives != null && Context.Ability.Primitives.Count > 0)
		{
			var primitives = Context.Ability.Primitives;
			GD.Print($"[Beam] Using primitives for color: {string.Join(", ", primitives)}");
			return VisualSystem.GetElementColor(primitives);
		}

		// Fallback to neutral
		GD.Print("[Beam] No element found, using neutral (white)");
		return VisualSystem.GetElementColor("neutral");
	}

	public override void _Process(double delta)
	{
		timeAlive += (float)delta;

		// Update length for travel animation
		if (TravelTime > 0 && currentLength < Length)
		{
			float travelProgress = Mathf.Min(timeAlive / TravelTime, 1f);
			currentLength = Length * travelProgress;

			// Update beam visual endpoint
			if (beamLine != null && beamLine.GetPointCount() >= 2)
			{
				beamLine.SetPointPosition(1, Direction.Normalized() * currentLength);
			}

			// Continuously cast beam as it grows (hit enemies as we reach them)
			CastBeamSegment();
		}

		// Fade out over duration (starts after travel time)
		float fadeStartTime = TravelTime;
		float alpha = timeAlive < fadeStartTime ? 0.8f : 1f - ((timeAlive - fadeStartTime) / (Duration - fadeStartTime));
		if (beamLine != null)
		{
			beamLine.DefaultColor = new Color(elementColor.R, elementColor.G, elementColor.B, alpha);
		}

		// Destroy when duration expires
		if (timeAlive >= Duration + TravelTime)
		{
			QueueFree();
		}
	}

	/// <summary>
	/// Cast beam segment as it grows (used for animated beams)
	/// </summary>
	private void CastBeamSegment()
	{
		if (Context == null || OnHitActions == null)
			return;

		CastBeamWithLength(currentLength);
	}

	/// <summary>
	/// Cast beam and hit all enemies in path
	/// </summary>
	private void CastBeam()
	{
		CastBeamWithLength(Length);
	}

	/// <summary>
	/// Cast beam with specific length
	/// </summary>
	private void CastBeamWithLength(float beamLength)
	{
		if (Context == null || OnHitActions == null)
			return;

		var spaceState = GetWorld2D().DirectSpaceState;
		var rayDirection = Direction.Normalized();
		var rayEnd = GlobalPosition + rayDirection * beamLength;

		// Use multiple raycasts to detect all enemies in the beam's path
		// We'll do multiple raycasts across the beam's width
		int rayCount = Math.Max(3, (int)(Width / 10)); // At least 3 rays, more for wider beams

		for (int i = 0; i < rayCount; i++)
		{
			// Calculate offset perpendicular to beam direction
			float offset = (i - rayCount / 2f) * (Width / rayCount);
			var perpendicular = new Vector2(-rayDirection.Y, rayDirection.X);
			var rayStart = GlobalPosition + perpendicular * offset;
			var rayEndOffset = rayEnd + perpendicular * offset;

			// Perform raycast
			var query = PhysicsRayQueryParameters2D.Create(rayStart, rayEndOffset);
			query.CollideWithAreas = true;
			query.CollideWithBodies = true;

			// Cast multiple times to hit all enemies in line
			for (int depth = 0; depth < 10; depth++) // Max 10 enemies per ray
			{
				var result = spaceState.IntersectRay(query);

				if (result.Count == 0)
					break; // No more hits

				var collider = result["collider"].As<Node>();

				// Check if this is an enemy we haven't hit yet
				if (collider != null && collider.IsInGroup("enemies") && !hitEnemies.Contains(collider))
				{
					hitEnemies.Add(collider);
					HitEnemy(collider);
				}

				// Update query to start from hit point + small offset to find next enemy
				var hitPos = result["position"].As<Vector2>();
				query.From = hitPos + rayDirection * 1f; // Small offset to avoid re-hitting same enemy

				if (query.From.DistanceTo(rayStart) >= beamLength)
					break; // Reached end of beam
			}
		}

		if (hitEnemies.Count > 0)
			GD.Print($"Beam hit {hitEnemies.Count} enemies total");
	}

	/// <summary>
	/// Execute OnHit actions for a hit enemy
	/// </summary>
	private void HitEnemy(Node enemy)
	{
		if (OnHitActions == null || OnHitActions.Count == 0)
			return;

		// Create context for this hit
		var hitContext = Context.With(
			target: enemy,
			position: enemy is Node2D enemy2D ? enemy2D.GlobalPosition : Context.Position
		);

		// Execute OnHit actions
		var interpreter = new EffectInterpreter(GetTree().Root);
		foreach (var action in OnHitActions)
		{
			interpreter.Execute(action, hitContext);
		}

		GD.Print($"Beam hit enemy: {enemy.Name}");
	}
}
