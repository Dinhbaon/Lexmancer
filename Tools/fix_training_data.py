#!/usr/bin/env python3
"""
Fix malformed JSON in ability_data_training.jsonl
Extracts all valid ability objects and creates a proper JSON array.
"""

import json
import re
from pathlib import Path

def extract_json_objects(text):
    """Extract individual JSON objects from malformed file."""
    objects = []
    # Pattern to match complete ability objects
    # We look for objects that start with "name" field
    pattern = r'\{\s*"name":\s*"([^"]+)"[^}]*?"ability":\s*\{.*?\n\s*\}\s*\}'

    # More robust: find all complete {...} blocks that contain "name" and "ability"
    depth = 0
    current_obj = ""
    in_object = False

    for char in text:
        if char == '{':
            if depth == 0:
                in_object = True
                current_obj = ""
            depth += 1

        if in_object:
            current_obj += char

        if char == '}':
            depth -= 1
            if depth == 0 and in_object:
                in_object = False
                # Try to parse this object
                try:
                    obj = json.loads(current_obj)
                    # Validate it has required fields
                    if 'name' in obj and 'ability' in obj:
                        objects.append(obj)
                        print(f"✓ Extracted: {obj['name']}")
                except json.JSONDecodeError as e:
                    print(f"✗ Failed to parse object (skipping): {str(e)[:50]}")
                    continue

    return objects

def validate_ability(ability_obj):
    """Validate an ability object has required structure."""
    required_fields = ['name', 'description', 'color', 'ability']
    for field in required_fields:
        if field not in ability_obj:
            return False, f"Missing field: {field}"

    ability = ability_obj['ability']
    if 'primitives' not in ability or len(ability['primitives']) != 2:
        return False, "Invalid primitives (must have exactly 2)"

    if 'effects' not in ability or not ability['effects']:
        return False, "Missing effects"

    if 'cooldown' not in ability:
        return False, "Missing cooldown"

    return True, "OK"

def main():
    input_file = Path(__file__).parent / "ability_data_training.jsonl"
    output_file = Path(__file__).parent / "ability_data_training_fixed.json"

    print(f"Reading: {input_file}")
    with open(input_file, 'r', encoding='utf-8') as f:
        content = f.read()

    print(f"\nExtracting JSON objects...")
    objects = extract_json_objects(content)

    print(f"\n{'='*60}")
    print(f"Extracted {len(objects)} ability objects")
    print(f"{'='*60}\n")

    # Validate all objects
    valid_objects = []
    invalid_count = 0

    print("Validating objects...")
    for obj in objects:
        is_valid, msg = validate_ability(obj)
        if is_valid:
            valid_objects.append(obj)
        else:
            invalid_count += 1
            print(f"✗ Invalid: {obj.get('name', 'UNKNOWN')} - {msg}")

    print(f"\n{'='*60}")
    print(f"Valid objects: {len(valid_objects)}")
    print(f"Invalid objects: {invalid_count}")
    print(f"{'='*60}\n")

    # Save fixed JSON
    print(f"Writing fixed JSON to: {output_file}")
    with open(output_file, 'w', encoding='utf-8') as f:
        json.dump(valid_objects, f, indent=2, ensure_ascii=False)

    print(f"\n✅ Success! Wrote {len(valid_objects)} valid abilities")
    print(f"   Output: {output_file}")

    # Print summary stats
    print(f"\n{'='*60}")
    print("SUMMARY STATISTICS")
    print(f"{'='*60}")

    element_combos = {}
    for obj in valid_objects:
        prims = tuple(sorted(obj['ability']['primitives']))
        element_combos[prims] = element_combos.get(prims, 0) + 1

    print(f"Unique element combinations: {len(element_combos)}")
    print(f"Average abilities per combo: {len(valid_objects) / len(element_combos):.1f}")

    return len(valid_objects)

if __name__ == "__main__":
    count = main()
    exit(0 if count > 0 else 1)
