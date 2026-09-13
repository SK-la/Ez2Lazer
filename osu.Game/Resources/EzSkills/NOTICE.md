# MinaCalc n-key WASM (minaclac-74.0.wasm)

Provenance for the vendored n-key MinaCalc binary used by the Ez mania skill pipeline.

## Source

- Upstream project: [LeoBlackMT/osumania_map_analyser](https://github.com/LeoBlackMT/osumania_map_analyser)
  ("ManiaMapAnalyser by Leo_Black"), upstream commit `5a6144c` (2026-08-30).
- Vendored copy used as the byte source: mania-hub `live-backend/vendor/leoblack/ett/versions/minaclac-74.0.wasm`.
- Original MinaCalc (Etterna) FFI gate widened from 4/6/7 to 4..18 by upstream PR #62
  (`d2d7561`..`34b96f2`), which also added the structurally identical `0.75.0`.
  We vendor only `0.74.0`.
- License: MIT, Copyright (c) 2026 Leo_Black. MinaCalc itself is MIT (ppy/Etterna lineage).

## Cap patch

The shipped `0.74.0` binary has been byte-patched by the mania-hub fork to lift the
per-skillset SSR clamp from 40 to 100 (`f32.const 40.0` -> `f32.const 100.0`, four
occurrences). Upstream's n-key rebuild silently dropped the clamp lift that the older
wasm builds (`<=0.72.3`) already carried, which pins every top-end skillset at 40.
Do **not** replace this file with a stock upstream `0.74.0` build without re-applying
the patch, or top-end MSD/SSR values will be clamped.

## Hosting

The module is loaded in-process by `EzNKeyWasmModule` (see
`osu.Game/EzOsuGame/Skills/EzNKeyMsdEngine.cs`) through the `Wasmtime` NuGet package.
It exposes `memory`, `__wasm_call_ctors`, `minacalc_compute`, `malloc` and `free`, and
imports only nine host functions (`wasi_snapshot_preview1.fd_{write,read,close,seek}`,
`environ_{sizes_get,get}`, plus `env._abort_js`, `env._tzset_js`, `env.emscripten_resize_heap`),
all of which are stubbed by the host. No WASI filesystem or environment access is used.

`minacalc_compute(keycount, rate, goal, masksPtr, timesPtr, count, outPtr) -> i32`
returns eight `f32` skillsets in the fixed order
`Overall, Stream, Jumpstream, Handstream, Stamina, JackSpeed, Chordjack, Technical`.
There is no separate MSD/SSR switch: `goal = 0.93` is the MSD baseline.

## Updating

Re-copy the file from the same upstream path, confirm the clamp patch is still applied
(check a known above-40 chart, or the `f32.const` byte signature), and re-run the
Ez skills regression tests.

## License

MIT License

Copyright (c) 2026 Leo_Black

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
