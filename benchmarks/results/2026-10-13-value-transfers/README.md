# Stored execution roots and value transfers

Runtime producer **28b8a95e**, production tree
**b7b526e54502dd89cecf6b2735aa90a2438d6008**, combines frame/root descriptors
(`e5c4fcf1`), lowered/code capture descriptors (`a1319514`) and executable
value transfers (`28b8a95e`). Complete native working state, partial construction,
conditional identity retention and workspace capacity remain open. **No logical
reclamation or forced-GC policy change is enabled.**

Arithmetic, unary, subscript/slice and iterator acquisition previously removed
their operands before entering native/protocol callbacks. The exact existing
slots now stay published through callbacks and genuine async suspension.
Completed values replace the consumed tail before observation; failure and
cancellation remove only that operation's inputs. Calls share the same completion
helper, and slice-bound reading is consolidated. No activation array, registry,
retained extent, per-instruction cleanup hook or protection switch was added.

The preceding descriptors read existing frames, hidden locals/cells, exception/
finally state, caches and prepared code children through the same funded walk.
Zero-local synchronous frames now publish through the existing context field.
Unknown shapes, opaque collections and conditional id() retention stop completion
without invoking guest properties or protocols. The walk remains disconnected
from governor relief.

## Validation

All ten original value-transfer regressions fail on the preceding production
tree because operands are absent during callbacks. The accepted implementation
passes **119 focused internal checks**, including 20 new value-transfer cases,
in **1.219 seconds** including cleanup. **310 public checks** pass in **6.286 seconds**
before the final shared slice-decoder consolidation. The full checkpoint covers
that final source; its counts, times and explicit Release package identity are
recorded in [qualification.json](qualification.json).

| Check at 28b8a95e | Result | Elapsed including cleanup |
| --- | --- | ---: |
| Full ordinary Release suite | 10,990 pass | 290.474 s |
| Original stress scales | 375 pass | 186.461 s |
| Explicit Release pack and isolated compatibility/file consumer | pass | 17.519 s |

Cross-platform CI is a separate delivery gate at the documentation descendant.
This table retains the measured producer. Suite durations are observations on
the development workstation, not runtime speed or embedding-latency evidence.

Focused cases cover physical live/tail slots, a 16,000-byte payload retained
solely through the operation input, sync/yielded async execution, result
publication before admission, denied results, failure/cancellation and frame
restoration. Public controls cover original roots, native CSV streaming/retention,
operators, indexing, slices and generator composition. Original stress scales
remain separate; they are not replaced by the focused group.

## Short cost observation

One existing-tool diagnostic on the shared Windows x64 development workstation
(i7-14700KF, 28 logical processors, 64 GiB; .NET 10.0.12, CPython 3.13.2) takes
**7.323 seconds**. It measures warm synchronous invocations of the existing
16,384-item keyword-call workload, with fresh state, projection/captured output,
and exact independent output checks. Compilation/startup are outside timing.

Baseline and candidate medians are 4.181 and 3.722 ms/job, with IQR/median
12.85% and 10.83%; CPython context is 1.290 ms/job. This noisy diagnostic establishes
neither a qualified improvement nor regression. It preceded the final slice-
decoder consolidation, is not a final-HEAD or embedding-latency measurement,
and was not repeated. Raw original attempts and loaded identities remain in
[micro.json](micro.json). CPython performance and milestone qualification remain
separate in [BENCHMARKS.md](../../../BENCHMARKS.md).

Private source/build/test receipts and setup failures remain under
`.git/agent-notes/value-transfer-20261013/`. These results do not prove complete
ownership or authorize removing CLR-dependent reclamation.
