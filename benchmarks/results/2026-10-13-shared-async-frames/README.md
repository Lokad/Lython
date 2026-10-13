# Shared async module frames and completed local transfers

Runtime revision: `b43664e0f60aa78f3cbd638578e0ced28d926194`; production tree
`e4039283988ee6f9ba9aeb11dabef0758f81f741`. Before: immutable Release package
`8b160695178f76f3258b256914de291b0c09ba70`, production tree `953de207`.

Public `RunAsync` now uses the existing async executable-frame driver when
executable compilation succeeds, with lowered execution retained for supported
compiler fallbacks. Frame initialization is shared. The async driver loses its
unused synchronous branches and forwarding wrapper. Module variables remain
mirrored for async definitions, closures and fallbacks; host effects still await.

Compiled local stores consume the popped operand in a completed void helper.
This keeps the returned CLR reference out of the long-running dispatch loop.
The previous experiment prevented store inlining while still popping in the
caller; it did not establish this transfer lifetime. No collector, refund rule,
root registry, protection option or unsafe/native machinery is introduced.

The original three module-level async recovery failures now pass. The minimal
synchronous `del held` case initially remained a failure; the completed-transfer
helper fixes it. The 54 recovery, retained-alias, live-root and native-consumer
controls pass. Six function recovery/retention cases also pass, along with 236
workload/closure/consumption cases, 25 async/cancellation checks and four stack
checks. All **10,843 Release tests** and the exact Release package consumer pass.
The package SHA512 is recorded in [validation.json](validation.json).

## Short synchronous cost check

| 16,384-iteration integer reduction | Median per invocation | Within-run IQR / median |
| --- | ---: | ---: |
| Previous Lython 8b160695 | 1.152227 ms | 0.324% |
| Candidate Lython b43664e0 | 1.162002 ms | 0.875% |
| CPython 3.13.16 | 0.617104 ms | 0.171% |

One original diagnostic run on 2026-10-13 used the existing `--compare micro`
tool: seven rotated measurement rounds after warmup/calibration, separate
verification before and after, and ordinary limits/GC/tiering. The original
[receipt](micro.json), [catalog](catalog.json), [process receipt](process.receipt.json)
and [stdout](stdout.txt) preserve all attempts. There were no retries or exclusions.
The owned group finished in **8.242 seconds**, with zero paging/steal and four
idle observations at 0% busy. Its service is terminal and the exclusive lease is
released. The [runner](run-micro.py) and [input identities](manifest.json) preserve
the fixed invocation; large dependency bundles remain private.

Both workers use the identical benchmark observer and dependencies; only the
immutable Lython DLL differs. These are Windows Release builds using **SDK
10.0.300-preview.0.26177.108**, executed on the dedicated four-vCPU Ubuntu 24.04.4
VM under **.NET 10.0.12** from SDK 10.0.401. CPython is the existing GCC 13.3,
PGO/LTO, GIL reference with cyclic GC, launched with `-I -S`. Compilation and
worker startup are outside warm invocation timing; the public synchronous run,
fresh state, output capture/projection and cheap golden check are inside it.
Exact expected stdout is `134209536\n` for every completed invocation.

The observed Lython medians differ by about 0.85%; this short run establishes
neither a performance regression nor equivalence. **No multiplier or confidence
interval qualified.** It checks the synchronous transfer cost, not async
throughput, complete-script performance or embedding latency. CPython remains
the external baseline; see [BENCHMARKS.md](../../../BENCHMARKS.md).

## Remaining recovery and ownership work

The separate nested-tuple control hides the payload from the bounded admission
proof. After a caught denial, deleting that tuple still falsely rejects a fitting
replacement in both modes. Before and candidate each pass the two retained-alias
controls and fail the two released controls, using the identical diagnostic test
assembly. This is an existing allocation-only relief-eligibility problem, not a
regression introduced by the frame change. Original red fixtures, failed TRX,
source/binary identities and finite cleanup receipts remain in private
`.git/agent-notes/shared-async-frame-20261013/`; they are not shipped as expected
failures. The initial extraction build error and pre-transfer synchronous failure
also remain preserved.

General reference withdrawal and complete native helper/child/resource coverage
remain open. CLR-only await retention evidence still applies; sharing these
frames does not justify partial logical refunds or removing forced GC. Managed
stack admission, host mediation, cancellation, allocation preflights, retained
alias funding and bounded public projection retain their contracts. Async fuel
counts now follow compiled module operations where applicable; their internal
unit remains implementation-defined as documented in SPEC.md.

## CI cancellation-controller correction

Delivery run 38345505644 passed Ubuntu but failed one Windows assertion in
`CancellationDuringPrimitiveAggregationStopsTheActualRun`: the continuation of
its nominal 100-ms `Task.Delay` observed a completed `min(range(n))` run after
six seconds, before the test issued cancellation. The fixture depended on pool
scheduling for its cancellation clock while synchronous runs can occupy workers.
The corrected test uses a dedicated managed controller after the actual stdout
checkpoint, still verifies the aggregate was unfinished at cancellation, checks
the real cancelled result and joins both run and controller. Its 32 focused
aggregation/cancellation/worker controls pass. The original failed workflow is
preserved, not rerun or counted as green; a new descendant CI is required.
Production tree `e4039283`, measured DLLs and timing receipts are unchanged.
