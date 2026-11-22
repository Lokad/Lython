# B1329 Current factory admission and invocation attribution

Profiled retained `318bf0e0a` on November 22, 2026 UTC on the quiet
Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3. This is the exact
32-file image used by B1327. The independently warmed processes
sample complete batches, including captured output, request handling
and prior-result cleanup. Compilation, startup and teardown are
separate. The capture is diagnostic attribution.

| Nearest resolved runtime method or admission caller | Mixed factory | Dynamic factory | Fully supplied helper |
| --- | ---: | ---: | ---: |
| All process samples | 1,464 | 1,424 | 1,502 |
| Unmapped leaf | 96 | 87 | 106 |
| Instruction prefix | 230 | 195 | 364 |
| MemoryGovernor.Reserve | 174 | 198 | 0 |
| └ Callable construction | 78 | 69 | 0 |
| └ Capture construction | 55 | 46 | 0 |
| └ Inline keyword-default admission | 34 | 43 | 0 |
| └ Dynamic positional tuple storage | 0 | 37 | 0 |
| Native definition dispatcher | 141 | 127 | 0 |
| StartNativeCall | 65 | 52 | 35 |
| General argument binding | 40 | 44 | 0 |

Callable and capture admission remain visible after folding literal
defaults. Each of the mixed factory's nested callables retains one cell
directly. A unified private definition preflight can displace a resize
and commit without changing cell storage or default evaluation. Preserve
live aliases, eager name order, decorator preflight, terminal refusal
and cleanup after suspension. Compare complete factories with both
allocation and timing controls. Sample counts include useful work
and inlining moves attribution between symbols.

All three CPython goldens matched before staging. Each worker verified
output before and after capture. Both machine gates passed. Workers
joined and the persistent archive verified under the lease before RAM
fixtures were removed.

[Attribution](attribution.json) · [Raw profiles](observations.tgz) ·
[Image verification and procedures](procedures.tar.xz).
