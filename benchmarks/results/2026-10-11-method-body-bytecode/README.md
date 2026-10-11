# Explicit function-body bytecode: rejected short experiment

The isolated prototype **is rejected**. Method time changes **-2.12% / +5.99%**,
with retained spreads **1.53% / 1.37%**. Its first improvement exceeds the declared
threshold, but its second pass slows down. Acceptance requires method gains beyond
max(2%, retained spread) in both passes, no repeated material control regression
and no repeated allocation growth above 0.5%. There is no repeated control or
allocation regression under those rules. Every control remains visible, and the
failed target rule remains decisive; no observation is discarded or recollected.

| Complete case | First pass change | Second pass change |
|---|---:|---:|
| calls.keyword.medium | +2.57% | -1.37% |
| calls.method.medium | -2.12% | +5.99% |
| calls.positional.medium | -1.98% | +0.60% |
| control.empty.control | -1.20% | -3.85% |
| lists.stable-sort.medium | -3.23% | -15.04% |
| loops.integer.large | -3.14% | +4.48% |
| strings.pipeline-ascii.medium | -1.25% | -4.15% |

Fourteen paired microbenchmarks take **8.038–8.085 seconds including cleanup**.
Separate ordinary allocation and native groups take **4.032–4.040 seconds**.
Every diagnostic process group has a **30-second hard cap**. Actual workers retain
ordinary tiering/PGO/GC; only the controller disables tiering. These short
diagnostics do not establish confidence intervals or milestone qualification.
No ten-minute timing lane ran.

## Compiler and correctness

The prototype compiles actual PyFunction bodies into immutable instructions and
executes them in a flat loop. Common operands, assignments, returns, short circuits,
branches and for/while control use bounded value-type operand and iterator fields.
Deeper shapes and complex operations use the existing handlers; whole calls retain
their original admission and argument expansion. This differs from the previously
rejected recursive typed body plans. The actual contexts, namespace/class cells,
receiver, argument leases, source checkpoints, compound depth, logical fees and
genuine awaited effects remain in use. The weak cache contains source artifacts.

Both clean producers pass **9,526 Windows Debug and 9,526 Linux Release tests**
each (1,657 white-box + 7,869 public), with matching Probe builds and **seven
complete canonical CPython correctness comparisons each**, before timing.
The identical test tree adds **11 independent cases** this round: nested
control/fallback cleanup, deep operands, actual locals/globals and fixed original
fuel cutoffs, spans, effects, funding and real host suspension/reentry/cancellation.
The focused boundaries total 82 cases, including existing coverage. Seven pure
control-flow goldens also match local CPython.

An additional lifetime diagnostic passes separately on both producers after the
full Windows gates: a discarded call result is collectable while a later real
host call is suspended. It contradicts a suspected retention defect. This extra
case is delivered as regression coverage and is **not** counted in the pre-timing
9,526 gates or their producer test tree. No source correction or recollection
followed that diagnostic. Initial fixture mistakes and an existing unsupported
call-result assignment form are retained privately with their corrections.

First artifact construction, cold startup and retained compilation costs remain
real and **unqualified**. Warm diagnostics exclude those costs. Because this
candidate fails its warm target gate, it is not integrated; any future compiler
acceptance still needs that qualification and the broader correctness inventory.

## Allocation, native code and cleanup

Complete method-job managed allocation saves **3.260%**
in the first pass and grows **0.00075%** in the second.
These are process-wide managed totals, not exact guest-type bytes or logical fee
changes. Allocation does not override the timing decision.

Native inspection retains all **230 baseline and 276 candidate listings**,
including **52 candidate bytecode listings**, with every emitted tier, inline
summary and call target. Code presence or size does not establish a CPU gain.
The [complete evidence](evidence.json) retains every timing/allocation row and
native inventory. Raw artifacts, receipts, journals and producer/helper hashes
remain archived privately. The frozen allocation helper is reused without rebuild.

All **20 owned services are terminal**, all **66 recorded PIDs are absent**, both
sources are clean, inputs rehash and the shared lease is available. The rejected
source stays isolated at `5f0cb1b09a2500dbf7f108aa865aac5e94b3eacc`; delivered production stays
`f813ff5487676229ee6f888846196dbef850bf75`. Broader compiler/design work, the original
pipeline cause, C05, external regex integration and milestone qualification
remain open. This experiment is not repeated to obtain a favorable result.
