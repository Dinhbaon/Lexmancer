using Godot;

namespace Lexmancer.Abilities.Visuals;

public partial class ChainVisual : Node2D
{
	public Vector2 From { get; set; }
	public Vector2 To { get; set; }
	public Color ChainColor { get; set; } = new Color(1, 1, 1);
	public float Duration { get; set; } = 0.15f;
	public float Width { get; set; } = 4f;
	public int Segments { get; set; } = 5;
	public float Jitter { get; set; } = 10f;

	private Line2D line;

	public override void _Ready()
	{
		GlobalPosition = From;
		CreateLine();
		SpawnEndBurst(From);
		SpawnEndBurst(To);
		FadeAndCleanup();
	}

	private void CreateLine()
	{
		line = new Line2D();
		line.Width = Width;
		line.DefaultColor = new Color(ChainColor.R, ChainColor.G, ChainColor.B, 0.9f);
		line.Antialiased = true;

		var points = BuildJitteredPoints();
		foreach (var point in points)
		{
			line.AddPoint(point);
		}

		AddChild(line);
	}

	private Vector2[] BuildJitteredPoints()
	{
		var rng = new RandomNumberGenerator();
		rng.Randomize();

		var points = new Vector2[Segments + 1];
		var direction = To - From;
		var length = direction.Length();
		if (length <= 0.01f)
		{
			points[0] = Vector2.Zero;
			points[Segments] = Vector2.Zero;
			return points;
		}

		var dirNorm = direction / length;
		var perpendicular = new Vector2(-dirNorm.Y, dirNorm.X);

		for (int i = 0; i <= Segments; i++)
		{
			float t = i / (float)Segments;
			var basePoint = dirNorm * length * t;
			float offset = (i == 0 || i == Segments) ? 0f : rng.RandfRange(-Jitter, Jitter);
			points[i] = basePoint + perpendicular * offset;
		}

		return points;
	}

	private void SpawnEndBurst(Vector2 position)
	{
		var particles = new GpuParticles2D();
		particles.GlobalPosition = position;
		particles.Amount = 6;
		particles.Lifetime = Duration;
		particles.OneShot = true;
		particles.Emitting = true;
		particles.ProcessMaterial = VisualSystem.CreateParticleMaterial(ChainColor, ParticleType.Burst);
		particles.DrawOrder = GpuParticles2D.DrawOrderEnum.Index;

		GetTree().Root.AddChild(particles);

		var timer = new Timer();
		timer.WaitTime = Duration + 0.05f;
		timer.OneShot = true;
		timer.Autostart = true;
		timer.Timeout += () =>
		{
			particles.QueueFree();
			timer.QueueFree();
		};
		GetTree().Root.AddChild(timer);
	}

	private void FadeAndCleanup()
	{
		var tween = CreateTween();
		tween.TweenProperty(this, "modulate:a", 0f, Duration);
		tween.Finished += QueueFree;
	}
}
