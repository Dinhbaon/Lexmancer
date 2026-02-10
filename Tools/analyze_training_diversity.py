#!/usr/bin/env python3
"""
Analyze diversity and quality of ability training data.
Provides metrics to assess if dataset is sufficient for training.
"""

import json
import math
from pathlib import Path
from collections import Counter, defaultdict
from typing import List, Dict, Any

class DiversityAnalyzer:
    def __init__(self, data: List[Dict[str, Any]]):
        self.data = data
        self.n = len(data)

    def shannon_entropy(self, counts: Counter) -> float:
        """Calculate Shannon entropy for distribution."""
        total = sum(counts.values())
        if total == 0:
            return 0.0

        entropy = 0.0
        for count in counts.values():
            if count > 0:
                p = count / total
                entropy -= p * math.log2(p)
        return entropy

    def analyze_element_combinations(self):
        """Analyze element pair diversity."""
        combos = Counter()
        for entry in self.data:
            prims = tuple(sorted(entry['ability']['primitives']))
            combos[prims] += 1

        # Calculate coverage (how many possible combinations are present)
        # With 8 elements, there are C(8,2) = 28 possible pairs
        max_possible = 28
        coverage = len(combos) / max_possible * 100

        # Calculate entropy (evenness of distribution)
        entropy = self.shannon_entropy(combos)
        max_entropy = math.log2(len(combos)) if combos else 0
        evenness = (entropy / max_entropy * 100) if max_entropy > 0 else 0

        return {
            'unique_combinations': len(combos),
            'max_possible': max_possible,
            'coverage_percent': coverage,
            'distribution': dict(combos),
            'entropy': entropy,
            'max_entropy': max_entropy,
            'evenness_percent': evenness,
            'min_samples': min(combos.values()) if combos else 0,
            'max_samples': max(combos.values()) if combos else 0,
            'avg_samples': sum(combos.values()) / len(combos) if combos else 0
        }

    def analyze_actions(self):
        """Analyze action type diversity."""
        actions = Counter()
        action_sequences = []

        for entry in self.data:
            sequence = []
            for effect in entry['ability']['effects']:
                for script in effect['script']:
                    action = script.get('action', 'unknown')
                    actions[action] += 1
                    sequence.append(action)
            action_sequences.append(tuple(sequence))

        # Sequence diversity
        unique_sequences = len(set(action_sequences))
        sequence_entropy = self.shannon_entropy(Counter(action_sequences))

        return {
            'action_distribution': dict(actions),
            'unique_actions': len(actions),
            'entropy': self.shannon_entropy(actions),
            'unique_sequences': unique_sequences,
            'sequence_entropy': sequence_entropy,
            'sequence_diversity_percent': unique_sequences / self.n * 100
        }

    def analyze_melee_attacks(self):
        """Analyze melee attack diversity."""
        shapes = Counter()
        movements = Counter()
        shape_movement_combos = Counter()

        for entry in self.data:
            for effect in entry['ability']['effects']:
                for script in effect['script']:
                    if script.get('action') == 'spawn_melee':
                        args = script.get('args', {})
                        shape = args.get('shape', 'unknown')
                        movement = args.get('movement', 'stationary')

                        shapes[shape] += 1
                        movements[movement] += 1
                        shape_movement_combos[(shape, movement)] += 1

        if not shapes:
            return None

        return {
            'shapes': dict(shapes),
            'movements': dict(movements),
            'shape_movement_combos': dict(shape_movement_combos),
            'shape_entropy': self.shannon_entropy(shapes),
            'movement_entropy': self.shannon_entropy(movements)
        }

    def analyze_status_effects(self):
        """Analyze status effect diversity."""
        statuses = Counter()

        def extract_statuses(script_item):
            if script_item.get('action') == 'apply_status':
                status = script_item.get('args', {}).get('status', 'unknown')
                statuses[status] += 1
            for on_hit in script_item.get('on_hit', []):
                extract_statuses(on_hit)

        for entry in self.data:
            for effect in entry['ability']['effects']:
                for script in effect['script']:
                    extract_statuses(script)

        return {
            'distribution': dict(statuses),
            'unique_statuses': len(statuses),
            'entropy': self.shannon_entropy(statuses)
        }

    def analyze_complexity(self):
        """Analyze ability complexity."""
        script_lengths = []
        nested_depths = []

        def get_max_depth(script_item, depth=0):
            max_d = depth
            for on_hit in script_item.get('on_hit', []):
                max_d = max(max_d, get_max_depth(on_hit, depth + 1))
            return max_d

        for entry in self.data:
            total_scripts = 0
            max_depth = 0

            for effect in entry['ability']['effects']:
                for script in effect['script']:
                    total_scripts += 1
                    max_depth = max(max_depth, get_max_depth(script))

            script_lengths.append(total_scripts)
            nested_depths.append(max_depth)

        return {
            'avg_scripts_per_ability': sum(script_lengths) / len(script_lengths),
            'min_scripts': min(script_lengths),
            'max_scripts': max(script_lengths),
            'avg_nesting_depth': sum(nested_depths) / len(nested_depths),
            'max_nesting_depth': max(nested_depths),
            'complexity_entropy': self.shannon_entropy(Counter(script_lengths))
        }

    def analyze_parameters(self):
        """Analyze parameter value diversity."""
        damage_values = []
        duration_values = []
        cooldown_values = []

        def extract_values(script_item):
            if script_item.get('action') == 'damage':
                damage_values.append(script_item.get('args', {}).get('amount', 0))
            if script_item.get('action') == 'apply_status':
                duration_values.append(script_item.get('args', {}).get('duration', 0))
            for on_hit in script_item.get('on_hit', []):
                extract_values(on_hit)

        for entry in self.data:
            cooldown_values.append(entry['ability'].get('cooldown', 0))

            for effect in entry['ability']['effects']:
                for script in effect['script']:
                    extract_values(script)

        def value_stats(values, name):
            if not values:
                return None
            return {
                'min': min(values),
                'max': max(values),
                'avg': sum(values) / len(values),
                'unique_values': len(set(values)),
                'entropy': self.shannon_entropy(Counter(values))
            }

        return {
            'damage': value_stats(damage_values, 'damage'),
            'duration': value_stats(duration_values, 'duration'),
            'cooldown': value_stats(cooldown_values, 'cooldown')
        }

    def calculate_overall_diversity_score(self, analyses):
        """Calculate overall diversity score (0-100)."""
        scores = []

        # Element coverage (0-100)
        scores.append(analyses['elements']['coverage_percent'])

        # Element evenness (0-100)
        scores.append(analyses['elements']['evenness_percent'])

        # Action sequence diversity (0-100)
        scores.append(analyses['actions']['sequence_diversity_percent'])

        # Complexity variety (normalized entropy)
        max_complexity_entropy = math.log2(5)  # Assume 5 reasonable complexity levels
        complexity_score = (analyses['complexity']['complexity_entropy'] / max_complexity_entropy * 100)
        scores.append(min(complexity_score, 100))

        # Status effect variety (assume 10 possible statuses)
        status_coverage = (analyses['status_effects']['unique_statuses'] / 10 * 100)
        scores.append(min(status_coverage, 100))

        return sum(scores) / len(scores)

    def generate_report(self):
        """Generate comprehensive diversity report."""
        print("=" * 80)
        print("ABILITY TRAINING DATA DIVERSITY ANALYSIS")
        print("=" * 80)
        print(f"Total Entries: {self.n}\n")

        # Element combinations
        elem_analysis = self.analyze_element_combinations()
        print("─" * 80)
        print("1. ELEMENT COMBINATIONS")
        print("─" * 80)
        print(f"Unique combinations: {elem_analysis['unique_combinations']}/{elem_analysis['max_possible']}")
        print(f"Coverage: {elem_analysis['coverage_percent']:.1f}%")
        print(f"Samples per combo: {elem_analysis['min_samples']}-{elem_analysis['max_samples']} (avg: {elem_analysis['avg_samples']:.1f})")
        print(f"Distribution evenness: {elem_analysis['evenness_percent']:.1f}% (100% = perfectly balanced)")
        print(f"Entropy: {elem_analysis['entropy']:.2f} bits (max: {elem_analysis['max_entropy']:.2f})")

        # Show top/bottom combinations
        sorted_combos = sorted(elem_analysis['distribution'].items(), key=lambda x: x[1], reverse=True)
        print(f"\nTop 5 combinations:")
        for combo, count in sorted_combos[:5]:
            print(f"  {count:3d}  {combo[0]:<10} + {combo[1]}")
        print(f"\nBottom 5 combinations:")
        for combo, count in sorted_combos[-5:]:
            print(f"  {count:3d}  {combo[0]:<10} + {combo[1]}")

        # Actions
        action_analysis = self.analyze_actions()
        print("\n" + "─" * 80)
        print("2. ACTION TYPES")
        print("─" * 80)
        print(f"Unique actions: {action_analysis['unique_actions']}")
        print(f"Action entropy: {action_analysis['entropy']:.2f} bits")
        print(f"Unique action sequences: {action_analysis['unique_sequences']} ({action_analysis['sequence_diversity_percent']:.1f}%)")
        print(f"Sequence entropy: {action_analysis['sequence_entropy']:.2f} bits")

        total_actions = sum(action_analysis['action_distribution'].values())
        print(f"\nAction distribution:")
        for action, count in sorted(action_analysis['action_distribution'].items(), key=lambda x: x[1], reverse=True):
            pct = count / total_actions * 100
            print(f"  {count:3d} ({pct:5.1f}%)  {action}")

        # Melee attacks
        melee_analysis = self.analyze_melee_attacks()
        if melee_analysis:
            print("\n" + "─" * 80)
            print("3. MELEE ATTACKS")
            print("─" * 80)
            print(f"Shapes: {len(melee_analysis['shapes'])} (entropy: {melee_analysis['shape_entropy']:.2f})")
            for shape, count in melee_analysis['shapes'].items():
                print(f"  {count:3d}  {shape}")

            print(f"\nMovements: {len(melee_analysis['movements'])} (entropy: {melee_analysis['movement_entropy']:.2f})")
            for movement, count in melee_analysis['movements'].items():
                print(f"  {count:3d}  {movement}")

        # Status effects
        status_analysis = self.analyze_status_effects()
        print("\n" + "─" * 80)
        print("4. STATUS EFFECTS")
        print("─" * 80)
        print(f"Unique statuses: {status_analysis['unique_statuses']}")
        print(f"Entropy: {status_analysis['entropy']:.2f} bits")
        for status, count in sorted(status_analysis['distribution'].items(), key=lambda x: x[1], reverse=True):
            print(f"  {count:3d}  {status}")

        # Complexity
        complexity_analysis = self.analyze_complexity()
        print("\n" + "─" * 80)
        print("5. ABILITY COMPLEXITY")
        print("─" * 80)
        print(f"Scripts per ability: {complexity_analysis['min_scripts']}-{complexity_analysis['max_scripts']} (avg: {complexity_analysis['avg_scripts_per_ability']:.1f})")
        print(f"Nesting depth: 0-{complexity_analysis['max_nesting_depth']} (avg: {complexity_analysis['avg_nesting_depth']:.1f})")
        print(f"Complexity entropy: {complexity_analysis['complexity_entropy']:.2f} bits")

        # Parameters
        param_analysis = self.analyze_parameters()
        print("\n" + "─" * 80)
        print("6. PARAMETER VALUES")
        print("─" * 80)

        for param_name, stats in param_analysis.items():
            if stats:
                print(f"{param_name.upper()}:")
                print(f"  Range: {stats['min']:.1f} - {stats['max']:.1f} (avg: {stats['avg']:.1f})")
                print(f"  Unique values: {stats['unique_values']}")
                print(f"  Entropy: {stats['entropy']:.2f} bits")

        # Overall score
        analyses = {
            'elements': elem_analysis,
            'actions': action_analysis,
            'status_effects': status_analysis,
            'complexity': complexity_analysis
        }
        overall_score = self.calculate_overall_diversity_score(analyses)

        print("\n" + "=" * 80)
        print("OVERALL DIVERSITY SCORE")
        print("=" * 80)
        print(f"Score: {overall_score:.1f}/100")

        if overall_score >= 80:
            verdict = "✅ EXCELLENT - High diversity"
        elif overall_score >= 60:
            verdict = "✓ GOOD - Acceptable diversity"
        elif overall_score >= 40:
            verdict = "⚠️  MODERATE - Could use more variety"
        else:
            verdict = "❌ LOW - Needs significant expansion"

        print(f"Verdict: {verdict}")

        # Recommendations
        print("\n" + "=" * 80)
        print("RECOMMENDATIONS")
        print("=" * 80)

        if self.n < 500:
            print("❌ Sample size too small for fine-tuning")
            print(f"   Current: {self.n} | Recommended: 500-1000+ | Ideal: 2000+")
        elif self.n < 1000:
            print("⚠️  Sample size on the low end")
            print(f"   Current: {self.n} | Recommended: 1000+ for robust training")
        else:
            print("✅ Sample size adequate for fine-tuning")

        if elem_analysis['avg_samples'] < 20:
            print(f"⚠️  Only {elem_analysis['avg_samples']:.1f} samples per element combo")
            print("   Recommend: 30-50+ samples per combination for good coverage")

        if elem_analysis['evenness_percent'] < 70:
            print(f"⚠️  Uneven distribution ({elem_analysis['evenness_percent']:.1f}% evenness)")
            print("   Some combinations are underrepresented - consider balancing")

        if action_analysis['sequence_diversity_percent'] < 80:
            print(f"⚠️  Low sequence diversity ({action_analysis['sequence_diversity_percent']:.1f}%)")
            print("   Consider adding more varied ability mechanics")

        print("\n" + "=" * 80)

def main():
    # Try to load fixed file first, fall back to original
    tools_dir = Path(__file__).parent
    fixed_file = tools_dir / "ability_data_training_fixed.json"
    original_file = tools_dir / "ability_data_training.jsonl"

    if fixed_file.exists():
        print(f"Loading: {fixed_file}\n")
        with open(fixed_file, 'r') as f:
            data = json.load(f)
    else:
        print(f"Fixed file not found, attempting to load: {original_file}")
        print("(Run fix_training_data.py first for best results)\n")

        # Try to parse the broken file
        with open(original_file, 'r') as f:
            content = f.read()

        # Quick and dirty extraction
        import re
        depth = 0
        current_obj = ""
        data = []

        for char in content:
            if char == '{':
                if depth == 0:
                    current_obj = ""
                depth += 1

            if depth > 0:
                current_obj += char

            if char == '}':
                depth -= 1
                if depth == 0:
                    try:
                        obj = json.loads(current_obj)
                        if 'name' in obj and 'ability' in obj:
                            data.append(obj)
                    except:
                        pass

    if not data:
        print("❌ No data loaded! Fix the JSON file first.")
        return 1

    analyzer = DiversityAnalyzer(data)
    analyzer.generate_report()

    return 0

if __name__ == "__main__":
    exit(main())
