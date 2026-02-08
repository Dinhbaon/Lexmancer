# Ability System Summary

## Core Concept: Element Combination System

### Philosophy
Instead of combining letters/primitives to create terminal **abilities**, the system creates **elements** that:
- ARE useable (have abilities/effects)
- CAN be combined further (non-terminal nodes)
- Create infinite depth of strategic possibilities

### System Flow
```
Letters/Primitives → Elements → More Elements → Even More Elements...
     ↓                  ↓              ↓                 ↓
  (useable)        (useable)      (useable)         (useable)
```

**Example Chain:**
- Fire (useable) + Water (useable) = **Steam** (useable AND combinable)
- Steam + Earth = **Mud** (useable AND combinable)
- Mud + Air = **Dust** (useable AND combinable)
- etc.

---

## Design Decisions

### 1. Power Curve: Different/Specialized > Raw Power

**Principle:** Deeper combinations = more specialized/situational, NOT just stronger

**Why:** Prevents "meta" rush to best combos; keeps all tiers relevant

**Examples:**
- **Tier 1 - Fire:** 10 damage to single target (simple, reliable)
- **Tier 2 - Steam (Fire+Water):** 6 damage + heals 4 (hybrid utility)
- **Tier 2 - Lava (Fire+Earth):** 15 damage over time but slower (DoT specialist)
- **Tier 3 - Lightning (Fire+Air):** 8 damage to 3 enemies (spread damage)

### 2. Resource Balance: Keep Elements (Non-Destructive)

**Principle:** Combining requires having both, but you keep them

**Why:** Prevents hoarding; encourages experimentation

**How It Works:**
- You have Fire + Water → You **unlock** Steam
- You can still use Fire, Water, **AND** Steam independently
- All three remain in your collection

**Optional Twist:** Limit how many you can equip for a battle (deck-building mechanic)

### 3. Discovery UX: Hybrid System

**Obvious Combos:** Show hints for intuitive ones
- Fire + Water shows "???" = hints at Steam
- Visual cues suggest common combinations

**Hidden Combos:** Rare/exotic combinations require experimentation
- Shadow + Light = ???
- Ice + Lightning + Poison = ???

**Discovery Rewards:** First-time combos give bonuses
- XP, gold, achievement unlocks
- Encourages trying new combinations

**Recipe Book:** Shows discovered combos, leaves undiscovered as "?"
- Visual progress tracker
- Can review what you've found

**Lore Clues:** NPCs/environmental hints
- "Legends speak of frozen fire..."
- "The ancient text mentions combining storm and earth..."

### 4. Complexity Cap: 3-4 Levels Maximum

**Principle:** Cap at 4 tiers to maintain meaningful combinations

**Why:**
- Beyond 4 levels, combinations become arbitrary
- Easier to balance and playtest
- Still provides dozens of elements without overwhelming

**Structure with 6 Starting Primitives:**

#### Tier 1: Base Elements (6)
- Fire, Water, Earth, Air, Light, Shadow
- Simple, reliable, foundational

#### Tier 2: Two-Way Combos (10-12)
- **Maximum possible:** C(6,2) = 15
- **Curated:** ~10-12 meaningful combinations only
- Examples: Steam, Lava, Ice, Lightning, Mist, etc.

#### Tier 3: Three-Way Combos (8-10)
- More selective, rarer combinations
- Examples: Storm (Air+Water+Lightning), Magma (Fire+Earth+Lava), etc.
- Higher specialization

#### Tier 4: Four-Way Combos (3-5 Legendary)
- **Maximum possible:** C(6,4) = 15
- **Curated:** 3-5 endgame legendary elements
- Extremely rare, powerful, specialized

**Total Elements:** ~27-33 elements (curated)
**vs Maximum Possible:** 56 elements (if all combos valid)

---

## LLM Integration (Ollama)

### Current Implementation
- **Library:** OllamaSharp for C#/Godot
- **Model:** qwen2.5:7b (best quality/speed balance)
- **Generation Time:** 2-5 seconds for first generation, instant for cached
- **Caching:** SQLite per-player ability cache

### What LLM Generates

**Currently (V2):**
- Effect scripts (flexible action-based system)
- Creative combinations of spawning, damage, modifiers
- Nested effects (on_hit, on_expire)

**For Element System:**
- **Element Properties:** Name, description, visual style
- **Element Abilities:** What happens when you USE the element
- **Combination Logic:** Creative effects when elements interact
- **Flavor Text:** Lore-friendly descriptions

### Hybrid Approach (Recommended)

**Pre-define:**
- Core mechanics (damage values, effect types)
- Balance constraints (cooldowns, costs)
- Combination rules (what can combine with what)

**LLM Generates:**
- Flavor text and descriptions
- Visual effect descriptions
- Creative ability descriptions
- Unique mechanics within constraints

**Human Curates:**
- Review for consistency
- Balance pass
- Ensure thematic fit

### Example LLM Generation

**Input:** "Fire + Water combination"

**LLM Outputs (3 variations):**
1. "Scalding Steam: Deals 7 damage and reduces enemy vision"
2. "Misty Vapor: Heals 5 HP and grants evasion"
3. "Boiling Geyser: Area damage with knockback"

**Designer Picks:** The one that fits balance/theme best

---

## Combinatorial Math

### Starting with 6 Base Elements

**Maximum Possible Combinations:**
- Tier 1: 6 base
- Tier 2: C(6,2) = 15 two-way
- Tier 3: C(6,3) = 20 three-way
- Tier 4: C(6,4) = 15 four-way
- **Total: 56 elements**

**Curated Approach (Recommended):**
- Tier 1: 6 base
- Tier 2: ~10-12 (skip nonsensical combos)
- Tier 3: ~8-10 (more selective)
- Tier 4: ~3-5 (legendary only)
- **Total: ~27-33 elements**

### Why Curated > Maximum?
1. **Quality over Quantity:** Each element feels meaningful
2. **Design Intent:** Only combinations that make thematic sense
3. **Balance:** Easier to tune 30 elements than 56
4. **Discovery:** Less overwhelming for players
5. **Content:** More feasible to create art/VFX for each

---

## Strategic Depth

### Player Choices

**During Discovery:**
- Which elements to combine first?
- Chase specific element you want, or experiment?
- Risk trying unknown combos vs using proven ones?

**During Combat:**
- Use basic element for reliability?
- Use advanced element for power/specialization?
- Save rare elements for boss fights?

**Deck Building (if equipped slots limited):**
- Bring generalist elements for flexibility?
- Bring specialists for known challenges?
- Balance tier 1 (reliable) vs tier 4 (powerful)?

### Progression Feel

**Early Game (Tier 1-2):**
- Learning base elements
- Discovering first combinations
- "Oh cool, Fire + Water makes Steam!"

**Mid Game (Tier 2-3):**
- Experimenting with three-way combos
- Building element "loadouts"
- Finding synergies

**Late Game (Tier 3-4):**
- Hunting legendary combinations
- Min-maxing builds
- Mastering rare element timings

---

## Implementation Notes

### Data Structure

```csharp
public class Element
{
    public string Id { get; set; }              // "fire", "steam", "lava"
    public string Name { get; set; }            // "Flame", "Steam", "Molten Lava"
    public string Description { get; set; }     // Flavor text
    public int Tier { get; set; }               // 1-4
    public List<string> Recipe { get; set; }    // ["fire", "water"] for Steam
    public AbilityV2 Ability { get; set; }      // What it does when used
    public bool IsDiscovered { get; set; }      // Has player found it?
}
```

### Generation Flow

```csharp
// Player combines Fire + Water
var baseElements = new List<string> { "fire", "water" };

// Check if combination is valid
if (CombinationDatabase.IsValidCombo(baseElements))
{
    // Get or generate the resulting element
    var newElement = await ElementGenerator.GetOrCreateElement(baseElements);

    // newElement.Id = "steam"
    // newElement.Ability = (LLM-generated ability for steam)
    // newElement can NOW be combined with other elements

    // Mark as discovered for this player
    PlayerProgress.UnlockElement("steam");
}
```

### LLM Prompt Strategy

**For Element Properties:**
```
"Generate a unique element from combining Fire + Water.
Consider: How do these elements interact? What emerges?
Format: {name, description, visual_style, tier}"
```

**For Element Abilities:**
```
"Create an ability for the Steam element (Fire + Water).
Should feel like: misty, scalding, between solid and gas
Use effect scripting system: spawn_projectile, damage, apply_status, etc."
```

---

## Benefits of This System

### For Players
- ✨ **Infinite Discovery:** Always something new to find
- 🧩 **Strategic Depth:** Meaningful choices at all levels
- 🎮 **Natural Progression:** Feels earned, not grindy
- 🔬 **Experimentation:** Safe to try new things (non-destructive)
- 📖 **Collection Aspect:** "Gotta find them all"

### For Designers
- 🎯 **Focused Content:** Can curate 30 quality elements
- ⚖️ **Easier Balance:** Smaller, more manageable set
- 🎨 **Creative Freedom:** LLM helps with variety
- 🔧 **Flexible:** Can add new base elements later
- 📊 **Trackable:** Analytics on popular combos

### For Development
- 🤖 **LLM-Assisted:** Reduces manual content creation
- 💾 **Cacheable:** Instant after first generation
- 🧪 **Testable:** Limited set easier to QA
- 🔄 **Iterative:** Can refine prompts to improve quality
- 🌐 **Scalable:** Add new tiers/elements post-launch

---

## Open Questions / Future Considerations

### Combination Rules
- Can you skip tiers? (Tier 1 + Tier 3 → Tier 4?)
- Or must you combine within/adjacent tiers only?
- Does combining two Tier 2 elements create Tier 3?

### Discovery Pacing
- How fast should players discover combinations?
- Should hints unlock over time or based on progress?
- Random discovery vs guided progression?

### Balancing Act
- How to prevent "solved" meta builds?
- Should some combos be intentionally suboptimal but fun?
- Rotate/patch balance like a live service game?

### Multiplayer Implications
- Can players share discovered elements?
- Trade elements between players?
- Show "X% of players have discovered this"?

### Monetization (if applicable)
- Cosmetic variants of elements?
- "Booster packs" to hint at rare combos?
- Early access to new base elements?

---

## Next Steps

1. **Prototype Element Database**
   - Define the 6 base elements
   - Pre-design 10-15 tier 2 combinations
   - Establish naming/theming conventions

2. **Refactor Primitive → Element**
   - Rename `PrimitiveType` to `Element`
   - Update `AbilityGenerator` to `ElementGenerator`
   - Change cache keys to support element IDs

3. **Implement Combination Validator**
   - Logic for which elements can combine
   - Tier progression rules
   - Recipe database (curated combos)

4. **Update LLM Prompts**
   - New prompt for element generation
   - New prompt for element abilities
   - Include tier context in prompts

5. **UI for Element Discovery**
   - Visual feedback when new element unlocked
   - Recipe book / collection view
   - Combination experiment screen

6. **Playtest & Iterate**
   - Does progression feel good?
   - Are combinations intuitive?
   - Is the LLM generating quality elements?

---

**Last Updated:** 2026-02-07
**Version:** 1.0 (Element System Design)
