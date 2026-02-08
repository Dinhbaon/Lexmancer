# Lexmancer - Game Completion Roadmap

**Target Design**: Element discovery roguelike where players combine elements to unlock new elements, each with unique LLM-generated abilities.

**Completion Status**: ~15% (Core systems only, no gameplay loop)

---

## Phase 1: Core Element System (Foundation)
**Priority**: CRITICAL | **Estimated Time**: 3-5 days

### 1.1 Element Data Architecture
- [ ] Create `Element.cs` class
  - `string Id` (unique identifier: "fire", "steam", "lava")
  - `string Name` (display name: "Flame", "Steam", "Molten Lava")
  - `string Description` (flavor text)
  - `int Tier` (1-4)
  - `List<string> Recipe` (element IDs needed: ["fire", "water"])
  - `AbilityV2 Ability` (what happens when used)
  - `Sprite2D Icon` (visual representation)
  - `Color PrimaryColor` (for VFX)
  - `bool IsDiscovered` (per-player tracking)

- [ ] Create `ElementDatabase.cs`
  - Load/save element definitions (JSON)
  - Query elements by ID, tier, recipe
  - Track valid combinations
  - Version management for updates

- [ ] Create `ElementRegistry.cs` (singleton)
  - Central access point for all elements
  - Initialize on game startup
  - Provide lookup methods

### 1.2 Base Elements Definition
- [ ] Define Tier 1 elements (6 total):
  - **Fire**: Direct damage, DoT specialist
  - **Water**: Healing, defensive utility
  - **Earth**: Knockback, shields, heavy damage
  - **Air**: Speed, mobility, multi-target
  - **Light**: Healing, buffs, purification
  - **Shadow**: Life drain, debuffs, fear

- [ ] Create icons/sprites for each base element
- [ ] Define base ability for each (simple, reliable)
- [ ] Balance tier 1 cooldowns and power

### 1.3 Combination System
- [ ] Create `CombinationValidator.cs`
  - Check if element combo is valid
  - Enforce tier progression rules
  - Handle duplicate detection
  - Validate combination count (2-4 elements)

- [ ] Create `RecipeDatabase.cs`
  - Pre-define Tier 2 combinations (~12)
  - Pre-define Tier 3 combinations (~8)
  - Pre-define Tier 4 combinations (~5)
  - Store in JSON for easy editing

- [ ] Create `ElementCombiner.cs`
  - Take input elements
  - Validate combination
  - Return resulting element
  - Trigger discovery events
  - Non-destructive (keep originals)

### 1.4 Player Element Collection
- [ ] Create `PlayerElementCollection.cs`
  - Track discovered elements per player
  - Track equipped elements (loadout)
  - Persist to save file
  - Query methods (HasElement, GetDiscovered, etc.)

- [ ] Create save/load system for collection
  - JSON or binary serialization
  - Per-player save files
  - Handle version migrations

---

## Phase 2: Element Generation with LLM (Core Loop)
**Priority**: CRITICAL | **Estimated Time**: 2-3 days

### 2.1 Refactor Primitive → Element
- [ ] Rename `PrimitiveType` → `ElementType` (enum)
- [ ] Update `AbilityGeneratorV2` → `ElementGenerator`
- [ ] Change cache keys to use element IDs
- [ ] Update all references throughout codebase

### 2.2 Element LLM Prompts
- [ ] Create prompt for element properties generation
  - Input: recipe (e.g., "fire + water")
  - Output: name, description, visual style, tier
  - Include examples for consistency

- [ ] Create prompt for element ability generation
  - Input: element properties + recipe
  - Output: AbilityV2 with effect scripts
  - Consider element theme and tier
  - Enforce balance constraints by tier

- [ ] Implement hybrid generation approach:
  - Pre-define core elements (Tier 1)
  - LLM generates Tier 2+ elements
  - Human curator can override/refine

### 2.3 Element Cache System
- [ ] Extend `AbilityCache` → `ElementCache`
  - Cache full element definitions
  - Support multi-tier caching
  - Track generation metadata
  - Version and migrate cache

- [ ] Add cache warming on startup
  - Pre-generate common Tier 2 elements
  - Background generation for discoverable elements
  - Progress indicator during loading

### 2.4 Balance System
- [ ] Create `ElementBalancer.cs`
  - Validate generated abilities against constraints
  - Enforce tier-based power budgets
  - Clamp damage/healing/cooldowns
  - Ensure Tier 2+ are specialized, not just stronger

- [ ] Define balance rules per tier:
  - Tier 1: 10-20 damage, 1-2s cooldown
  - Tier 2: 8-18 damage, specialized effects
  - Tier 3: 12-25 damage, multi-effect
  - Tier 4: 15-30 damage, legendary effects

---

## Phase 3: Discovery & Progression (Gameplay Loop)
**Priority**: HIGH | **Estimated Time**: 4-5 days

### 3.1 Discovery Mechanics
- [ ] Create `DiscoveryManager.cs`
  - Handle element unlock events
  - Grant first-time discovery rewards
  - Track discovery statistics
  - Emit events for UI updates

- [ ] Implement discovery rewards:
  - Experience points
  - Gold/currency
  - Achievement triggers
  - Visual celebration (particles, sound)

- [ ] Create hint system:
  - Show "???" for obvious combos
  - Visual cues for valid combinations
  - Unlock hints through progression
  - NPC dialogue with lore clues

### 3.2 Recipe Book UI
- [ ] Create `RecipeBookUI.cs`
  - Grid view of all possible elements
  - Show discovered vs undiscovered ("?")
  - Display element details on hover/click
  - Filter by tier, type, discovered status

- [ ] Element card design:
  - Icon, name, tier indicator
  - Description and flavor text
  - Recipe (ingredients)
  - Ability summary
  - Stats (damage, cooldown)

- [ ] Collection statistics:
  - X/Y elements discovered
  - Progress per tier
  - Rarest elements found
  - Total combinations tried

### 3.3 Combination Experiment Screen
- [ ] Create `CombinationUI.cs`
  - Drag-and-drop element slots (2-4)
  - "Combine" button
  - Result preview (if known)
  - Animation for successful combination
  - Particle effects on discovery

- [ ] Visual feedback:
  - Valid combo: slots glow, hint shown
  - Invalid combo: shake, red flash
  - New discovery: explosion, celebration
  - Already known: simple confirmation

### 3.4 Element Loadout System
- [ ] Create `ElementLoadout.cs`
  - Limited equipped slots (5-8 elements)
  - Drag-and-drop to equip/unequip
  - Save loadout presets
  - Quick-swap during run prep

- [ ] Deck-building constraints:
  - Max 1-2 Tier 4 elements
  - Encourage diverse tiers
  - Cooldown budgeting
  - Strategic tradeoffs

---

## Phase 4: Combat System (Gameplay Core)
**Priority**: CRITICAL | **Estimated Time**: 5-7 days

### 4.1 Enemy System
- [ ] Create `Enemy.cs` base class
  - Implements `IEntity`, `IDamageable`
  - Health, movement, AI
  - Status effect integration
  - Death/loot handling

- [ ] Create enemy types (3-5 initial):
  - Melee chaser
  - Ranged shooter
  - Tank/heavy
  - Fast/swarm
  - Elite/mini-boss

- [ ] Create `EnemySpawner.cs`
  - Wave-based spawning
  - Difficulty scaling
  - Spawn patterns
  - Boss encounters

### 4.2 Combat Abilities Integration
- [ ] Complete `EffectInterpreter.cs` implementation:
  - Actual projectile spawning (not stubs)
  - Collision detection and handling
  - Area effect node creation
  - Beam/laser effects
  - Status effect application

- [ ] Create visual effect nodes:
  - `ProjectileNodeV2.cs` (complete implementation)
  - `AreaEffectNode.cs` (complete implementation)
  - `BeamNode.cs` (new)
  - `ChainLightningNode.cs` (new)

- [ ] Implement damage system:
  - Apply damage to `IDamageable` entities
  - Element type advantages/resistances
  - Damage numbers UI
  - Hit feedback (flash, shake)

### 4.3 Status Effect System
- [ ] Complete `StatusEffectManager.cs`:
  - Apply status by type
  - Stack tracking and intensity
  - Duration countdowns
  - Visual indicators (icons, particle effects)

- [ ] Implement each status type:
  - **Burning**: DoT over time
  - **Frozen**: Movement slow/stun
  - **Poisoned**: Stacking DoT
  - **Shocked**: Chain damage on hit
  - **Slowed**: Movement speed reduction
  - **Stunned**: Cannot act
  - **Weakened**: Reduced damage dealt
  - **Feared**: Run away from source

### 4.4 Player Combat Controller
- [ ] Enhance `PlayerController.cs`:
  - Element ability casting system
  - Cooldown tracking per element
  - Input mapping for element slots (1-8 keys)
  - Aim indicator for targeted abilities
  - Range/targeting display

- [ ] Create `PlayerStats.cs`:
  - Health/max health
  - Movement speed
  - Damage multipliers
  - Cooldown reduction
  - Element affinities

- [ ] Implement player damage/death:
  - Take damage from enemies
  - Health bar UI
  - Death screen
  - Respawn/restart logic

### 4.5 Combat UI
- [ ] Create HUD elements:
  - Health bar
  - Element ability slots with icons
  - Cooldown overlays
  - Status effect indicators
  - Combo/score counter

- [ ] Create targeting UI:
  - Reticle for mouse aiming
  - Range indicators
  - AoE preview circles
  - Chain lightning arcs

---

## Phase 5: Roguelike Structure (Run Progression)
**Priority**: HIGH | **Estimated Time**: 4-6 days

### 5.1 Run Management
- [ ] Create `RunManager.cs`
  - Initialize new run
  - Track run state (floor, enemies killed, time)
  - Handle run completion/failure
  - Generate run rewards
  - Persist run statistics

- [ ] Create run preparation screen:
  - Select element loadout
  - Preview run modifiers
  - Choose starting buffs
  - Confirm and start

### 5.2 Level Generation
- [ ] Create `LevelGenerator.cs`
  - Procedural room generation
  - Room types (combat, treasure, shop, event)
  - Difficulty curve by floor
  - Boss rooms at milestones

- [ ] Create room templates:
  - Arena layouts
  - Obstacle placement
  - Enemy spawn points
  - Environmental hazards

### 5.3 Progression Between Floors
- [ ] Create reward screen after combat:
  - Choose reward type
  - Element discovery opportunities
  - Stat upgrades
  - Currency/gold

- [ ] Create shop system:
  - Spend gold on upgrades
  - Buy element hints
  - Reroll element abilities
  - Healing/restoration

### 5.4 Meta-Progression
- [ ] Create persistent progression:
  - Unlock new base elements
  - Permanent stat upgrades
  - Recipe hints unlocked
  - Starting loadout improvements

- [ ] Create achievement system:
  - Discover X elements
  - Complete run with Tier 1 only
  - Chain 10 enemies
  - Defeat boss without damage

---

## Phase 6: Polish & Game Feel (Quality)
**Priority**: MEDIUM | **Estimated Time**: 3-5 days

### 6.1 Visual Effects
- [ ] Create particle effects per element:
  - Fire: Flames, embers
  - Water: Splashes, droplets
  - Earth: Rocks, dust
  - Air: Wind trails, swirls
  - Light: Radiance, sparkles
  - Shadow: Dark mist, tendrils

- [ ] Create hit effects:
  - Impact flashes
  - Screen shake on heavy hits
  - Slow-motion on kills
  - Element-specific hit VFX

- [ ] Create environment effects:
  - Background parallax
  - Ambient particles
  - Dynamic lighting
  - Weather effects (if element-themed)

### 6.2 Sound Design
- [ ] Create SFX for each element type:
  - Activation sounds
  - Projectile whoosh
  - Impact sounds
  - Status effect audio

- [ ] Create music tracks:
  - Menu/discovery theme
  - Combat theme
  - Boss theme
  - Victory/defeat themes

- [ ] Implement audio mixing:
  - Priority system for important sounds
  - Ducking for UI sounds
  - Spatial audio for positional effects

### 6.3 UI/UX Polish
- [ ] Create main menu:
  - New run
  - Recipe book
  - Settings
  - Quit

- [ ] Create pause menu:
  - Resume
  - View recipe book
  - Check stats
  - Abandon run

- [ ] Animation/transitions:
  - Smooth screen transitions
  - Element card reveals
  - Combo animations
  - Victory celebrations

### 6.4 Juice & Feel
- [ ] Screen shake system:
  - Variable intensity
  - Frequency controls
  - Damping over time

- [ ] Hit-stop/freeze frames:
  - Pause on heavy hits
  - Emphasize critical moments

- [ ] Camera effects:
  - Zoom on discoveries
  - Follow projectiles
  - Recoil on player damage

---

## Phase 7: Content Creation (Elements)
**Priority**: MEDIUM | **Estimated Time**: 2-3 days

### 7.1 Pre-Design Tier 2 Elements (~12)
- [ ] Fire + Water = **Steam** (healing + damage hybrid)
- [ ] Fire + Earth = **Lava** (slow DoT specialist)
- [ ] Fire + Air = **Lightning** (fast multi-target)
- [ ] Water + Earth = **Mud** (slow/trap specialist)
- [ ] Water + Air = **Ice** (freeze/control)
- [ ] Earth + Air = **Sand** (vision obscure, area denial)
- [ ] Light + Fire = **Radiance** (AoE healing + damage)
- [ ] Shadow + Water = **Mist** (stealth/evasion)
- [ ] Light + Shadow = **Twilight** (hybrid light/dark)
- [ ] Shadow + Fire = **Inferno** (fear + burn)
- [ ] Earth + Shadow = **Obsidian** (defense + counter-damage)
- [ ] Air + Shadow = **Void** (pulls enemies, darkness)

### 7.2 Pre-Design Tier 3 Elements (~8)
- [ ] Fire + Water + Air = **Storm** (lightning chains + rain healing)
- [ ] Fire + Earth + Air = **Volcano** (explosive AoE)
- [ ] Water + Earth + Air = **Tsunami** (wave knockback)
- [ ] Fire + Light + Shadow = **Eclipse** (paradox effects)
- [ ] Water + Light + Shadow = **Abyss** (deep water drain)
- [ ] Earth + Light + Shadow = **Crystal** (reflect + shield)
- [ ] Ice + Lightning + Poison = **Frozen Storm** (status overload)
- [ ] Lava + Steam + Lightning = **Magma Core** (massive DoT)

### 7.3 Pre-Design Tier 4 Legendary (~5)
- [ ] Fire + Water + Earth + Air = **Primordial Chaos** (all elements)
- [ ] Light + Shadow + Fire + Air = **Supernova** (massive burst)
- [ ] Earth + Water + Shadow + Air = **Black Hole** (pull + consume)
- [ ] Fire + Light + Lightning + Radiance = **Solar Flare** (burning light)
- [ ] Ice + Shadow + Void + Abyss = **Absolute Zero** (freeze time)

### 7.4 Generate Abilities with LLM
- [ ] Generate abilities for all Tier 2 elements
- [ ] Generate abilities for all Tier 3 elements
- [ ] Generate abilities for all Tier 4 elements
- [ ] Manual balance pass on all generated abilities
- [ ] Playtest and iterate on each element

---

## Phase 8: Testing & Balance (Quality Assurance)
**Priority**: CRITICAL | **Estimated Time**: 3-5 days

### 8.1 Unit Testing
- [ ] Test element combination validation
- [ ] Test effect interpreter actions
- [ ] Test damage calculations
- [ ] Test status effect stacking
- [ ] Test save/load system

### 8.2 Integration Testing
- [ ] Test full run from start to finish
- [ ] Test all element combinations
- [ ] Test enemy spawn/defeat cycles
- [ ] Test discovery and progression
- [ ] Test loadout system

### 8.3 Balance Testing
- [ ] Playtest each element for viability
- [ ] Test tier progression feel
- [ ] Test run difficulty curve
- [ ] Test reward pacing
- [ ] Test meta-progression rate

### 8.4 Bug Fixing
- [ ] Fix critical bugs (crashes, data loss)
- [ ] Fix gameplay bugs (softlocks, exploits)
- [ ] Fix UI bugs (alignment, clarity)
- [ ] Fix audio bugs (missing sounds, loops)

---

## Phase 9: Tutorial & Onboarding (Player Experience)
**Priority**: MEDIUM | **Estimated Time**: 2-3 days

### 9.1 Tutorial Content
- [ ] Create tutorial level:
  - Introduce movement and basic combat
  - Explain element system
  - Guide first combination
  - Show recipe book

- [ ] Create tutorial hints:
  - Context-sensitive tooltips
  - Highlight important UI elements
  - Explain status effects on first application
  - Guide to first boss

### 9.2 Help System
- [ ] Create in-game help menu:
  - Element system explanation
  - Combat mechanics
  - Status effects reference
  - Controls reference

- [ ] Create tooltips everywhere:
  - Element cards
  - Status effect icons
  - UI buttons
  - Ability descriptions

---

## Phase 10: Optimization & Performance (Technical)
**Priority**: LOW | **Estimated Time**: 2-3 days

### 10.1 Performance Optimization
- [ ] Profile and optimize hot paths
- [ ] Implement object pooling for projectiles
- [ ] Optimize particle effects
- [ ] Batch rendering where possible
- [ ] Reduce GC allocations

### 10.2 Memory Management
- [ ] Implement effect node pooling
- [ ] Limit max concurrent effects
- [ ] Cleanup on scene transitions
- [ ] Monitor memory leaks

### 10.3 Load Time Optimization
- [ ] Async resource loading
- [ ] Cache prewarming
- [ ] Background LLM generation
- [ ] Loading screen with progress

---

## Completion Criteria & Launch Checklist

### Minimum Viable Product (MVP)
- [ ] 6 base elements implemented
- [ ] 12 Tier 2 elements discoverable
- [ ] Combination system working
- [ ] Combat loop functional
- [ ] 3-5 enemy types
- [ ] 1 boss encounter
- [ ] Recipe book UI
- [ ] Basic run structure (5-10 floors)
- [ ] Save/load working
- [ ] Tutorial complete

### Full V1.0 Launch
- [ ] All planned elements (27-33) implemented
- [ ] All 4 tiers with legendary elements
- [ ] Multiple enemy types and bosses
- [ ] Meta-progression system
- [ ] Polish pass (VFX, SFX, animations)
- [ ] Balanced and playtested
- [ ] Achievement system
- [ ] Settings (audio, controls, graphics)
- [ ] Credits screen
- [ ] Bug-free (or close to it)

---

## Estimated Timeline Summary

| Phase | Priority | Time Estimate | Dependencies |
|-------|----------|---------------|--------------|
| 1. Core Element System | Critical | 3-5 days | None |
| 2. Element Generation | Critical | 2-3 days | Phase 1 |
| 3. Discovery & Progression | High | 4-5 days | Phases 1-2 |
| 4. Combat System | Critical | 5-7 days | Phases 1-2 |
| 5. Roguelike Structure | High | 4-6 days | Phase 4 |
| 6. Polish & Game Feel | Medium | 3-5 days | Phase 5 |
| 7. Content Creation | Medium | 2-3 days | Phases 2-6 |
| 8. Testing & Balance | Critical | 3-5 days | Phases 1-7 |
| 9. Tutorial | Medium | 2-3 days | Phase 8 |
| 10. Optimization | Low | 2-3 days | All phases |

**Total Estimated Time**: 30-45 days of focused development

**MVP Milestone**: 15-20 days (Phases 1-2-4 + minimal content)

---

## Success Metrics

### Player Engagement
- Average session length: 20-30 minutes
- Completion rate for first run: 60%+
- Discovery rate: Players find 70%+ of elements
- Return rate: 50%+ players return after first session

### Technical Performance
- <100ms frame time (10+ FPS minimum)
- <3 second load times
- <5 second LLM generation (cached instant)
- <100MB RAM usage

### Content Quality
- LLM-generated elements feel creative 80%+ of time
- Manual curation required <20% of generations
- Players report elements feel balanced
- No "solved" meta builds dominate >40% of runs

---

**Document Version**: 1.0
**Created**: 2026-02-07
**Status**: ACTIVE ROADMAP
**Next Action**: Begin Phase 1 - Core Element System
