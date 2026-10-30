# Performance against CPython

| Workload | Input | Lython | CPython | Lython / CPython | Status |
| --- | --- | ---: | ---: | ---: | --- |
| CSV summary/helper | 4,096 records / 57,361 bytes | 8.559 ms | 3.756 ms | 2.28× | Diagnostic; 2.3% Lython IQR |
| Text summary/helper | 3,072 lines / 35,840 bytes | 6.607 ms | 1.481 ms | 4.46× | Diagnostic; 3.2% Lython IQR |
| JSON config read/edit/write | 16 paths / 181 bytes | 3.057 ms | 1.568 ms | 1.95× | Diagnostic; 4.4% Lython IQR |
| JSON-lines helper | 4,096 records / 110,592 bytes | 9.260 ms | 4.689 ms | 1.97× | Diagnostic; 7.6% Lython IQR |
| Copy/checksum/manifest | Two files / 4,107 bytes | 2.337 ms | 0.598 ms | 3.91× | Diagnostic; 6.5% Lython IQR |
| Search with early exit | 42,010 bytes | 2.307 ms | 0.394 ms | — | Inconclusive; 147% Lython IQR |

Measured **2026-10-30**, 0.11.0 development revision **`4433986e`**, .NET 11 RC3;
[complete results and exact identities](../benchmarks/results/2026-10-30-native-json-writer/README.md).
One fixed seven-job collection took 57.57 seconds, including a separate warm JSON
case below. All jobs passed complete output/file checks; both quiet machine gates
passed. Separately, 33 representative cases matched CPython through verification,
warm execution, fresh compilation/run and compilation-only smoke checks. These
short observations are diagnostic, not qualified release multipliers. A ratio
divides Lython elapsed time by CPython elapsed time; above one means slower.
IQR is interquartile range divided by median, not a confidence interval. A dash
denotes an inconclusive ratio, not zero. No repeat collection or favorable sample
selection was used.

The [previous six-job table](../benchmarks/results/2026-10-29-delivery011/README.md)
records `da0ff811`, including its severely variable rows. It remains historical;
these separately collected medians do not establish a speedup between revisions.
The [foundation matrix](../benchmarks/results/2026-10-29-foundation/README.md) and
[older diagnostic table](../benchmarks/results/2026-10-18-text-milestone/README.md)
also describe earlier sources and boundaries.

## Measurement boundary

The table measures **fresh compilation plus one synchronous execution in an
already-running host**. Each job includes invocation setup, complete file work,
reset/output checks and relevant cleanup. Startup/transport are excluded. Both
engines execute identical Python source and validate complete stdout/file results.
The host supplies efficient ranged reads/appends; whole-file compatibility
fallbacks are not an explanation of these gaps.

Machine: dedicated Azure **Standard_F4as_v7**, Ubuntu 24.04.4 x86-64, four AMD
EPYC 9V45 vCPUs, one thread/core, 16 GiB RAM, no swap. Reference: **CPython
3.13.16**, GCC 13.3, PGO/LTO, ordinary GIL/GC, no JIT/debug mode, launched `-I -S`.
Candidate SDK/runtime: 11.0.100-rc.3.26522.148 / 11.0.0-rc.3.26522.148.
This collection compares development sources on the same runtime; the separate
[historical published 0.10.0 comparison](../benchmarks/results/2026-10-29-delivery011/README.md#published-lython-separately)
uses its actual NuGet core on .NET 10.0.12. Engines ran sequentially with ordinary
GC/tiering.

Keep distinct measurements for:

| Boundary | Includes |
| --- | --- |
| Warm execution | Reused compiled code plus public invocation/setup, workload and declared cleanup; excludes compilation/startup |
| Fresh compile/run | Fresh frontend work plus one complete execution in a warm host |
| Compilation | Frontend/code preparation only; not inferred by subtracting unrelated medians |
| Startup | A new process and its explicitly declared first-result boundary |

The same 2026-10-30 collection separately measures **warm** config read/edit/write
with 1,024 paths / 7,177 bytes: Lython 3.549 ms, CPython 1.787 ms. The Lython IQR
is 36.3%, so its ratio and the old/new change remain inconclusive. Compilation
and startup are excluded from that row. No new compilation-only or startup
performance claim is made.

The separate foundation report separates historical warm and warm-async rows from fresh
compile/run. Its async host operations completed synchronously; it does not
measure suspended I/O. The same report records separate compilation-only rows
(development 146–330 microseconds, CPython 31–57 microseconds), a single
ordinary-tiering empty-worker startup smoke (158/29.5 ms), and frontend allocation
diagnostics. Startup includes loading, empty compilation/execution and process
exit; it is not a generic host estimate. A separate
[runtime-phase and delayed-I/O diagnostic](../benchmarks/results/2026-10-29-runtime-boundaries/README.md)
at revision 1c5cff2a separates setup, guest/host work, projection and cleanup.
Large aggregation cost remains mostly inside execution; setup/projection are
small in that experiment. All owners return to baseline. It uses instrumented
clocks and one injected host delay, supplies no CPython ratio or qualified
speedup, and does not update the primary table.
Do not infer these boundaries by subtracting unrelated medians.

## Evidence and reproduction

Use the [existing comparison tools](../benchmarks/COMPARISON.md) and complete
workload goldens. Development uses short focused diagnostics; ten-minute lanes
are infrequent milestones. Qualification requires stable controls and declared
machine/execution boundaries, not a favorable point sample.

[Earlier-Lython improvements and measurement history](../benchmarks/HISTORY.md)
are separate from the CPython baseline. [Experiments](../benchmarks/EXPERIMENTS.md)
retain profiles, rejected changes and inconclusive results. They are historical
evidence, not current architecture or supported runtime contracts.

The [explicit-call activation diagnostic](../benchmarks/results/2026-10-29-explicit-call-frames/README.md)
compares development revisions c0846e69 and 4b408097 on the same net11 runtime.
It confirms bounded CLR stack growth for ordinary calls, with unchanged CSV
compile/run, a roughly 3.6% async-CSV increase and inconclusive noisy rows.
It establishes no qualified speedup and does not update the table's revision.

The [prepared-scope consolidation](../benchmarks/results/2026-10-29-prepared-scope-facts/README.md)
at cc988b9d removes repeated lexical analysis. Five representative sources allocate
1.7–3.7% less during compilation; the fixed timing comparison is mixed and noisy.
It establishes no overall elapsed speedup and does not update the primary table.

The [optional-adapter refresh diagnostic](../benchmarks/results/2026-10-29-regex-adapter04/README.md)
compares PythonRe 0.2/0.4 on identical core runtime sources. Finditer improves in
one short observation; pattern reuse remains inconclusive. Both rows retain
their noise and CPython context, with no qualified overall multiplier.

The [original-subject window diagnostic](../benchmarks/results/2026-10-30-regex-windows/README.md)
compares 10d77eda and 2796d2e2. Removing copied windows cuts ASCII allocation,
but Unicode allocation increases while the 0.4 adapter prepares more original
context with dense coordinate maps. It establishes no elapsed speedup or current
CPython ratio; the correctness fix and remaining adapter cost are both visible.

The [native format-field diagnostic](../benchmarks/results/2026-10-30-native-format-fields/README.md)
records lower allocation for nested specifications and a small plain-field increase
from metadata admission. Private expanded specs stop accumulating batch charges.
These are allocation/accounting observations, without an elapsed or CPython claim.

The [owned binary-file diagnostic](../benchmarks/results/2026-10-30-owned-binary-files/README.md)
records lower allocation after removing read-window and publication copies. It
measures synchronous native file handles and cleanup, excluding frontend, VM
dispatch and disk I/O; it supplies no elapsed or CPython ratio.

The [owned text-file diagnostic](../benchmarks/results/2026-10-30-owned-text-files/README.md)
records lower text allocation after removing transport copies, alongside small
binary increases from the shared transport. Window decoding retains a UTF-16
bridge. These are native allocation observations, without elapsed/CPython claims.

The [owned whole-file input diagnostic](../benchmarks/results/2026-10-30-owned-whole-file-input/README.md)
records higher allocation than the older host-returned-buffer path, particularly
for borrowed text input. Shared whole-buffer decoding and EOF probing remove
avoidable prototype work. This acquisition/ownership cost is visible; no elapsed
speedup, complete Path.read_bytes comparison or new CPython ratio is claimed.
