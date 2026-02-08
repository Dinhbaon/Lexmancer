# LLM-Powered Ability System

A dynamic ability generation system where players combine primitives to create unique abilities powered by a local LLM.

## Overview

Players collect **primitives** (like Fire, Ice, Projectile, Chain) and combine them to create abilities. The system uses a local LLM to generate creative, balanced abilities from these combinations. Each player has their own **spell book** - a cache of abilities they've discovered.

### Key Features

- ✨ **12 Primitive Types** that can be combined
- 🤖 **Local LLM Integration** (Ollama, llama.cpp)
- 💾 **Player-Specific Caching** - each player's abilities are unique
- 🔄 **Choice System** - reuse known abilities or generate new variants
- 🎮 **Fully Composable** - effects stack and combine naturally

## Architecture

### Core Components

1. **PrimitiveType.cs** - Defines the 12 primitives
   - Elements: Fire, Ice, Lightning, Poison
   - Actions: Projectile, Area, Beam, Touch
   - Modifiers: Chain, Pierce, Explode, Bounce

2. **AbilityData.cs** - JSON structure for abilities
   - Composable effects system
   - Support for damage, projectiles, areas, status effects

3. **AbilityCache.cs** - SQLite-based player-specific storage
   - Stores abilities per player ID
   - Tracks usage statistics
   - Supports multiple variants (future)

4. **LLMClient.cs** - Interface to local LLM
   - Ollama API support
   - Fallback system if LLM unavailable
   - JSON-only responses

5. **AbilityGenerator.cs** - Main generation service
   - Checks cache before generating
   - Handles force-regeneration
   - Records usage stats

6. **AbilityExecutor.cs** - Executes abilities in-game
   - Spawns projectiles, areas, beams
   - Applies damage and effects
   - Element-based visuals

7. **AbilityChoiceUI.cs** - UI for known abilities
   - Shows cached ability
   - "Use Known" vs "Generate New"
   - Pauses game during choice

## Setup

### 1. Install Dependencies

Add the SQLite NuGet package to your project:

```bash
dotnet add package Microsoft.Data.Sqlite
```

Or add to your `.csproj`:

```xml
<ItemGroup>
    <PackageReference Include="Microsoft.Data.Sqlite" Version="8.0.0" />
</ItemGroup>
```

### 2. Install Local LLM

Install [Ollama](https://ollama.ai/):

```bash
# Pull a model (recommended: qwen2.5:7b for best quality/speed balance)
ollama pull qwen2.5:7b
```

Or use llama.cpp server with compatible API.

### 3. Add to Your Scene

```csharp
// In your main game script
var abilityGenerator = new AbilityGenerator("player_001");
var abilityExecutor = new AbilityExecutor();
AddChild(abilityExecutor);

// Generate an ability
var primitives = new List<PrimitiveType> {
    PrimitiveType.Fire,
    PrimitiveType.Projectile,
    PrimitiveType.Explode
};

var result = await abilityGenerator.GenerateAbilityAsync(primitives);

// Execute it
abilityExecutor.ExecuteAbility(
    result.Ability,
    playerPosition,
    direction,
    worldNode
);
```

## Usage Examples

### Basic Generation

```csharp
// Create generator for a player
var generator = new AbilityGenerator("player_123");

// Combine primitives
var combo = new List<PrimitiveType> {
    PrimitiveType.Ice,
    PrimitiveType.Area,
    PrimitiveType.Chain
};

// Generate ability (checks cache first)
var result = await generator.GenerateAbilityAsync(combo);

if (result.WasCached) {
    GD.Print("Using known ability");
} else {
    GD.Print("NEW ability discovered");
}
```

### Check Cache Before Generating

```csharp
if (generator.HasCachedAbility(combo)) {
    // Show UI: "Use known ability or generate new?"
    ShowChoiceUI(combo);
} else {
    // First time, just generate
    var result = await generator.GenerateAbilityAsync(combo);
}
```

### Force Regeneration

```csharp
// Generate a new variant, replacing the old one
var result = await generator.GenerateAbilityAsync(combo, forceNew: true);
GD.Print("Generated new variant!");
```

### Execute Ability

```csharp
var executor = new AbilityExecutor();
executor.ExecuteAbility(
    ability,
    spawnPosition,
    direction,
    GetTree().Root
);
```

## JSON Format

Abilities are stored as JSON with this structure:

```json
{
  "name": "Erupting Fireball",
  "description": "Launches a burning projectile that explodes on impact",
  "primitives": ["fire", "projectile", "explode"],
  "effects": [
    {
      "type": "projectile",
      "properties": {
        "speed": 500,
        "lifetime": 2.0,
        "piercing": false
      }
    },
    {
      "type": "damage",
      "properties": {
        "element": "fire",
        "amount": 25,
        "area_of_effect": 100
      }
    },
    {
      "type": "status_effect",
      "properties": {
        "effect": "burning",
        "duration": 3.0,
        "damage_per_second": 5
      }
    }
  ],
  "cost": {
    "type": "mana",
    "amount": 20
  },
  "cooldown": 1.5,
  "version": 1,
  "generated_at": 1738800000
}
```

## Effect Types

The system supports these composable effects:

- **Projectile**: Traveling projectiles with speed/lifetime
- **Area**: Area of effect with radius/duration
- **Beam**: Continuous beam (TODO)
- **Damage**: Immediate or DoT damage with elements
- **StatusEffect**: Burning, Frozen, Poisoned, etc.
- **Knockback**: Physics-based pushback
- **Heal**: Restore health
- **Summon**: Spawn entities (TODO)
- **Shield**: Temporary protection (TODO)

## Testing

Use the demo script:

```bash
# Run the game with AbilitySystemDemo attached to a node
# Press number keys to add primitives:
# 1=Fire, 2=Ice, 3=Lightning, 4=Poison
# 5=Projectile, 6=Area, 7=Chain, 8=Pierce
# Enter = Execute combo
# R = Regenerate combo
# Backspace = Clear combo
```

## Cache Storage

Caches are stored per-player in:
```
{UserDataDir}/ability_cache/player_{playerId}.db
```

On Linux: `~/.local/share/godot/app_userdata/Lexmancer/ability_cache/`

## Configuration

### LLM Settings

```csharp
// Default: Ollama on localhost:11434 with qwen2.5:7b
var generator = new AbilityGenerator("player_id");

// Custom LLM model
var generator = new AbilityGenerator(
    "player_id",
    llmModel: "llama3.2:3b"  // Faster but less creative
);

// Custom LLM server
var generator = new AbilityGenerator(
    "player_id",
    llmBaseUrl: "http://localhost:8080",
    llmModel: "qwen2.5:14b"  // Higher quality
);
```

### Balance Tuning

Edit the prompt in `LLMClient.cs` to adjust:
- Damage ranges
- Cooldown ranges
- Cost ranges
- Effect intensity

## Future Enhancements

- [ ] Multiple variant storage (keep history)
- [ ] Ability upgrading/refinement
- [ ] Primitive discovery/unlocking
- [ ] Beam effects implementation
- [ ] Explosion visual effects
- [ ] Status effect system
- [ ] Ability hotbar/loadout
- [ ] Spell book UI
- [ ] Export/share abilities
- [ ] Guided generation (player preferences)

## Performance Notes

- **First generation**: 2-5 seconds (LLM inference)
- **Cached abilities**: Instant
- **Cache size**: ~1-2 KB per ability
- **LLM memory**: Depends on model (8B = ~5GB RAM, 70B = ~40GB RAM)

## Troubleshooting

**LLM not responding:**
- Check Ollama is running: `ollama list`
- Test API: `curl http://localhost:11434/api/generate -d '{"model":"llama3.2","prompt":"test"}'`

**Cache not saving:**
- Check permissions on user data directory
- Look for SQLite errors in console

**Abilities not balanced:**
- Adjust prompt in `LLMClient.GetDefaultSystemPrompt()`
- Add validation layer in `AbilityGenerator`

**JSON parsing errors:**
- LLM might be adding extra text - check `format: "json"` is working
- Fallback ability will be used automatically
