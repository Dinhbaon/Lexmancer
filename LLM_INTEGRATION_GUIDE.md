# LLM Integration Guide

## Overview

The game now supports **LLM-generated element abilities** using a local Ollama server! You can switch between hardcoded abilities (fast, for testing) and LLM-generated abilities (creative, unique abilities).

## Quick Start

### Option 1: Use Hardcoded Abilities (Default - Fastest)

**No setup needed!** The game works out of the box with pre-written abilities.

Just run the game:
```bash
godot .
```

Press F5 and play immediately.

---

### Option 2: Enable LLM Generation (Creative Abilities)

#### Prerequisites:

1. **Install Ollama**: https://ollama.ai
   ```bash
   curl https://ollama.ai/install.sh | sh
   ```

2. **Pull a model** (recommended: qwen2.5:7b):
   ```bash
   ollama pull qwen2.5:7b
   ```

3. **Start Ollama server**:
   ```bash
   ollama serve
   ```

#### Enable in Godot:

1. **Open Godot Editor**
2. **Open Scenes/Main.tscn**
3. **Select the Main node** in the scene tree
4. **In the Inspector**, set:
   - ✅ **Use LLM**: `true`
   - ✅ **Generate Abilities On Startup**: `true` (optional)

5. **Save the scene**
6. **Press F5** to run

## Configuration Options

### In Godot Inspector (Main node):

| Property | Default | Description |
|----------|---------|-------------|
| **Use LLM** | `false` | Enable LLM-generated abilities |
| **Generate Abilities On Startup** | `false` | Generate all 6 abilities at game start (takes 10-30 sec) |

### In Code (Scripts/Config/GameConfig.cs):

```csharp
public static class GameConfig
{
    // LLM Settings
    public static bool UseLLM = false;
    public static string LLMBaseUrl = "http://localhost:11434";
    public static string LLMModel = "qwen2.5:7b";

    // Element Settings
    public static bool GenerateElementAbilitiesOnStartup = false;
    public static bool CacheGeneratedAbilities = true;
}
```

## How It Works

### Element → Primitive Mapping

Elements are mapped to LLM primitives:

| Element | Primitive(s) | LLM Interpretation |
|---------|-------------|-------------------|
| Fire | Fire | Burns, explosive, DoT |
| Water | Ice | Slows, freezes, defensive |
| Earth | Earth | Heavy, knockback, shields |
| Steam | Fire | Hot vapor, area damage |
| Mud | Earth | Heavy, slowing |
| Lava | Fire | Burning pools, DoT |

### Generation Modes

**Mode 1: Startup Generation** (`GenerateAbilitiesOnStartup = true`)
- Generates all 6 abilities when game starts
- Takes 10-30 seconds
- Cached for future runs (instant after first time)
- Best for: Complete experience with unique abilities

**Mode 2: On-Demand Generation** (Coming Soon)
- Generates abilities when you first get an element
- Faster startup
- Progressive loading
- Best for: Faster iteration

## LLM Output Examples

### Fire Element
```json
{
  "name": "Blazing Inferno",
  "description": "Launches a searing fireball that explodes on impact, burning enemies",
  "effects": [{
    "script": [
      {
        "action": "spawn_projectile",
        "args": {"count": 1, "speed": 500},
        "on_hit": [
          {"action": "damage", "args": {"amount": 30, "element": "fire"}},
          {"action": "apply_status", "args": {"status": "burning", "duration": 4}}
        ]
      }
    ]
  }]
}
```

### Steam Element (Fire + Water)
```json
{
  "name": "Scalding Vapor Cloud",
  "description": "Releases a burst of superheated steam that damages and obscures",
  "effects": [{
    "script": [
      {
        "action": "spawn_area",
        "args": {"radius": 150, "duration": 3, "lingering_damage": 12}
      }
    ]
  }]
}
```

## Troubleshooting

### "LLM request failed" errors

**Check Ollama is running:**
```bash
# Should return version info
curl http://localhost:11434/api/version
```

**Check model is available:**
```bash
ollama list
```

**Restart Ollama:**
```bash
killall ollama
ollama serve
```

### Slow generation

- **First run**: LLM needs to load model into memory (~10-30 sec)
- **Subsequent runs**: Cached (instant!)
- **Try faster model**: `llama3.2:3b` (less creative but faster)

### Abilities not working

- LLM may generate invalid JSON → Falls back to hardcoded
- Check console for "LLM generation failed" messages
- Abilities are cached in `~/.local/share/godot/app_userdata/Lexmancer/Cache/`

## Cache Location

Generated abilities are cached at:
```
~/.local/share/godot/app_userdata/Lexmancer/Cache/abilities_player_001.db
```

**Clear cache:**
```bash
rm ~/.local/share/godot/app_userdata/Lexmancer/Cache/abilities_player_001.db
```

## Switching Models

Edit `Scripts/Config/GameConfig.cs`:

```csharp
public static string LLMModel = "llama3.2:3b";  // Faster, less creative
// OR
public static string LLMModel = "qwen2.5:14b";  // Slower, more creative
```

**Popular models:**
- `qwen2.5:7b` (recommended) - Good balance
- `llama3.2:3b` - Fast, works on laptops
- `llama3.1:8b` - Good creativity
- `qwen2.5:14b` - Best quality, needs 16GB+ RAM

## Performance Tips

1. **First playthrough**: Enable LLM, let it generate all abilities once
2. **Testing/iteration**: Disable LLM, use cached/hardcoded abilities
3. **Production**: Ship with hardcoded abilities, offer LLM as optional feature

## Console Output

**With LLM enabled:**
```
=== Game Configuration ===
LLM Enabled: true
  LLM URL: http://localhost:11434
  LLM Model: qwen2.5:7b
  Generate on Startup: true
  Cache Abilities: true
==========================
Generating elements with LLM...
⏳ This may take 10-30 seconds...
Generating ability for Fire...
✨ Generated NEW ability: Blazing Inferno
Generating ability for Water...
✓ Using cached ability: Healing Stream
...
✅ LLM generation complete!
   Generated: 3, Cached: 3
```

**With LLM disabled (default):**
```
=== Game Configuration ===
LLM Enabled: false
==========================
Loading hardcoded elements...
Runtime Cache: 6 elements
```

## Advanced: Custom Prompts

To modify how abilities are generated, edit:
`Scripts/Abilities/LLM/LLMClientV2.cs` → `BuildCreativePrompt()`

Example customizations:
- Add new element characteristics
- Modify damage ranges
- Add custom actions
- Change creativity level

## FAQ

**Q: Do I need internet for LLM?**
A: No! Ollama runs 100% locally.

**Q: How much RAM needed?**
A: 8GB minimum for qwen2.5:7b, 16GB for larger models.

**Q: Can I use OpenAI/Claude instead?**
A: Not yet, but you can modify `LLMClientV2.cs` to call any API.

**Q: Are LLM abilities balanced?**
A: They can be overpowered or underpowered. We clamp damage (10-50) and validate actions for safety.

**Q: Can I edit generated abilities?**
A: Yes! They're cached in SQLite. Edit with DB browser or regenerate with `forceNew: true`.

## Next Steps

1. **Test with hardcoded** (fast, works immediately)
2. **Install Ollama** (when ready for creative abilities)
3. **Enable LLM** in Inspector
4. **Generate once** (cache it)
5. **Ship game** with option to use local LLM or cached abilities

---

**You're all set!** The vertical slice now supports both hardcoded and LLM-generated abilities. 🎮✨
