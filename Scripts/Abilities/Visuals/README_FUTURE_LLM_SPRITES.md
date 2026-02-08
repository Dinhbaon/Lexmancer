# Future: LLM-Generated Sprites and Animations

## Current State (Phase 1 - DONE)
- ✅ Element-based color tinting
- ✅ Procedural particle systems
- ✅ Dynamic glow effects
- All visuals use `VisualSystem.cs` for consistent theming

## Phase 2: LLM Visual Descriptions (Next)
Extend the LLM prompt to include detailed visual descriptions:

```json
{
  "name": "Fireball",
  "visuals": {
    "projectile": {
      "description": "A swirling orb of orange flames with red core",
      "color": "#FF4500",
      "secondary_color": "#DC143C",
      "size": 16,
      "particle_type": "fire",
      "trail": true,
      "glow_intensity": 0.8
    },
    "impact": {
      "description": "Explosive burst with embers scattering outward",
      "particle_burst": 25,
      "screen_shake": 3
    }
  }
}
```

**Implementation:**
1. Add `visuals` section to `LLMClientV2.cs` prompt
2. Parse visual metadata in `AbilityBuilder.cs`
3. Pass to `ProjectileNodeV2`, `AreaEffectNode`, `BeamNode`
4. Use description for debugging/UI tooltips

## Phase 3: LLM-Generated Sprites (Future)

### Architecture

```
LLM Ability Generator (text)
    ↓
Visual Description Extractor
    ↓
Image Generation API (DALL-E, Stable Diffusion, etc.)
    ↓
Sprite Processor (crop, scale, cleanup)
    ↓
Animation Frame Generator (for multi-frame sprites)
    ↓
Godot Texture Import
    ↓
ProjectileNodeV2 / AreaEffectNode uses Sprite2D
```

### Integration Points

**1. Extend `LLMClientV2.cs`:**
```csharp
public async Task<GeneratedSprite> GenerateSprite(string visualDescription)
{
    // Call image generation API
    var imageUrl = await imageGenAPI.Generate(visualDescription);

    // Download and process
    var texture = await DownloadAndConvertToTexture(imageUrl);

    // Cache sprite
    SpriteCache.Store(visualDescription, texture);

    return new GeneratedSprite { Texture = texture };
}
```

**2. Update `ProjectileNodeV2.cs`:**
```csharp
public override void _Ready()
{
    if (VisualData?.GeneratedSprite != null)
    {
        // Use LLM-generated sprite
        var sprite = new Sprite2D();
        sprite.Texture = VisualData.GeneratedSprite;
        AddChild(sprite);
    }
    else
    {
        // Fallback to procedural visuals (current system)
        var visual = VisualSystem.CreateProjectileVisual(elementColor, size);
        AddChild(visual);
    }
}
```

### Image Generation Options

**Option A: DALL-E 3 API**
- Pros: High quality, good at following descriptions
- Cons: Expensive, requires OpenAI API key, slower
- Cost: ~$0.04 per image (1024x1024)

**Option B: Stable Diffusion (Local)**
- Pros: Free, fast if you have GPU, unlimited generations
- Cons: Requires setup, hardware requirements, quality varies
- Setup: Run locally via `stable-diffusion-webui` API

**Option C: Hybrid Approach (Recommended)**
- Generate sprite sheets for element archetypes (8 base elements)
- LLM picks and recolors existing sprites
- Only generate new sprites for unique combinations
- Reduces cost and generation time

### Prompt Engineering for Sprites

```csharp
private string BuildSpritePrompt(string abilityDescription)
{
    return $@"Generate a pixel art game sprite for:
    {abilityDescription}

    Style:
    - 32x32 pixels
    - Top-down 2D roguelike
    - Bright, saturated colors
    - Clear silhouette
    - Transparent background
    - Glowing effect around edges

    Format: PNG with alpha channel";
}
```

### Animation Generation

For multi-frame animations:
1. Generate 4-8 frames per ability
2. Assemble into sprite sheet
3. Use `AnimatedSprite2D` instead of `Sprite2D`
4. Cache animations by element id or ability signature

### Caching Strategy

```
~/.lexmancer/sprite_cache/
  ├── fire_projectile_v1.png
  ├── water_heal_v2.png
  ├── lava_pool_v1.png
  └── manifest.json  # Maps descriptions to files
```

**Benefits:**
- Avoid regenerating same sprites
- Consistent visuals across sessions
- Share sprites between players
- Version control for improvements

### Performance Considerations

1. **Generation is SLOW** (2-10 seconds per sprite)
   - Generate asynchronously
   - Show loading indicator
   - Use placeholder visuals during generation

2. **Texture memory** (each 32x32 sprite = ~4KB)
   - Monitor VRAM usage
   - Limit cache size (e.g., 1000 sprites max)
   - Use texture atlases for better performance

3. **Quality control**
   - LLMs can generate bad sprites
   - Implement rating system (👍👎)
   - Regenerate low-rated sprites
   - Fallback to procedural if generation fails

### Testing Plan

1. Generate sprites for all 8 base elements
2. Test with 10 random combinations
3. Measure generation time and quality
4. User playtest for feedback
5. Iterate on prompts

### Estimated Timeline

- **Phase 2** (Visual descriptions): 1 day
- **Phase 3a** (Basic sprite generation): 3-5 days
- **Phase 3b** (Animation frames): 2-3 days
- **Phase 3c** (Polish & caching): 2 days

**Total**: ~2 weeks for full LLM sprite generation

---

## Next Steps

1. ✅ Phase 1 complete (color tinting + particles)
2. ⬜ Add visual metadata to LLM prompt
3. ⬜ Choose image generation API
4. ⬜ Build sprite pipeline
5. ⬜ Integrate with ability system
6. ⬜ Add caching and quality control
