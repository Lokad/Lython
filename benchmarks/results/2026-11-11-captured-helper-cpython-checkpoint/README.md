# Six-script CPython checkpoint — 2026-11-11

Retained `6904093e` production source, frozen refined core `ccdd645d`, versus stock
CPython 3.13.16. All production hashes match; the binary carries recorded precommit
metadata. This refresh measures the current state after several delivered changes,
not the contribution of one optimization. Exact-source full Windows/Ubuntu,
actual .NET 11/10, original stress and installed Release consumers are tracked
separately by delivery run 43058223560; focused checks alone do not establish it.

| Complete script | Warm Lython / CPython | Compile-to-result Lython / CPython | Uncertainty |
| --- | ---: | ---: | --- |
| Captured text helper/counter | 1.96–1.97× | 1.70–1.75× | Warm max IQR 2.95%; substantial gap remains |
| Ordinary text helpers | 1.16–1.19× | 1.18–1.19× | Warm Lython control 1.43% |
| CSV validation/helper calls | 1.06–1.09× | 1.05–1.08× | Warm Lython control 1.78% |
| JSON-lines/helpers | 0.84–0.85× | 0.87–0.89× | Complete Lython control 2.07% |
| JSON-to-Markdown report | 1.25–1.38× | 1.25–1.32× | Warm controls L6.41%/P3.39%, max IQR5.11%; adverse ratio versus earlier checkpoint unresolved |
| Keyword-default file search | 1.54–1.59× | 1.44–1.48× | Warm Lython control3.43%, max IQR3.28% |

Ratio is Lython elapsed divided by CPython elapsed; above one means slower.
Ranges span all four pairings of two independent process medians per engine,
not confidence intervals. Compilation is measured separately; it does not explain
the captured-text warm gap. Neither allocation reductions nor cross-checkpoint
ratio changes establish CPU attribution or unchanged-workload neutrality.

Report ratio adversity needs care: both engines' absolute elapsed times are lower
than in the earlier report checkpoint, with CPython improving more. Current warm
report controls also move substantially. This observation does not establish a
Lython source regression or explain it; all earlier and current results remain.
The source-pair CSV/text control debt is not declared resolved by these external
ratios. Config, literal-prefix search, CSV summary, manifest and patch retain
their explicit earlier-source measurements in the current guide.

## Collection and boundary

B584 completes 18 jobs in **487.016 seconds**, reusing the existing protocol and
fixed `fec79` worker with optional adapter 0.4. Six scripts use their unchanged
source, fixture and full stdout/file goldens from the earlier checkpoints. Each
has compilation, warm execution and compilation-plus-one-execution lanes. Four
independent workers per job use five-second warmups and seven rotated 150 ms
rounds, with normal GC/tiering on both engines.

Dedicated Azure F4as_v7, Ubuntu 24.04.4, four AMD EPYC 9V45 vCPUs, 16 GiB/no swap;
actual .NET 11 RC3 and isolated CPython 3.13.16. Warm and compile-to-result include
mediated RAM-file I/O, full validation and prior-result cleanup. Compilation
includes code construction/disposal under the shared allowance. Startup, fixture
staging/reset, protocol and final cache teardown are outside the internal clocks.
Phase medians need not add up. External CPU includes protocol/reset.

Lython uses efficient mediated file operations with capability/path checks;
CPython uses builtin open and retains imports while Lython isolates execution
imports. This is an embedding comparison, not an isolated CPU kernel. Startup,
RSS, pending-I/O performance, embedding tail latency and large-file throughput
remain unqualified.

All loaded hashes, outputs, ownership, machine quiet gates, joined users, RAM
cleanup and archive verification pass. B584 is terminal/cached, with no remaining
VM workers or fixtures. The persistent archive was verified under its owned
lease and after retrieval. Only duplicate historical B581/B583 archives were
retired after fresh absolute path/UID/inode/all-alias proofs against exact
committed canonical Git bytes; all original evidence and selected core roles
remain available.

## Evidence

[Summary](summary.json), [raw observations](observations.tgz),
[loaded identity](identity.json), [source hashes](source-identity.json),
[archive receipt](archive-receipt.json), [archive hashes](archive-identity.json),
[preparation](preparation.tar.xz) and the [prior guide](prior-BENCHMARKS.md)
preserve every observation, control and replaced source label. The current
[performance guide](../../../docs/BENCHMARKS.md) presents all three timing phases.

Preparation first requested a nonexistent keyword-search catalog ID; the gate
refused before VM work. The unchanged keyword-default case was then imported
from its existing catalog under its real ID, with original hashes/goldens. No
runtime, fixture, golden or timing assertion was weakened.

Next work selects a complete remaining phase from current cost evidence; this
checkpoint does not complete the performance objective. Regex development stays
independent and publication remains postponed.
