# Native conditional helper returns — initial prototype, 2026-11-08

Historical initial core67e76e70, superseded by the [retained refinement](../2026-11-08-native-conditional-helpers-final/README.md). Original archive and measurements are unchanged.

One existing native-return proof was extended to acyclic branch returns.
Remove its single-block restriction and the assumption that the final stored return
is selected. Original source indices survive physical layout; type proofs precede
effects, and only selected Python operations run. Loops/shared joins/custom truth/
data-query format guards keep ordinary executable dispatch. Shared empty-list
construction preserves fresh values, admission, post-admission cancellation and
refunds. Old native expressions/local forwarding/cased kernels remain.

No second lowering/interpreter, control carrier, arena, runtime owner/cache/pool or
public option. Two32-bit immutable metadata fields/node and compiler offset/visited/
per-path scratch are explicit costs. Native combined nesting remains within its
existing proof bound; deeper syntax retains ordinary executable behavior.

Baseline fully qualified a7bd4103/coreaae80cc7 at [cached CI42488128858](../2026-11-08-native-json-construction/delivery-ci.json).
Candidatea7 plus archived five-file patch/core67e76e70, source hashes in identity.
Informational binary revision predates the candidate commit. This initial prototype was not separately fully qualified. Retained-source
platform/Stress/installed qualification is recorded with the refinement. The primary CPython
guide still names15c; no per-edit external remeasurement.

| Complete script / phase | Controlled elapsed, all four median pairings | Coarse CPU |
| --- | ---: | ---: |
| Guarded text helper, warm sync | −12.82 to −13.48% | −13.51% |
| Guarded text helper, warm async API | −13.61 to −15.93% | −13.24 to −14.71% |
| Ordinary rsplit helper, warm control | −0.65 to +1.52% | flat/slightly lower |
| CSV compilation control | −1.23 to +0.58% | mixed, near baseline |
| CSV warm control | **+2.71 to +3.73%** | **+4.65%** |
| JSON warm control | −1.36 to +0.64% | flat |

Guarded baseline spreads0.51%sync/2.09%async; candidate0.25/0.65%. Short controlled
observations, not confidence intervals or universal gains. CSV runtime regression
has stable baseline0.66%/candidate0.34%spread and remains **accepted debt with
unresolved cause**. Rsplit/JSON are nearbaseline/inconclusive, not causal gains.
Earlier R95/R96/R102/R105 debts are distinct and are not silently repaid.

Warm allocation is neutral(<0.02%diagnostic differences). Compiler allocation
+0.08to+0.53% across the five scripts, guarded+0.37%. Windows desktop elapsed varies:
async guarded old-control spread22.7%, fresh guarded−60% is diagnostic only.
Original Windows CSV compilation+74to+79% was not reproduced on quiet Ubuntu.
One bounded Windows confirmation changed image order: CSV−4.88to−5.98%, but guarded
+6.63to+9.62%, JSON+26.76to+31.89% and other controls vary. **Windows compilation
timing remains uncertain**, and the original adverse cause remains unexplained.
The controlled Linux result does not prove a Windows compiler speedup.

The existing driver rejected an initial two-case frontend catalog before collection.
Original source/catalog/stderr remain. Only compile/fresh were corrected to the
supported five-case catalog; the driver and warm observations were unchanged.
All original runs, adverse controls and confirmation conditions are retained.
Warm local elapsed measures Run only; allocation includes host fixture reset/
validation/cleanup. Compile includes construction/disposal; fresh includes compile,
execution, fixture reset/full validation/disposal. These local clocks differ from
the controlled command boundary and support allocation/diagnostic claims only.

205public/42internal focused Release peractual11/10 pass. Independent isolated
Python goldens cover true/false/nested/implicit returns, missing/unselected work,
changing builtins, formatting, fresh aliases, joins/loops and local forwarding.
Actual truth I/O/cancellation/terminal refusal waits for cleanup, returns the shared
allowance and permits another execution. Four call drivers preserve fresh funding,
post-admission cancellation/refunds, active exceptions and selected error spans.
The frozen-core source diagnostic verifies the maintained guarded helper changes
from ordinary instructions to the native proof. No architecture-only quota tests.

Guarded report:3072lines/35840B, read completely, count words and write full report.
Ordinary rsplit uses the same input; CSV4096records/57361B and JSON4096/110592B.
All unchanged catalog sources/fixtures/full stdout/file goldens are reused exactly.
Quiet Ubuntu24.04.4/AzureF4as_v7,4EPYC9V45vCPUs/16GiB/noswap, actual.NET11RC3,
worker7cf37ca5/optional0.4 unchanged, normalGC/tiering.5swarmup/process/job,
two copies/core,7rotated150msrounds, six jobs168.236s. Warm command-v2 includes
complete execution, mediated RAM I/O/full validation/prior-result cleanup;
compile includes construction/disposal, no guest execution. Compilation/startup/
staging/final cached teardown are outside warm clocks. Actual pending I/O is tested
separately, not timed here; no startup/RSS/embedding-tail claim.

All source/core/aux/Ready/golden/quiet/Closed/exit/join/fixture/archive/local gates
pass. B464 terminal/cached; all workers joined and owned RAM unmounted before archive.
Fresh exact-byte core hardlinks recovered27860992B while preserving every original
path/alias/content/archive; proof retained.240MiBOSfloor+7MiBevidence+actual payload
preflight; archivedaae reused, only67e uploaded. No discarded evidence.

[Controlled results](controlled-comparison.json), [allocation/local/confirmation](allocation-and-local.json),
[identity](identity.json), [status](status.json), [sources/cores/tests/goldens/failures/raw](observations.tar.xz).
