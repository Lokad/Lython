# Canonical executable activation — active prototype

2026-11-15. Uncommitted **77a63bea** replaces the native helper instruction loop,
fixed-buffer eligibility, recursive child transfer and ordinary call-state
duplication with one activation and linear runner. The activation owns final
argument/local/operand slots independently of its optional lexical context.
The old array-only single-idle cache becomes exclusive activation/slot loans;
idle capacity stays charged and returned loans clear execution/guest references.
This prototype is **not retained or fully qualified**. Publication stays postponed.

| Complete workload | Final allocation change | Quiet-VM elapsed change | Wider process CPU change |
| --- | ---: | ---: | ---: |
| Invoice | +0.026% | +28.17..33.33% | +29.32..34.11% |
| Fulfillment | +0.010% | +15.40..16.96% | +16.67..16.84% |
| Captured text | +0.145% | +46.68..50.34% | +45.65..47.21% |
| JSON summary | +0.027% | +13.85..14.66% | +14.50..17.35% |
| Protected file search | not measured | +43.91..55.06% | +34.63..42.47% |
| Configuration, compile-to-result | not measured | +0.75..2.14% | −8.23..+3.63% |

These are clearly adverse whole-script results, not evidence of improvement.
B787 takes 170.682 s on the established quiet F4as_v7 Ubuntu 24.04.4/.NET 11 RC3
VM: two processes/source, three-second full-request warmup (eight for text), five
rotated approximately 0.5 s batches/process. CSV/text/JSON reference elapsed
spread is 0.27–1.69%; search reference/candidate spread is 3.63/3.98%.
Ranges retain every pairing of the two process medians per source.

Warm elapsed includes synchronous Run, mediated RAM-file I/O, complete goldens
and prior-result cleanup. It excludes compilation, startup, fixture reset,
protocol and final cache teardown. Configuration adds compilation and compiled
owner disposal; its small fixture is the existing empty configuration case.
Wider process CPU also includes reset/protocol. VM reference is fully qualified
**51e3c2c1 /22e5b72c**; allocation separately compares qualified **6e276759 /45dd2da1**
on the Windows development box with the unchanged complete-job driver, including
reset/I/O/goldens/result disposal. There is no new CPython comparison, heap/RSS,
pending-I/O latency or embedding-tail claim.

All intermediate allocation results stay preserved. Initial unpooled activations
show invoice +112.44%, fulfillment +52.59%, text +214.27%, JSON +63.24%.
Replacing array-only reuse reduces those costs but leaves CSV +9.40/+23.71%.
Restoring typed integer builtin returns in the shared path leaves invoice +3.15%
and fulfillment effectively flat. Selecting the callable before observing an
unneeded argument brings all four final workloads effectively to baseline.
These changed-source observations do not assign exact causal CPU shares.

Focused final-source Release checks pass: 169 internal/478 public actual .NET 11,
56 internal/93 public actual .NET 10, plus 106 protocol/lifetime cases on .NET 11.
Broader intermediate checks also cover actual .NET 10. Original correctness and
setup failures remain, including the .NET 11 probe initially resolving system
dotnet without the pinned preview runtime; corrected PATH/DOTNET_ROOT and a
matching probe build pass. Logical/managed stack admission, live-default lookup,
exception provenance, direct tuple returns, cancellation, first refused admission,
funded retained aliases, suspended users and joined cleanup remain required.

One source-backed refinement is selected before deciding the coherent redesign:
ordinary dead operands can remain in funded private storage until its owner
relinquishes it, removing repeated Array.Clear calls. Private cursors/builders
still retire immediately; returned loans clear used capacity before reuse.
Conservative reference retention is the explicit tradeoff. This is a hypothesis,
not an explanation of the measured percentages. The first 190 internal checks
pass for that subsequent, unmeasured refinement; its source and comparison will
be recorded separately. Do not retain the measured regression as a speedup,
add another execution controller or discard the adverse samples.

All VM goldens, balances, quiet/Closed/exit/join gates pass; observations are
archived and verified under the lease and RAM fixtures removed. Two initial
staging preflights fail before mutation (self-matching process check, an already
archived/removed RAM trace); corrected staging retires only an exact old archived
binary and a duplicate working archive after committed custody/hash/link/path
proof. Current qualified images, canonical evidence and disk floors stay.
The terminal VM result is cached; do not query it again.

[Every median, source identity and decision state](decision.json),
[exact sources, reconstructed images, checks, adverse results and procedures](sources-images-checks-and-observations.tar.xz).
