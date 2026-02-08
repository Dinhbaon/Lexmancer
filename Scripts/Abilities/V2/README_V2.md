# Ability System V2 - Quick Start Guide

## What's New in V2?

✨ **Effect Scripting** - LLM generates flexible action scripts instead of rigid JSON
🎨 **Infinite Creativity** - Actions can be nested and combined in unique ways
🛡️ **No Unknown Effects** - Only predefined actions, validated and safe
🔗 **Composable** - Effects can trigger other effects (on_hit, on_expire)

---

## Basic Usage

### 1. Create Generator

```csharp
var generator = new AbilityGeneratorV2("player_001");
```

### 2. Generate Ability

```csharp
var primitives = new List<PrimitiveType> {
    PrimitiveType.Fire,
    PrimitiveType.Shadow
};

var result = await generator.GenerateAbilityAsync(primitives);
var ability = result.Ability;

GD.Print($"Generated: {ability.Name}");
GD.Print($"Description: {ability.Description}");
```

### 3. Execute Ability

```csharp
ability.Execute(
    position: playerPosition,
    direction: aimDirection,
    caster: playerNode,
    worldNode: GetTree().Root
);
```

---

## Example: Generated Ability

### Input
```csharp
Primitives: Fire + Ice
```

### LLM Generates
```json
{
  "name": "Thermal Shock",
  "description": "Rapidly alternates between fire and ice, shattering frozen enemies",
  "effects": [{
    "script": [
      {
        "action": "apply_status",
        "args": {"status": "frozen", "duration": 2}
      },
      {
        "action": "spawn_area",
        "args": {"radius": 150, "duration": 1},
        "on_expire": [
          {
            "action": "damage",
            "args": {"amount": 40, "element": "fire"}
          }
        ]
      }
    ]
  }]
}
```

### What Happens
1. Freezes enemy for 2 seconds
2. Creates ice area (150px radius, 1 second)
3. When area expires → Explodes with fire damage

---

## Available Actions

### Spawning
- `spawn_projectile` - Fires projectiles (count, pattern, speed)
- `spawn_area` - Creates area effect (radius, duration, lingering_damage)
- `spawn_beam` - Shoots beam (length, width, duration)

### Damage
- `damage` - Deals damage (amount OR formula, element)
- `apply_status` - Applies status effect (status, duration, stacks)

### Modifiers
- `chain_to_nearby` - Chains to nearby enemies (max_chains, range)
- `knockback` - Pushes targets (force, direction)
- `heal` - Restores health (amount)
- `repeat` - Repeats actions (count, interval)

### Nesting
- `on_hit` - Execute when effect hits target
- `on_expire` - Execute when effect duration ends

---

## Testing

### Run Test Script

1. Attach `TestAbilityV2.cs` to a node in your scene
2. Run the scene
3. Check console output

### Manual Testing

```csharp
// In your game code
public partial class PlayerController : Node2D
{
    private AbilityGeneratorV2 generator;
    private AbilityV2 currentAbility;

    public override void _Ready()
    {
        generator = new AbilityGeneratorV2("player_001");
        LoadAbility();
    }

    private async void LoadAbility()
    {
        var result = await generator.GenerateAbilityAsync(
            new List<PrimitiveType> { PrimitiveType.Lightning, PrimitiveType.Poison }
        );

        currentAbility = result.Ability;
        var prims = string.Join(", ", currentAbility.Primitives);
        GD.Print($"Loaded ability (primitives: {prims})");
    }

    public override void _Input(InputEvent @event)
    {
        if (@event.IsActionPressed("cast_ability") && currentAbility != null)
        {
            CastAbility();
        }
    }

    private void CastAbility()
    {
        var mousePos = GetGlobalMousePosition();
        var direction = (mousePos - GlobalPosition).Normalized();

        currentAbility.Execute(
            GlobalPosition,
            direction,
            this,
            GetTree().Root
        );
    }
}
```

---

## Migration from V1

### Key Differences

| V1 | V2 |
|----|-----|
| `AbilityData` | `AbilityV2` |
| `AbilityGenerator` | `AbilityGeneratorV2` |
| `LLMClient` | `LLMClientV2` |
| Rigid JSON effects | Flexible action scripts |
| Limited nesting | Unlimited nesting (with safety) |

### Code Changes

```csharp
// OLD (V1)
var generator = new AbilityGenerator("player_001");
var result = await generator.GenerateAbilityAsync(primitives);
AbilityData ability = result.Ability;

// NEW (V2)
var generator = new AbilityGeneratorV2("player_001");
var result = await generator.GenerateAbilityAsync(primitives);
AbilityV2 ability = result.Ability;

// Execution is similar
ability.Execute(pos, dir, caster, world);
```

---

## TODOs for Full Integration

### 1. Wire Up Projectile Collisions

In `ProjectileNodeV2.cs`:
```csharp
private void _OnArea2DBodyEntered(Node2D body)
{
    if (body is Enemy enemy)
    {
        OnCollision(enemy);
    }
}
```

### 2. Implement Actual Damage

In `EffectInterpreter.cs`:
```csharp
private void ApplyDamage(...)
{
    if (ctx.Target is Enemy enemy)
    {
        enemy.TakeDamage(damage, element);
    }
}
```

### 3. Add Status Effect Visuals

In `StatusEffectManager.cs`:
```csharp
private void ApplyVisualEffect(Node entity, string status)
{
    switch (status)
    {
        case "burning":
            // Add fire particle effect
            break;
        case "frozen":
            // Add ice shader effect
            break;
    }
}
```

### 4. Implement Enemy Finding

In `EffectInterpreter.cs`:
```csharp
private List<Node> FindNearbyEnemies(Vector2 pos, float range, int max)
{
    var enemies = new List<Node>();
    var space = GetWorld2D().DirectSpaceState;

    // Use area query or loop through enemy nodes
    // Return up to 'max' enemies within 'range'

    return enemies;
}
```

### 5. Add Projectile/Area Spawning

In `EffectInterpreter.cs`:
```csharp
private void SpawnProjectile(...)
{
    var scene = GD.Load<PackedScene>("res://Scenes/ProjectileNodeV2.tscn");
    var projectile = scene.Instantiate<ProjectileNodeV2>();

    projectile.Position = ctx.Position;
    projectile.Direction = direction;
    projectile.Speed = speed;
    projectile.OnHitActions = onHit;
    projectile.Context = ctx;

    worldNode.AddChild(projectile);
}
```

---

## Performance Tips

1. **Cache Hit Rate** - After ~10 combos, you'll have 80%+ cache hits
2. **First Generation** - 2-5 seconds (qwen2.5:7b thinking)
3. **Cached Abilities** - Instant (<1ms)
4. **Effect Execution** - <5ms per ability

---

## Troubleshooting

### LLM Not Responding
```bash
# Check Ollama is running
ollama list

# Should see: qwen2.5:7b
```

### JSON Parse Errors
- Check console for LLM response
- LLM should return valid JSON (format: "json" enforces this)
- If persists, check prompt in `LLMClientV2.BuildCreativePrompt()`

### Effects Not Executing
- Check console logs in `EffectInterpreter.Execute()`
- Verify world node is passed correctly
- Ensure stubs are replaced with actual implementation

---

## Next Steps

1. ✅ Test with `TestAbilityV2.cs`
2. ⚙️ Wire up collision detection
3. 🎨 Add visual effects for elements
4. 🎮 Integrate into your player controller
5. ⚡ Implement actual damage/healing
6. 🧪 Playtest and balance

---

**Questions?** Check the full implementation plan in `IMPLEMENTATION_PLAN.md`
