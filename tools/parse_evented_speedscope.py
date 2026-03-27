import sys
import json
from collections import defaultdict

if len(sys.argv) < 2:
    print("usage: parse_evented_speedscope.py <speedscope.json>")
    sys.exit(2)

path = sys.argv[1]
with open(path, 'r', encoding='utf-8') as f:
    j = json.load(f)

# find frames array
frames = None
if 'shared' in j and 'frames' in j['shared']:
    frames = j['shared']['frames']
elif 'frames' in j:
    frames = j['frames']
else:
    # sometimes profiles contain frames per-profile
    frames = None

# helper to resolve frame id to string
def frame_name(frame_idx, profile_frames=None):
    try:
        if frames:
            f = frames[frame_idx]
            return f.get('name') or f.get('file') or str(frame_idx)
        if profile_frames is not None:
            f = profile_frames[frame_idx]
            return f.get('name') or f.get('file') or str(frame_idx)
    except Exception:
        return str(frame_idx)
    return str(frame_idx)

agg = defaultdict(float)

for profile in j.get('profiles', []):
    if profile.get('type') != 'evented':
        continue
    unit = profile.get('unit', 'milliseconds')
    events = profile.get('events', [])
    # some profiles have their own frames
    prof_frames = profile.get('frames')

    stack = []
    last_ts = None
    for ev in events:
        ts = ev.get('at')
        typ = ev.get('type')
        frame_idx = ev.get('frame')
        # measure time elapsed since last event and attribute to current stack
        if last_ts is not None and ts is not None and ts >= last_ts:
            dur = ts - last_ts
            if dur > 0 and stack:
                # attribute full duration to top of stack
                top = stack[-1]
                name = frame_name(top, prof_frames)
                agg[name] += dur
        last_ts = ts if ts is not None else last_ts

        if typ == 'O':
            stack.append(frame_idx)
        elif typ == 'C':
            # pop until matching frame (some traces may duplicate)
            if stack:
                try:
                    # prefer popping last occurrence
                    if stack[-1] == frame_idx:
                        stack.pop()
                    else:
                        # remove last occurrence of frame_idx
                        for i in range(len(stack)-1, -1, -1):
                            if stack[i] == frame_idx:
                                stack.pop(i)
                                break
                except Exception:
                    stack = []
            else:
                stack = []

# convert units to ms if needed
scale = 1.0
# common units: "milliseconds", "seconds"
if unit == 'seconds':
    scale = 1000.0

# print top frames
items = sorted(agg.items(), key=lambda x: x[1], reverse=True)
print(f"Total frames tracked: {len(items)}")
print("Top frames (time in ms):")
for name, t in items[:40]:
    print(f"{t*scale:.3f}\t{name}")

# also print totals
total = sum(agg.values())*scale
print(f"\nTotal attributed time: {total:.3f} ms")
