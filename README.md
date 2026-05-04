# Lexmancer

A Godot 4.5 game featuring procedurally generated element combinations powered by local LLM inference.

## Stack

- **Engine**: Godot 4.5 with C# scripting
- **LLM**: LlamaSharp (bundled GGUF model) with Ollama HTTP fallback
- **Architecture**: Event bus pattern for loose coupling between systems

## Elements

**Base Elements** (8 total): Fire, Water, Earth, Lightning, Poison, Wind, Shadow, Light

**Dynamic Combinations**: Elements combine into new abilities via LLM inference:
- Fire + Water = Steam Burst
- Water + Earth = Mud Trap
- Fire + Earth = Lava Pool

Elements differ only by **status effects**: Burning, Slowed, Stunned, Shocked, Poisoned. All base damage and delivery methods are equalized.

## Controls

| Input | Action |
|--------|--------|
| WASD | Move |
| Space | Dash |
| 1-3 | Select hotbar slot |
| TAB | Open combination panel |
| Left Click | Fire ability |
| R | Restart (on game over) |

## Architecture

```
Services (autoload singletons)
├── EventBus          # Decoupled event system
├── ElementService    # Element definitions and combinations
├── CombatService     # Damage application
├── ConfigService     # LLM/GPU configuration
└── ServiceLocator    # Service locator pattern

UI (CanvasLayer-based, resolution-independent)
├── HealthBar         # Anchor-based fill bar
├── ElementHotbar    # 3 equipped slots
├── CombinationPanel  # TAB: Combine/Inventory tabs
└── GameOverScreen   # ProcessMode=Always for input while paused
```

## LLM Integration

GPU auto-detection configures optimal layer offloading based on VRAM:
- 4GB+: 99 layers (full GPU)
- 3-4GB: 99 layers
- 2.5-3GB: 50 layers
- 2-2.5GB: 25 layers
- <2GB: CPU only

Model: Granite-3.1-3B-A800M-Instruct-Q4_K_M (~3GB, Apache 2.0)

## Project Structure

```
Scripts/
├── Abilities/
│   ├── Execution/     # MeleeAttackNode, ProjectileNode, AreaEffectNode
│   └── LLM/          # ModelManager, LLMClient, prompts
├── Combat/            # HealthComponent, DamageSystem
├── Core/              # EventBus, ServiceLocator, GameManager
├── Elements/          # ElementDefinitions, PlayerElementInventory
├── Enemies/           # BasicEnemy chaser
├── Services/           # ElementService, CombatService, ConfigService
├── Systems/           # WaveSpawner
└── UI/                # HealthBar, Hotbar, CombinationPanel
```
