# Bounded executable CPU attribution

Source **2fd1ccfd**, 2026-10-31 UTC, dedicated Ubuntu 24.04.4 Azure F4as_v7,
four EPYC 9V45 vCPUs / 16 GiB, pinned .NET 11 RC3. This is an external diagnostic,
not a throughput/CPython comparison or a change to Lython's runtime policies.
Both quiet-machine gates pass. The four fixed workload capture windows total
**13.10 seconds**, below the 60-second limit; there are no recaptures.

Question: which work inside the hot instruction/call paths warrants a coherent
redesign? The [prior capture](../2026-10-31-architecture-profiles/README.md) had
mostly empty DWARF caller chains. This one uses saved frame-pointer chains plus
JIT code bytes to resolve that attribution gap. All profiling and native-address
analysis live outside production; Lython remains managed and memory-safe.

## Collection and limits

The existing [capture driver](ops-profile.py) uses the maintained worker/catalog
and complete stdout/file goldens before/after every case. Warmup and settling
precede each fixed batch of 512 warm executions. Perf cpu-clock samples at 199 Hz
with frame-pointer chains; the CLR emits optimized instruction-loop disassembly
to a separate file. These diagnostics are explicitly recorded in each response
record. [collect.sh](collect.sh) preserves the lease, gates and 180-second owned
process-group bound; workers/profilers finish and are joined.

| Existing script | Capture wall seconds | Guest-thread CPU seconds | Guest samples |
| --- | ---: | ---: | ---: |
| CSV helpers | 4.20 | 3.37 | 667 |
| Text helpers | 3.38 | 2.55 | 507 |
| JSON-lines helpers | 4.46 | 3.59 | 713 |
| File search control | 1.06 | 0.29 | 56 |

Ordinary tiering remains enabled, matching the prior diagnostic. Background JIT
still consumes 0.56/0.48/0.70/0.43 CPU seconds during these windows. Thus this is
not steady-state timing qualification; the small control especially cannot
support latency conclusions. Analysis explicitly selects the recorded Lython
execution thread. The original max-CPU heuristic selected the JIT thread in the
control (84 mostly unknown samples); the corrected 56 guest samples are derived
from the same saved events, with no rerun or hidden omission.

All guest sample chains have multiple frames in this capture. They can still skip
managed/native frames; do not describe them as exact or complete logical stacks.
Unknown guest leaves remain 62/28/54/8 respectively. Approximate sampled self/
inclusive counts are not exact execution-time accounting or a predicted speedup.

## Findings

Instruction-loop self samples are 88/667 CSV, 73/507 text and 109/713 JSON
(13–15%). [Offset analysis](ops-offsets.py) matches each live method to its unique
perf-map range by method identity and total code size, then maps IPs to the saved
JIT blocks. There are no ambiguous method matches. The combined instruction-fetch/
checkpoint block contains 43/88, 41/73 and 51/109 of those self samples: roughly
6–8% of guest samples. Index advance and opcode dispatch are separately visible.
This is not evidence to weaken cancellation or terminal-admission checks.
The loop already reads instructions by readonly reference; a proposal to remove
whole-instruction copies there would target code that is already gone.

Monitor Enter/Exit self samples total 71 CSV (10.6%), 30 text (5.9%) and 100 JSON
(14.0%). Caller chains now identify concrete native paths: dictionary insertion,
CSV field/record construction, executable working storage, text split/builders,
JSON string/number token scratch and result strings. Some frames are inlined or
missing; inspect source before assigning every native lock to one subsystem.
This supports capacity-level ownership/admission work when a specific path is
chosen, not reopening heap tracing or changing the borrowed-budget authority.

The text case has **57/507 samples (11.2%) in SigmaPropertyContains**. Current
source already maps Unicode runes directly, so old per-character CLR-string
conversion is not the remaining issue. Ordinary lowercasing still decodes/maps/
encodes each scalar and performs Unicode context-property lookup across ASCII
text. A bulk native ASCII-run casing path is the strongest bounded next text
pipeline candidate; Unicode context/expansion behavior must remain intact.

The data does not establish boxed arithmetic or call/frame transport as the sole
dominant explanation for the remaining CPython gap. Do not introduce a universal
tagged-value representation, JIT or another activation pool on this evidence.
JSON token-working-storage consolidation is a separate candidate; its source
currently reserves private scratch even on direct native string-token paths.
Neither candidate's elapsed benefit is established by these samples alone.

## Evidence and stopping point

[raw-capture.tar.gz](raw-capture.tar.gz) preserves perf data, exact original driver,
disassembly/maps, logs, identities, all responses, thread CPU deltas and gates.
Each case directory adds corrected [guest leaf](csv-warm/guest-cpu-leaves.json),
[caller-chain](csv-warm/guest-call-chains.json) and
[dispatch-block](csv-warm/dispatch-offsets.json) analyses. The readable sources
[guest leaves](analyze-cpu-guest.py), [guest chains](ops-chains-guest.py) and
[offsets](ops-offsets.py) reproduce those derivations from the archived events.
The original generic analyzer/report remains in the archive, including its
documented incorrect thread selection for the control. No measurement is replaced.

Stop this investigation: caller attribution and a concrete next pipeline are now
identified. PLAN.md records the bulk casing hypothesis, semantic/ownership checks,
fixed complete-script comparison and rejection criterion before implementation.
This record supplies no speedup claim and does not update the primary CPython table.
