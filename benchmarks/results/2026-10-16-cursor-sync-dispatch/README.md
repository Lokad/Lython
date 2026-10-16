# Synchronous cursor dispatch under RunAsync — 2026-10-16 UTC

Ordinary builtin cursor pulls reuse TryMoveNextCore without entering the cursor's
async state machine. User/async iterator ValueTasks are forwarded directly. Only
async-enumerator result conversion retains an awaiting helper. Duplicate sync
iterator/fallback arms and redundant forwarding awaits are removed. Cursor
creation, VM ForNext, stack policy, owners and cleanup keep their existing routes.

| Engine | Measured revision | Warm branch-loop median | IQR / median |
| --- | --- | ---: | ---: |
| Lython before | 86ef68e9a1eae419112fcddb4059d9aa63c084b7 | 5.979 ms | 4.74% |
| Lython after | 2e4d0a3462a6b0d580e141739ca7158f79691f8c | 5.889 ms | 0.70% |
| CPython | 3.13.16 | 1.0203 ms | 0.37% |

The 1.5% lower Lython median is within baseline spread: inconclusive, no speedup
or qualified ratio established. No allocation, embedding-latency or later HEAD
claim. This measures the cursor change; both roles include the preceding Counter
admission correction. Historical complete file jobs remain separate.

Unchanged loops.branch.large, 16,384 iterations / 119 UTF-8 source bytes. The
script adds multiples of three and subtracts other values, exercising range
iteration, modulo, comparison and integer arithmetic. Source SHA256:
f561f11f62301d183ac0e240174d882de9b34ad052d97b922f3711d050e6cf4e.
Independent arithmetic-series goldens pass at 256, 2,048 and 16,384 iterations
for both roles before timing. No files, new producers or artificial I/O delay.

Boundary: warm public RunAsync, reused compilation and fresh invocation state;
execution/projection and prior-result cleanup timed. Compilation, startup,
transport and full response hashing excluded. Lython public limits, cancellation
and cooperative admission enabled, optional instruction fuel unset. CPython has
no equivalent in-process containment. Genuine suspension is tested separately.

Machine: dedicated Azure Standard_F4as_v7, Ubuntu 24.04.4 LTS x86_64, four AMD
EPYC 9V45 vCPUs, 16 GiB RAM, no swap; .NET SDK 10.0.401/runtime 10.0.12.
CPython 3.13.16, GCC 13.3 PGO/LTO, ordinary GIL/GC, -I -S. Workers retain normal
GC/tiering; only supervisor tiering disabled. Seven rounds span 7.257 s under a
28 s owned process-group cap. Both 524-production-source manifests and archives
match; backend/tooling identical. Seven workers/fixtures gone, services inactive,
VM lease free. One capture, no retry, retuning or full lane.

Validation: 52 focused white-box, 195 focused public, 2,887 ordinary white-box,
59 original stack/trace/copy and 93 representative CSV/text/JSON/file retention/
cancellation checks pass. Sixteen pre-edit cursor fuel/output traces and 26 saved
call/JSON traces match exactly. New controls cover lengths 0/63/64/65, both
directions and execution paths, pre-pull cancellation/fuel order, dual-interface
precedence, actual async suspension and acquired-enumerator disposal once.
Existing protocol/host and cleanup controls remain. Original fixtures, timeouts
and guard limits are unchanged. Full platform/package qualification is recorded
at the delivery revision, separately from this diagnostic source pair.

Internal ValueTask failures may now throw at invocation rather than produce a
faulted ValueTask; actual cursor callers await within existing exception/cleanup
scopes. No public async contract changes. No claim that all asynchronous hosting
overhead has been removed.

[Samples](warm-async.json), [declaration](comparison-declaration.json),
[catalog](catalog-micro.json), independent goldens
([before](baseline-correctness.json), [after](candidate-correctness.json)),
[pre-capture gate](before-proof.json), [cleanup/source proof](cleanup-source-proof.json),
[frozen cursor baseline](baseline-proof.json), [focused comparison](focused-proof.json)
and [local validation / exact traces](local-proof.json). Frozen archives and
original process/test receipts remain in private notes.
