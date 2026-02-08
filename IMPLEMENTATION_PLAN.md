# LLM-Powered Ability System V2 - Implementation Plan

**Date**: 2026-02-07
**Goal**: Replace rigid JSON schema with flexible component-based effect scripting system
**LLM Model**: qwen2.5:7b (already installed and tested)

---

## Executive Summary

**Current System**: Rigid JSON schema → Direct deserialization → Limited creativity
**New System**: Effect Scripts (DSL) → Safe Interpreter → Infinite creative combinations

**Key Benefits**:
- ✅ No unknown effect types (predefined action vocabulary)
- ✅ Infinite creativity through composition and nesting
- ✅ Safe execution (no arbitrary code)
- ✅ More interesting abilities with less prompt engineering

---

## Architecture Overview

### Component-Based Effect System

```
Player combines elements (Fire + Shadow)
         ↓
LLM generates effect scripts using predefined actions
         ↓
AbilityBuilder parses JSON into AbilityV2
         ↓
EffectInterpreter executes scripts safely
         ↓
Game spawns projectiles, damage, status effects, etc.
```

### Core Components

1. **AbilityV2** - New ability class with scriptable effects
2. **EffectScript** - List of actions to execute
3. **EffectAction** - Single scripted action (spawn_projectile, damage, etc.)
4. **EffectInterpreter** - Safe executor for effect scripts
5. **AbilityBuilder** - Parser for flexible LLM JSON
6. **CreativeLLMPrompt** - Teaches LLM the scripting language

---

## File Structure

### New Files to Create

```
Scripts/Abilities/
├── V2/
│   ├── AbilityV2.cs              # New ability class
│   ├── EffectScript.cs           # Action definitions & interpreter
│   ├── EffectAction.cs           # Individual action class
│   ├── EffectContext.cs          # Execution context
│   ├── AbilityBuilder.cs         # Parses LLM JSON
│   └── AbilityInterpreter.cs     # Executes effect scripts
├── LLM/
│   ├── LLMClientV2.cs            # Updated with creative prompts
│   └── AbilityGeneratorV2.cs     # V2 generation service
└── Execution/
    ├── ProjectileNode.cs         # Enhanced with script support
    ├── AreaEffectNode.cs         # Area effects with duration
    └── StatusEffectManager.cs    # Track status effects on entities
```

### Files to Keep (Unchanged for now)

```
Scripts/Abilities/
├── AbilityCache.cs               # Still works with JSON
├── PrimitiveType.cs              # Element definitions unchanged
└── AbilitySystemDemo.cs          # May need minor updates
```

---

## Implementation Phases

### Phase 1: Core Effect System (~15 min)

**File**: `Scripts/Abilities/V2/EffectAction.cs`
```csharp
// Define action structure
public class EffectAction
{
    public string Action { get; set; }
    public Dictionary<string, object> Args { get; set; }
    public List<EffectAction> OnHit { get; set; }
    public List<EffectAction> OnExpire { get; set; }
    public EffectCondition Condition { get; set; }
}
```

**File**: `Scripts/Abilities/V2/EffectScript.cs`
```csharp
// Effect interpreter with all actions
public class EffectInterpreter
{
    // Actions to implement:
    // - spawn_projectile
    // - spawn_area
    // - spawn_beam
    // - damage
    // - apply_status
    // - chain_to_nearby
    // - knockback
    // - heal
    // - repeat
}
```

**File**: `Scripts/Abilities/V2/AbilityV2.cs`
```csharp
public class AbilityV2
{
    public string Name { get; set; }
    public string Description { get; set; }
    public List<string> Primitives { get; set; }
    public List<EffectScript> Effects { get; set; }
    public float Cooldown { get; set; }

    public void Execute(Vector2 pos, Vector2 dir, Node caster, Node world)
    {
        // Execute all effect scripts
    }
}
```

**Tasks**:
- [x] Create EffectAction class
- [ ] Create EffectScript interpreter
- [ ] Implement basic actions (spawn_projectile, damage)
- [ ] Add nested action support (on_hit, on_expire)
- [ ] Create AbilityV2 class
- [ ] Add JSON serialization

**Time estimate**: 15 minutes

---

### Phase 2: AbilityBuilder Parser (~10 min)

**File**: `Scripts/Abilities/V2/AbilityBuilder.cs`

**Responsibilities**:
- Parse flexible LLM JSON
- Handle nested actions recursively
- Validate action names and parameters
- Clamp numerical values to safe ranges
- Provide helpful error messages

**Key Methods**:
```csharp
public static AbilityV2 FromLLMResponse(string json)
{
    // Parse JSON
    // Validate structure
    // Build AbilityV2 object
    // Return or throw exception
}

private static EffectAction ParseAction(JsonElement element)
{
    // Parse action recursively
    // Support on_hit, on_expire nesting
    // Parse args dictionary
}
```

**Tasks**:
- [ ] Create AbilityBuilder class
- [ ] Implement FromLLMResponse()
- [ ] Add recursive action parsing
- [ ] Add validation logic
- [ ] Test with sample JSON

**Time estimate**: 10 minutes

---

### Phase 3: LLM Prompt Engineering (~10 min)

**File**: `Scripts/Abilities/LLM/LLMClientV2.cs`

**New Prompt Structure**:
```
1. Element characteristics & themes
2. Available actions vocabulary
3. Nesting examples
4. Creative encouragement
5. JSON format specification
6. Multiple examples
```

**Actions Vocabulary to Teach LLM**:

**Spawning Actions**:
- `spawn_projectile`: count, pattern (single/spiral/spread/circle), speed
- `spawn_area`: radius, duration, lingering_damage
- `spawn_beam`: length, width, duration

**Damage Actions**:
- `damage`: amount OR formula, element
- `apply_status`: status (burning/frozen/poisoned/shocked/slowed/stunned/weakened), duration, stacks

**Modifier Actions**:
- `chain_to_nearby`: max_chains, range, on_hit
- `knockback`: force, direction (away/towards/up)
- `heal`: amount
- `repeat`: count, interval, on_hit

**Nesting Support**:
- `on_hit`: Actions to execute when effect hits target
- `on_expire`: Actions when effect duration ends

**Conditions** (Advanced):
- `condition.if`: "target.is_burning", "caster.health < 0.5"
- `condition.then`: Modifications to apply

**Tasks**:
- [ ] Create CreativeLLMPrompt class
- [ ] Write comprehensive action documentation
- [ ] Add creative examples for each element combo
- [ ] Include nesting examples
- [ ] Test prompt with qwen2.5:7b

**Time estimate**: 10 minutes

---

### Phase 4: Effect Execution (~15 min)

**File**: `Scripts/Abilities/Execution/ProjectileNode.cs`

**Enhancements needed**:
```csharp
public partial class ProjectileNode : Node2D
{
    public List<EffectAction> OnHitActions { get; set; }
    public EffectContext Context { get; set; }

    private void OnCollision(Node2D target)
    {
        // Execute on_hit actions
        foreach (var action in OnHitActions)
        {
            Interpreter.Execute(action, Context with { Target = target });
        }
    }
}
```

**File**: `Scripts/Abilities/Execution/AreaEffectNode.cs`

**New node for area effects**:
```csharp
public partial class AreaEffectNode : Node2D
{
    public float Radius { get; set; }
    public float Duration { get; set; }
    public int LingeringDamage { get; set; }
    public List<EffectAction> OnExpireActions { get; set; }

    // Visual circle
    // Damage tick timer
    // On expire callback
}
```

**File**: `Scripts/Abilities/Execution/StatusEffectManager.cs`

**Track status effects on entities**:
```csharp
public class StatusEffectManager
{
    public void ApplyStatus(Node target, string status, float duration, bool stacks);
    public bool HasStatus(Node target, string status);
    public int GetStatusStacks(Node target, string status);
    public void ClearStatus(Node target, string status);
}
```

**Tasks**:
- [ ] Enhance ProjectileNode with on_hit support
- [ ] Create AreaEffectNode
- [ ] Create StatusEffectManager
- [ ] Implement damage application
- [ ] Add knockback physics
- [ ] Test each action type individually

**Time estimate**: 15 minutes

---

### Phase 5: Integration & Testing (~10 min)

**File**: `Scripts/Abilities/LLM/AbilityGeneratorV2.cs`

**Updated generator**:
```csharp
public class AbilityGeneratorV2
{
    private readonly LLMClientV2 llmClient;
    private readonly AbilityCache cache;

    public async Task<AbilityV2> GenerateAbilityAsync(List<PrimitiveType> primitives)
    {
        // Check cache
        // Generate with LLM
        // Parse with AbilityBuilder
        // Cache result
        // Return
    }
}
```

**Test Combinations**:
1. **Fire + Ice**: Steam effects, thermal shock, paradox abilities
2. **Poison + Lightning**: Shocking toxins, electrified venom, chain poison
3. **Shadow + Light**: Twilight effects, eclipse abilities, duality
4. **Earth + Wind**: Sandstorms, dust devils, terrain effects
5. **Fire + Shadow** (Already tested): Dark flames, fear + burn

**Tasks**:
- [ ] Create AbilityGeneratorV2
- [ ] Wire up to LLMClientV2
- [ ] Test each element combination
- [ ] Verify caching works
- [ ] Check ability balance
- [ ] Test nested actions execute correctly

**Time estimate**: 10 minutes

---

## Detailed Action Specifications

### spawn_projectile

**Args**:
- `count` (int, 1-5): Number of projectiles
- `pattern` (string): "single", "spiral", "spread", "circle"
- `speed` (float, 200-800): Movement speed

**Behavior**:
- Spawns projectile nodes moving in direction
- Pattern affects spawn angles
- Can have `on_hit` nested actions

**Example**:
```json
{
  "action": "spawn_projectile",
  "args": {"count": 3, "pattern": "spread", "speed": 500},
  "on_hit": [
    {"action": "damage", "args": {"amount": 30}}
  ]
}
```

---

### spawn_area

**Args**:
- `radius` (float, 50-300): Area radius in pixels
- `duration` (float, 1-10): How long area lasts
- `lingering_damage` (int, 0-20): Damage per second (optional)

**Behavior**:
- Creates circular area effect at position
- Deals tick damage if specified
- Can have `on_expire` nested actions

**Example**:
```json
{
  "action": "spawn_area",
  "args": {"radius": 150, "duration": 5, "lingering_damage": 10},
  "on_expire": [
    {"action": "damage", "args": {"amount": 50, "element": "fire"}}
  ]
}
```

---

### damage

**Args**:
- `amount` (int, 10-50): Fixed damage value
- `formula` (string): Expression like "20 * (1 + target.poison_stacks * 0.15)"
- `element` (string): "fire", "ice", "lightning", "poison", etc.
- `area_radius` (float, optional): AOE damage radius

**Behavior**:
- Deals damage to target (or area if radius specified)
- Formula evaluated if present (overrides amount)
- Element affects visuals and damage type

**Examples**:
```json
// Fixed damage
{"action": "damage", "args": {"amount": 30, "element": "fire"}}

// Formula-based
{"action": "damage", "args": {
  "formula": "25 * (1 + target.poison_stacks * 0.2)",
  "element": "poison"
}}

// AOE damage
{"action": "damage", "args": {"amount": 40, "area_radius": 100}}
```

---

### apply_status

**Args**:
- `status` (string): "burning", "frozen", "poisoned", "shocked", "slowed", "stunned", "weakened", "feared"
- `duration` (float, 1-10): How long status lasts
- `stacks` (bool): Whether status can stack

**Behavior**:
- Applies status effect to target
- If stacks=true, multiple applications increase intensity
- Visual feedback based on status type

**Example**:
```json
{
  "action": "apply_status",
  "args": {
    "status": "poisoned",
    "duration": 8,
    "stacks": true
  }
}
```

---

### chain_to_nearby

**Args**:
- `max_chains` (int, 2-5): Maximum number of enemies to chain to
- `range` (float, 100-300): Search radius for chain targets

**Behavior**:
- Finds nearest enemies within range
- Executes nested `on_hit` actions on each
- Visual lightning/chain effect between targets

**Example**:
```json
{
  "action": "chain_to_nearby",
  "args": {"max_chains": 4, "range": 250},
  "on_hit": [
    {"action": "damage", "args": {"amount": 20, "element": "lightning"}},
    {"action": "apply_status", "args": {"status": "shocked", "duration": 2}}
  ]
}
```

---

### knockback

**Args**:
- `force` (float, 100-500): Knockback strength
- `direction` (string): "away" (from caster), "towards" (pull), "up"

**Behavior**:
- Applies physics impulse to target
- Direction relative to caster position
- Can interrupt enemy actions

**Example**:
```json
{
  "action": "knockback",
  "args": {"force": 300, "direction": "away"}
}
```

---

### repeat

**Args**:
- `count` (int, 2-5): Number of repetitions
- `interval` (float, 0.5-2): Delay between repetitions

**Behavior**:
- Executes nested `on_hit` actions multiple times
- Waits `interval` seconds between each
- Useful for multi-hit abilities

**Example**:
```json
{
  "action": "repeat",
  "args": {"count": 3, "interval": 0.8},
  "on_hit": [
    {"action": "damage", "args": {"amount": 15}}
  ]
}
```

---

## Formula System (Optional Advanced Feature)

For `damage.formula` and conditions, support simple expression evaluation:

**Variables Available**:
- `base_damage`: Default damage value
- `target.poison_stacks`: Number of poison stacks on target
- `target.is_burning`: Boolean, true if burning
- `target.is_frozen`: Boolean, true if frozen
- `caster.health`: Caster's current health (0-1 normalized)
- `caster.mana`: Caster's current mana (0-1 normalized)

**Operators**:
- Arithmetic: `+`, `-`, `*`, `/`
- Comparison: `<`, `>`, `<=`, `>=`, `==`
- Logical: `&&`, `||`

**Example Formulas**:
```
"20 * (1 + target.poison_stacks * 0.15)"
"30 * (target.is_burning ? 1.5 : 1.0)"
"25 + (caster.health < 0.5 ? 15 : 0)"
```

**Implementation**: Use a simple recursive descent parser or library like `NCalc` for safe expression evaluation.

---

## Testing Strategy

### Unit Tests

**Test**: Action parsing
```csharp
[Test]
public void TestParseSimpleAction()
{
    var json = "{\"action\":\"damage\",\"args\":{\"amount\":30}}";
    var action = AbilityBuilder.ParseAction(json);
    Assert.Equal("damage", action.Action);
    Assert.Equal(30, action.Args["amount"]);
}
```

**Test**: Nested actions
```csharp
[Test]
public void TestParseNestedActions()
{
    var json = @"{
      ""action"":""spawn_projectile"",
      ""on_hit"":[{""action"":""damage"",""args"":{""amount"":20}}]
    }";
    var action = AbilityBuilder.ParseAction(json);
    Assert.Equal(1, action.OnHit.Count);
}
```

### Integration Tests

**Test**: Full LLM generation
```csharp
[Test]
public async Task TestGenerateFireIceAbility()
{
    var generator = new AbilityGeneratorV2("test_player");
    var result = await generator.GenerateAbilityAsync(
        new List<PrimitiveType> { PrimitiveType.Fire, PrimitiveType.Ice }
    );

    Assert.NotNull(result);
    Assert.Contains("fire", result.Primitives);
    Assert.Contains("ice", result.Primitives);
    Assert.True(result.Effects.Count > 0);
}
```

### Manual Tests

1. **Single element abilities**: Fire only, Ice only
2. **Two element combos**: All 28 combinations (8 choose 2)
3. **Three element combos**: Fire+Ice+Lightning, etc.
4. **Edge cases**: All same element, empty combination
5. **Complex nesting**: 3+ levels deep (projectile → on_hit → area → on_expire)

---

## Migration Notes

### Backwards Compatibility

**Not required** - Clean break from V1 to V2

However, if needed:
- Keep AbilityData.cs for old saves
- Add conversion method: `AbilityV2.FromV1(AbilityData old)`
- Cache migration script

### Cache Format

**Old cache** (V1):
```
ability_cache/player_001.db
  - combo_key TEXT
  - ability_json TEXT (AbilityData format)
  - version INTEGER
  - use_count INTEGER
```

**New cache** (V2):
```
ability_cache/player_001.db
  - combo_key TEXT
  - ability_json TEXT (AbilityV2 format with effect scripts)
  - version INTEGER (2)
  - use_count INTEGER
```

**Migration**: Simple version check in AbilityCache.GetCachedAbility()

---

## Performance Considerations

### LLM Generation Time

- **First generation**: 2-5 seconds (qwen2.5:7b inference)
- **Cached abilities**: Instant (<1ms)
- **Cache hit rate**: Expected 80%+ after 10 unique combos

### Effect Execution

- **Simple ability** (1 projectile, 1 damage): <1ms
- **Complex ability** (5 projectiles, chains, area effects): 2-5ms
- **Max nested depth**: Limit to 5 levels to prevent stack overflow

### Memory

- **qwen2.5:7b model**: ~5GB RAM when loaded
- **Ability cache**: ~2KB per ability
- **Runtime effect nodes**: ~100 bytes per active effect

---

## Risk Mitigation

### LLM Generates Invalid JSON

**Risk**: LLM returns malformed JSON or missing fields

**Mitigation**:
- Use `format: "json"` in Ollama request (enforces valid JSON)
- Comprehensive try/catch in AbilityBuilder
- Fallback to simple default ability
- Log errors for debugging

### LLM Generates Broken Combos

**Risk**: LLM creates overpowered or useless abilities

**Mitigation**:
- Clamp all numerical values (damage: 10-50, cooldown: 0.5-10)
- Validate effect counts (max 5 effects per ability)
- Limit nesting depth (max 5 levels)
- Playtesting and prompt tuning

### Performance Issues

**Risk**: Complex nested abilities cause lag

**Mitigation**:
- Limit max projectiles: 5
- Limit max chains: 5
- Limit area effect count: 3 simultaneous
- Object pooling for projectiles/areas

### Unknown Actions

**Risk**: LLM invents new action names

**Mitigation**:
- Strict action name validation in interpreter
- Log unknown actions for analysis
- Ignore unknown actions (graceful degradation)
- Iterative prompt improvement

---

## Success Criteria

✅ **Feature Complete**:
- [ ] All 9 action types implemented and working
- [ ] Nested actions (on_hit, on_expire) functional
- [ ] LLM generates valid effect scripts 95%+ of time
- [ ] AbilityBuilder parses all generated JSON successfully

✅ **Quality**:
- [ ] Abilities feel creative and unique
- [ ] Element combinations make thematic sense
- [ ] No overpowered abilities slip through
- [ ] Visual feedback for all effect types

✅ **Performance**:
- [ ] Ability generation <5 seconds
- [ ] Effect execution <5ms per ability
- [ ] No memory leaks from active effects
- [ ] Cache system working correctly

✅ **Robustness**:
- [ ] Handles malformed LLM output gracefully
- [ ] No crashes from deeply nested actions
- [ ] Proper cleanup of effect nodes
- [ ] Error messages are helpful

---

## Timeline

**Total estimated time**: ~60 minutes

**Breakdown**:
- Phase 1 (Core): 15 min
- Phase 2 (Parser): 10 min
- Phase 3 (Prompts): 10 min
- Phase 4 (Execution): 15 min
- Phase 5 (Testing): 10 min

**Suggested approach**:
1. Implement phases 1-3 first (core + parsing + prompts)
2. Test LLM generation with minimal execution
3. Implement phase 4 (execution) incrementally
4. Polish and test

---

## Next Steps

1. **Review this plan** - Any questions or concerns?
2. **Begin Phase 1** - Create core effect system files
3. **Iterative development** - Test each phase before moving to next
4. **Gather feedback** - Playtest abilities as they're implemented

---

## Appendix: Example Abilities

### Fire + Ice: "Thermal Shock"
```json
{
  "name": "Thermal Shock",
  "description": "Rapidly alternates between extreme heat and cold, shattering frozen enemies with explosive force.",
  "primitives": ["fire", "ice"],
  "effects": [{
    "script": [
      {"action": "apply_status", "args": {"status": "frozen", "duration": 2}},
      {"action": "damage", "args": {"amount": 15, "element": "ice"}},
      {"action": "spawn_area", "args": {"radius": 120, "duration": 1},
        "on_expire": [
          {"action": "damage", "args": {"amount": 40, "element": "fire"}},
          {"action": "apply_status", "args": {"status": "burning", "duration": 4}}
        ]
      }
    ]
  }],
  "cooldown": 3.5
}
```

### Poison + Lightning: "Toxic Storm"
```json
{
  "name": "Toxic Storm",
  "description": "Electrified venom that chains between poisoned enemies, stacking corruption with each strike.",
  "primitives": ["poison", "lightning"],
  "effects": [{
    "script": [
      {"action": "damage", "args": {
        "formula": "20 * (1 + target.poison_stacks * 0.25)",
        "element": "lightning"
      }},
      {"action": "chain_to_nearby", "args": {"max_chains": 4, "range": 250},
        "on_hit": [
          {"action": "apply_status", "args": {"status": "poisoned", "duration": 6, "stacks": true}},
          {"action": "damage", "args": {"amount": 15, "element": "poison"}}
        ]
      }
    ]
  }],
  "cooldown": 2.0
}
```

### Shadow + Light: "Eclipse"
```json
{
  "name": "Eclipse",
  "description": "A paradox of light and darkness that heals allies while draining life from enemies caught in the twilight.",
  "primitives": ["shadow", "light"],
  "effects": [{
    "script": [
      {"action": "spawn_area", "args": {"radius": 200, "duration": 5},
        "on_expire": [
          {"action": "heal", "args": {"amount": 30}},
          {"action": "damage", "args": {"amount": 25, "element": "shadow"}}
        ]
      },
      {"action": "apply_status", "args": {"status": "feared", "duration": 3}}
    ]
  }],
  "cooldown": 4.5
}
```

---

**Document Version**: 1.0
**Last Updated**: 2026-02-07
**Author**: Claude (Sonnet 4.5) + deebee
**Status**: Ready for Implementation
