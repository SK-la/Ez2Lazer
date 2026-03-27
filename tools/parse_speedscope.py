import sys, json, collections

if len(sys.argv) < 2:
    print('Usage: parse_speedscope.py <speedscope.json> [top_n]')
    sys.exit(2)

path = sys.argv[1]
top_n = int(sys.argv[2]) if len(sys.argv) > 2 else 30

with open(path, 'r', encoding='utf-8') as f:
    data = json.load(f)

profiles = data.get('profiles') or []
if not profiles:
    print('no profiles')
    sys.exit(1)

# pick the largest profile by samples length
profile = max(profiles, key=lambda p: len(p.get('samples',[])))
frames = profile.get('frames', [])
samples = profile.get('samples', [])

counter = collections.Counter()
for s in samples:
    # sample is a list of frame indices, or a single int
    if isinstance(s, list):
        for idx in s:
            counter[idx] += 1
    else:
        counter[s] += 1

# map to names
name_counts = collections.Counter()
for idx, cnt in counter.items():
    name = frames[idx].get('name') if idx < len(frames) else f'<idx_{idx}>'
    name_counts[name] += cnt

print(f'Total samples: {sum(counter.values())}')
print('Top functions:')
for name, cnt in name_counts.most_common(top_n):
    print(f'{cnt:10d}  {name}')

# also print top stacks weights? skipped
