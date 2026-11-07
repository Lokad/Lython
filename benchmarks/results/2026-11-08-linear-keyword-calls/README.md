# Ordinary executable call activation — R71 / B424–B425

2026-11-08. Retained design on source 7a81a629, with explicit ordinary-CSV
regression debt. Final measured cores 40a4959e (.NET 11) / a00c2ce7 (.NET 10).
Exact source inventories, binaries, patches and original adverse results remain.
No public API or intended Python compatibility change. Full delivery of this new
source is pending; the R70 baseline is qualified separately by CI42422556519.

## Work removed and preserved

Nonmirrored executable keyword/variadic calls now use the positional call runner,
the existing final workspace and shared binder. They no longer recursively enter
the generic InvokeFromOperands / InvokeBound / ExecuteBody driver chain. This also
permits deferred lexical contexts independently of signature. Mirrored functions,
lowered functions and genuine callbacks retain their real generic entry points.
Redundant local-storage state, an impossible positional selector and generic
operand native-borrow attempts/flags with no production eligible caller disappear.
Native opcode borrows and callback native execution after binding remain.

Completed frame transitions and synchronous binding return directly. Only actual
pending default callbacks retain a continuation. Caller operands, binding storage
and call admission stay owned through suspension/body/awaited cleanup and transfer
exactly once. Live defaults, receiver evaluation, closures, varargs/kwargs, errors,
managed stack admission, current cancellation and terminal refusal remain.
No second binder, arena, persistent binding fields, policy or execution engine.

## Correctness and source variants

- Twenty public default checks pass on the baseline first.
- Initial prototype: 1,366 internal / 3,118 public per actual .NET 11/10.
- Intermediate direct-context variant: the same counts per target, archived
  separately; these runs do not qualify the final synchronous-binding cleanup.
- Final measured source: 1,366 internal / 239 targeted public per actual .NET 11/10,
  with matching probes. The public set covers function/default/bound-method/native
  return/deferred/async/lifetime/argument-order/expanded-call/file-worker/namespace/
  definition/generator/recursive-exception/depth behavior.

New default-callback regression really suspends, replaces live default metadata,
withdraws the global input alias, refuses substantial allocation, attempts guest
suppression/finally return, and then reuses the compiled script/shared budget.
It verifies retained arguments, first terminal origin, stopped output, awaited I/O
and balance return. Existing actual cancellation/default mutation and 301-frame
keyword recursion coverage remains. No timeout or containment assertion relaxed.

## Allocation and original experiment

Existing five-script driver, normal GC/tiering, independent before/current/control
assemblies sharing CoreLib/GC/hashing, both public APIs. All goldens, shared-budget
balances, image isolation and fixture cleanup pass. Final returned-record CSV
allocation falls **13.51–13.53% sync / 12.62–12.64% async**; ordinary CSV/text/JSON/
report allocation is essentially unchanged. This establishes allocation reduction,
not a CPU improvement.

Initial B424, 114.295 seconds: affected sync elapsed +0.43–2.76%, async
−0.33–4.51%; ordinary CSV −2.58 to +1.93%, text −3.04 to +3.96%, with baseline
spreads 3.86%/4.63%. Initial desktop CSV +8.47–9.98% remains adverse. Its newly
added completed-value packaging justified one coherent refinement; this does not
establish that packaging caused these timings. Initial sources/raw/cores remain
beside the refined observations.

Final desktop clocks remain diagnostic: affected sync +1.80/+15.99% with 12.23%
old-role drift, ordinary sync CSV −13.26/−8.57% (opposite the initial desktop and
VM direction), async affected about −0.54%, adverse async report +11.81%. No
discarded observation, desktop CPU claim or causal explanation.

## Controlled refined comparison — B425

Quiet Azure F4as_v7, Ubuntu 24.04.4, .NET 11 RC3. Two independent baseline/two
current processes, five-second warmup per role/job, seven rotated 150 ms rounds,
normal GC/tiering. Baseline d6445fc5 matches fully qualified R70/7a81a629;
desktop baseline 063faf7f has identical production source and distinct metadata.
Unchanged 7cf37ca5 manifest worker and normal optional 0.4 adapter. Four jobs
complete in **114.478 seconds**. Negative elapsed means faster.

| Complete job | Elapsed change, all process pairs | Baseline / current spread |
| --- | ---: | ---: |
| csv-validated-records.large / warm | -0.42 to +0.39% | 0.39% / 0.43% |
| csv-validated-records.large / warm-async | -4.37 to -1.57% | 1.33% / 1.54% |
| csv-summary-functions.large / warm | +0.07 to +5.09% | 2.49% / 2.34% |
| text-summary-helpers.large / warm | -4.02 to +0.07% | 2.62% / 1.57% |

Returned-record sync is flat; async has a modest elapsed benefit. Ordinary CSV
has **+0.07–5.09% elapsed and +6.25–11.70% coarse process CPU**. CPU includes
outside-job work and 10 ms quantization; it still cannot be dismissed as a proven
improvement. Text is mixed. No general CPU gain, CPython multiplier or repayment
of earlier regression debt is claimed.

**Disposition:** retain the coherent execution-route deletion and independently
measured allocation reduction, with ordinary CSV regression debt explicitly open.
Before another runtime optimization, inspect the changed completed-call transport
with a bounded source/generated-code assessment. Correct concrete added work if
found; otherwise keep the cause uncertain and choose a focused attribution or
explicit restoration/debt decision. Do not repeatedly retime unchanged code or
hide the adverse result beneath an unrelated improvement. This experiment does
not complete the whole-script performance objective.

Warm includes public invocation, mediated RAM I/O, full stdout/file validation
and prior-result cleanup; compilation/startup/staging/reset/protocol/final cached
teardown excluded. Async timing uses completed host I/O; genuine suspension is
separately tested. Desktop includes reset/result disposal and absolute clocks are
not comparable. No RSS, startup, pending-I/O or embedding-tail claim.

Every source/actualReady/auxiliary/runtime/golden/quiet/Closed/exit/join/empty-fixture
gate passes. Persistent evidence is separate from owned 128 MiB RAM fixtures;
all users join and the mount is removed before archive verification under lease
and independently locally. Staging 294,662,144 B passes the 256 MiB floor without
reclaim; 150 s owner + 30 s stop + 30 s margin. No timeout restart. B424/B425 are
terminal/archived/verified and must not be queried again. Publication postponed.

[Refined assessment](assessment.json), [initial assessment](initial-assessment.json),
[allocation/adverse desktop](local-assessment.json), [initial desktop](initial-local-assessment.json),
[final focused checks](binding-focused.json), [intermediate checks](focused.json),
[identities](identity.json), [VM identities](vm-identity.json), [contract](contract.md),
[refined VM archive](observations.tgz), [initial VM archive](initial-observations.tgz),
[local source/cores/tests/raw evidence](local-observations.tar.xz).
