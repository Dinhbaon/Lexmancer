# Visual System - Testing Guide

## What We Just Built (Phase 1)

### New Files
1. **`Scripts/Abilities/Visuals/VisualSystem.cs`** - Core visual system
   - Element color mapping (16 elements + neutral)
   - Particle material generator (4 types)
   - Color blending for combinations
   - Glow effect helper

### Modified Files
1. **`ProjectileNodeV2.cs`** - Now uses element colors + particle trails + impact bursts
2. **`AreaEffectNode.cs`** - Now uses element colors + lingering particles + fade animation
3. **`BeamNode.cs`** - Now uses element colors + glow

## Expected Visual Changes

### Before (Old System)
- **Projectiles**: All yellow squares
- **Areas**: All orange circles
- **Beams**: All white lines

### After (New System)
- **Fire abilities**: Orange-red (#FF4500) with fiery trails
- **Water abilities**: Blue (#1E90FF) with flowing particles
- **Earth abilities**: Brown (#8B4513) with rocky particles
- **Lightning abilities**: Gold (#FFD700) with sparking trails
- **Poison abilities**: Purple (#9932CC) with toxic particles
- **Wind abilities**: Sky blue (#87CEEB) with light particles
- **Shadow abilities**: Dark gray (#2F4F4F) with dark particles
- **Light abilities**: Light yellow (#FFFACD) with bright particles

### Visual Features
1. **Projectiles**:
   - Colored square matching element
   - Particle trail behind projectile
   - Glow effect around projectile
   - Particle burst on impact

2. **Area Effects**:
   - Colored circle matching element
   - Lingering particles floating in area
   - Fade-out animation near end of duration

3. **Beams**:
   - Colored line with gradient (bright → faded)
   - Glow effect along beam

## How to Test

### 1. Run the Game
```bash
# In Godot editor, press F5 or click Play
```

### 2. Test Each Element

**Fire (Key 1)**:
- Should see orange-red projectile
- Particle trail should be orange
- Impact should create orange burst

**Water (Key 2)**:
- Should see blue healing effect
- (Note: Water is heal-only, no projectile)

**Earth (Key 3)**:
- Should see brown projectile
- Slower speed
- Brown particle trail

### 3. Test Combinations

**Open TAB → Combine Tab**:
1. Combine Fire + Water = Steam
   - Should see blended color (mix of orange and blue)
2. Combine Fire + Earth = Lava
   - Should see lava-red color (#FF6347)
3. Try casting combined element abilities

### 4. Visual Quality Checks

**Particles**:
- ✅ Trail follows projectile smoothly
- ✅ Burst appears at impact location
- ✅ Area particles float within radius
- ✅ Particles fade out (not pop)

**Colors**:
- ✅ Colors match element theme
- ✅ Combined elements blend colors
- ✅ Glow is visible but not overwhelming

**Performance**:
- ✅ No lag when casting abilities
- ✅ Particles clean up after lifetime
- ✅ No memory leaks

## Troubleshooting

### Issue: All abilities still look the same
**Cause**: Element color not being extracted correctly
**Fix**: Check that `OnHitActions` has a damage action with `element` parameter

### Issue: Particles not showing
**Cause**: GPUParticles2D not emitting
**Fix**:
1. Check `Emitting = true` in code
2. Verify `Amount > 0`
3. Check particle material is assigned

### Issue: Glow not visible
**Cause**: PointLight2D texture missing
**Fix**: Replace `res://icon.svg` with proper glow texture, or it will use fallback

### Issue: Performance lag
**Cause**: Too many particles
**Fix**: Reduce particle counts in `VisualSystem.CreateParticleMaterial()`

## Next Steps

### Phase 2: LLM Visual Descriptions
Add this to LLM prompt in `LLMClientV2.cs`:
```json
"visuals": {
  "projectile": {
    "color": "#FF4500",
    "size": 16,
    "particle_type": "fire",
    "glow_intensity": 0.8
  }
}
```

### Phase 3: LLM Sprite Generation
See `Scripts/Abilities/Visuals/README_FUTURE_LLM_SPRITES.md`

## Known Limitations

1. **No custom sprites** - Currently using ColorRect placeholders
2. **No animation frames** - Static visuals only
3. **Limited particle variety** - Only 4 particle types
4. **No sound** - Visual only
5. **Glow uses placeholder texture** - Replace with proper glow texture for better effect

## Success Criteria

✅ Different elements have different colors
✅ Projectiles leave particle trails
✅ Impact effects spawn burst particles
✅ Area effects have lingering particles
✅ Beams have color gradients
✅ Performance is smooth (60 FPS)
✅ Particles clean up automatically
