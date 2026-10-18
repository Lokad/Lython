# Ordinary binary file-write transport, 2026-10-18 UTC

Ordinary one-positional binary writes now share the existing native text-write
operand path. Eligible calls avoid bound-member/signature/delegate construction,
argument binding arrays and generic async callable/member forwarding. The existing
marker and call helper are renamed and shared; no method, marker, dispatcher,
cache, owner or mode is added.

Baseline: `92f98863e35892181b113ec056ad6e19d1f6b450`. Candidate: that revision
plus the frozen [five-file patch](candidate.patch), identified by source hashes
in [validation](validation.json). This is not a measurement of a later HEAD.

| 128 writes / allocated bytes | Baseline | Candidate |
| --- | ---: | ---: |
| Ordinary binary, sync interpreter | 21,672 | 17,208 |
| Ordinary binary, async interpreter | 64,312 | 17,208 |
| Exposed binary method, sync control | 21,648 | 21,648 |
| Exposed binary method, async control | 21,648 | 21,648 |
| Ordinary text, sync control | 11,088 | 11,088 |
| Ordinary text, async control | 11,088 | 11,088 |
| Exposed text method, sync control | 15,408 | 15,408 |
| Exposed text method, async control | 15,408 | 15,408 |

Medians of eight samples after eight warmups; every sample is retained
[before](allocation-before.json) and [after](allocation-after.json). Allocation
savings do not establish elapsed speedup, embedding latency or a CPython ratio.
The leading comparison in [BENCHMARKS](../../../BENCHMARKS.md) remains separate.

The [temporary fixture](allocation-diagnostic.cs) compiles one loop outside the
counter. Each invocation prepares a fresh governed context, native handle and
funded input before measuring execution. Binary calls reuse one 32-byte value
128 times, producing 4,096 output bytes. Text controls reuse `alpha`, producing
640 bytes. The initiating-thread counter includes the executable loop, alias
lookup where applicable, calls and buffering. Close, mediated publication and
exact complete-output checks are outside; owners end only after cleanup. No
actual I/O suspends. Public scheduling, compilation/startup and CPython timing
are excluded. Optional fuel is unset, so diagnostic step counts are zero; the
separate guard matrices validate every checkpoint. No GC was requested. The
fixture was removed before final matching Release builds.

Machine: Windows build 26200, x64, Intel Core i7-14700KF, 20 cores / 28 logical
processors. [Toolchain](dotnet-info.txt), environment, source/assembly hashes and
bounded process receipts are recorded in validation. No VM timing, trace, long
lane, new framework or qualification campaign ran for this change.

The patch only widens existing binary Write visibility, shares the native
receiver selection/marker/helper and removes text-only casts. Binary calls retain
the dispatcher's third check before the unchanged write body; text keeps its two
checks. Storage, 4,096-byte chunk checks/preflights, append-before-position order,
flush/close, ownership and awaited host publication are otherwise byte-identical.
General/exposed/keyword/wrong-arity/user calls keep their binder. Supported value
types and closed/read-only/type error order stay unchanged. See
[MEMORY](../../../MEMORY.md).

[Baseline](guard-baseline.trx) and [final](guard-candidate.trx) preserve 28 exact
ordinary/exposed sync/async binary records and two preceding text records. Binary
cases cover small/empty/4,097-byte writes, invalid input, closed/read-only errors
and receiver rebinding. Every fuel cutoff records span, argument effects and
published prefix length/digest. The fixture independently checks actual bytes;
compact SHA-256 constants freeze the complete records without repeating wide
output. Its injected fuel is reset only for native fixture cleanup after guest
work ends, before owners are disposed.

Four additional refusal cases preserve funded input and previously published
bytes; releasing padding cannot revive publication after terminal refusal. Four
suspended-argument cases preserve the evaluated receiver, rebinding, completion/
cancellation and awaited cleanup. Existing checks retain delayed binary host
publication, lifecycle/accounting, shared allowances/results/caches, managed
stack protection and 58 complete CSV/text/JSON/file jobs.

All 463 distinct focused Release cases pass. Validation records three fixture
preparation corrections separately; no runtime defect was hidden, existing
assertion/timeout relaxed or Stress scale changed. Full local Stress was not
repeated; platform and explicit Release package consumers are batched at delivery.
Whole-job elapsed effects of this patch remain unmeasured.
