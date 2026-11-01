# Execution-owned call arena: rejected (B69)

2026-11-01, production baseline `9003d045`, source base `fb5ea544`.
A single 35.854-second comparison rejects the prototype. Complete CSV and text
jobs are consistently slower. The JSON control is inconclusive. **No runtime
change or speedup is retained.** This is an internal design experiment, not an
updated CPython comparison or a measurement of future HEAD.

## Hypothesis and actual scope

Reuse one admitted value arena and saved-activation array in the existing
executable engine. Ordinary eligible positional helper calls stop allocating a
child interpreter, operand array and bound-local array. Existing binding already
targets final locals; there was no second argument copy to eliminate. Keep real
lexical contexts, synchronous/asynchronous opcode loops, simple-return paths and
separate root/generator/reentrant-protocol drivers. Keyword/default/protocol calls
retain the existing binder. No pool, second backend, unsafe code or new policy.

Local windows hold owner/offset pairs, remaining valid across backing-array
growth. The existing frame view is created on semantic inspection. Full arena and
activation capacity, construction overlap and genuine cell/cache/region arrays are
admitted before allocation. Returned calls clear relinquished references; reusable
capacity stays funded until the driver or generator actually closes. Borrowed
native argument storage remains funded through awaited completion. Escaping cells
and snapshots retain their independent existing ownership.

The prototype removes actual call allocations, unlike the earlier control-record
trial that retained them. Nevertheless, allocation removal does not establish a
latency gain. It also adds slot-window access and active-state save/restore work;
these results do not individually attribute the regression to either mechanism.

## Semantic checks

Focused Release checks pass on actual .NET 11 RC3 and .NET 10.0.8:
**1,131 native and 3,821 public cases per runtime**. They cover functions,
closures, generators, argument/default behavior, classes, admission/refusal,
cancellation and real asynchronous users. The first native run has 32 failures in
six methods across three test classes: their reflection assumes the old frame and
array representation. Inspector adaptations preserve the independent assertions;
all adaptations are withdrawn with the rejected prototype.

The independent `NestedFrameLifetimeTests` remains: an observed parent and its
shared cell stay correct while a wide nested call grows working storage, actually
suspends, then succeeds or is cancelled. An escaping closure reads the final cell.
Funding remains live until the real user finishes; owner disposal returns the
shared allowance to zero. The same test also passes after restoring production.

## One frozen comparison

An owned quiet Azure VM, actual .NET 11 RC3, ordinary GC/tiering. Three isolated
Lython images share CoreLib and the same process, hash witness and auxiliary
binaries: original, candidate, identical original. The frozen complete CSV/text
helper scripts and JSON control preserve all source, inputs, stdout and files.
Compilation/startup are outside this warm boundary. Each job includes mediated
I/O, fixture reset, full verification, result disposal and ownership cleanup.

128 interleaved warmups per image; two seconds of settling per case; seven
rotating batches of 32 complete jobs per image. Completed cases are saved
incrementally. The service limit is 45 seconds. The frozen acceptance requires
at least 7.5% elapsed improvement for both affected jobs against both originals,
no more than 3% valid JSON regression/original drift, IQR at most 10%, and every
batch longer than 100 ms. No retry, threshold change or retiming.

| Complete job | Candidate change vs original | vs identical original | Decision |
| --- | ---: | ---: | --- |
| CSV helpers | +5.75% | +5.12% | Slower; valid affected controls |
| Text helpers | +5.03% | +6.77% | Slower; valid affected controls |
| JSON control | +3.99% observed | +1.81% observed | Inconclusive: identical-control IQR 80.95% |

Positive changes mean longer elapsed time. Affected-case IQRs are 1.29–1.81%,
original drift is 0.60% CSV and 1.63% text; all affected batches exceed 204 ms.
JSON's failed variability gate prevents a qualified control conclusion. Both
quiet gates, complete output/file goldens, zero final balances and owned-fixture
cleanup pass. The service exits zero; MainPID is zero, the control group is empty
and the VM lease is free. No worker or profiler remains active.

## Result and evidence

Remove the entire production prototype and all representation-specific test
adaptations. Keep the independent lifetime regression and restore the original
source with a fresh nonincremental build. No unchanged platform/package or
optional-adapter qualification is repeated. No further arena tuning is selected.

[Summary and gates](summary.json), [identity](candidate-identity.json), original
plans, private driver, candidate source archive and compressed patch preserve the
actual design. [Raw observations and terminal proof](raw-results.tar.gz), SHA256
`590746545c52b928e5a57b632758c34d9cb9dd88f786e2e0b95894f6905f3411`.
Quartiles use inclusive interpolation over the seven batch observations. Medians
are compared separately against both originals. Runtime documentation remains
about the retained implementation, not this rejected design.
