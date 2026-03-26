import math, random, statistics

random.seed(42)

N_NOTES = 5000
perfect_windows = {
    'EZ2AC 18ms': 18,
    'IIDX 16.67ms': 16.67,
    'LR2 15ms': 15,
    'Standard 22.4ms': 22.4,
}

player_precision_std = 5.0  # ms

print("=== Audio Jitter Impact on Perfect Rate ===\n")
print("Hypothesis: WASAPI shared mode adds ~3ms audio jitter in lazer")
print("vs ~1ms in stable, causing fewer Perfects with tight windows.\n")

header = f"{'Window':>18s} | {'Engine':>7s} | {'AudioJitter':>10s} | {'TotalStd':>8s} | {'PerfectRate':>10s}"
print(header)
print("-" * len(header))

for pw_label, pw_val in perfect_windows.items():
    for label, audio_jitter_std in [('Stable', 1.0), ('Lazer', 3.0), ('Lazer+5', 5.0)]:
        total_std = math.sqrt(player_precision_std ** 2 + audio_jitter_std ** 2)
        # Monte Carlo simulation
        hits = 0
        for _ in range(N_NOTES):
            offset = random.gauss(0, total_std)
            if abs(offset) <= pw_val:
                hits += 1
        pr = hits / N_NOTES * 100
        print(f"{pw_label:>18s} | {label:>7s} | {audio_jitter_std:>8.1f}ms | {total_std:>6.2f}ms | {pr:>8.1f}%")
    print()

# Alternation analysis
print("\n=== Alternation Analysis ===")
print("With audio jitter, consecutive notes' offsets are INDEPENDENT")
print("(each note has its own audio output timing jitter)")
print("So alternation rate should be ~50% (random)")
print()

for label, audio_jitter_std in [('Stable', 1.0), ('Lazer', 3.0)]:
    total_std = math.sqrt(player_precision_std ** 2 + audio_jitter_std ** 2)
    offsets = [random.gauss(0, total_std) for _ in range(500)]
    alternations = sum(1 for i in range(len(offsets) - 1) 
                      if (offsets[i] > 0) != (offsets[i+1] > 0))
    rate = alternations / (len(offsets) - 1) * 100
    print(f"{label}: alternation rate = {rate:.1f}% (expected ~50%)")

print("\nConclusion: Audio jitter explains FEWER Perfects")
print("but does NOT explain strictly ALTERNATING early-late pattern.")
print("A strictly alternating pattern requires a SYSTEMATIC mechanism.")
