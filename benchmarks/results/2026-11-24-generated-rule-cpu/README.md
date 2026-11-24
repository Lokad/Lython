# B1565 CPU attribution for generated rules

All three scripts run against the frozen `5d54f7afb` image used in
the CPython comparison. Its runtime and tests match `566259ebc`.
The scripts exercise computed defaults, eager annotations and mutable
captures. The active runtime threads complete 3,712, 8,064 and 3,456
invocations respectively during the sampled windows.

| Nearest resolved runtime group | Default rules | Annotated rules | Captured rules |
| --- | ---: | ---: | ---: |
| Native and ordinary instruction dispatch | 379 | 295 | 418 |
| Function and defaults construction | 77 | 111 | 108 |
| Entry and operand movement | 149 | 124 | 144 |
| Lexical binding operations | 0 | 0 | 31 |
| Numeric arithmetic | 38 | 115 | 70 |
| Admission or retirement | 147 | 115 | 118 |
| Other resolved runtime | 417 | 420 | 301 |
| Total with a resolved runtime ancestor | 1,207 | 1,180 | 1,190 |

On these paths, most samples at `MemoryGovernor.Reserve` have
specific construction callers. Default rules show 49 under
`CreateFunctionValue`, 46 under `AllocateExecutableDefaults` and
31 under `AdmitUnpackedTuple`. Annotated rules show 55 under eager
annotation preparation and 44 under default storage. Captured rules
show 61 under callable construction, 20 under unpacked return
preparation and 19 under capture construction. Lookup and capture
construction remain a smaller direct category than dispatch and
entry. These results support investigating repeated call/factory work
shared across the three shapes. They do not justify replacing
cooperative admission or widening the function layout.

The quiet Azure F4as_v7 runs Ubuntu 24.04.4 and .NET 11 RC3.
Each complete warm batch is sampled at 199 Hz for six seconds
after at least eight seconds of guest warm-up on November 24,
2026 UTC. All 32 image files match. All three goldens, both quiet gates,
worker joins, fixture cleanup and persistent verification pass.
The samples include the wider request. The three runs have 87,
59 and 102 unmapped leaves. Nearest categories are heuristic,
inclusive ancestry overlaps and each script completes different
work per invocation. Counts locate costs and give no standalone
speed estimate.

[Attribution](attribution.json) · [Image identity](identity.json) ·
[Golden checks](verification.json) · [Raw traces](observations.tar.xz) ·
[Procedures](procedures.tar.xz) · [Terminal receipt](terminal-status.json) ·
[External comparison](../2026-11-24-rule-closures-cpython/README.md).
