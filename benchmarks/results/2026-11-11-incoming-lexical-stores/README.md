# R221 — incoming lexical stores through existing owners

Retained runtime `4625d83e`, 2026-11-11; reference `0b8f9e9a` / runtime
`1208a681`. The measured prototype's 568 production-source hashes match the
committed source. [Identity](source-identity.json), [archive hashes](archive-identity.json).
Full Windows/Ubuntu, both-target, original stress and installed-package validation
is running against this exact source in [42942735331](https://github.com/Lokad/Lython/actions/runs/42942735331).
Focused checks are complete; this record does not yet claim full delivery.

Ordinary incoming nonlocal writes no longer materialize an invocation context,
search lexical namespaces or synchronize the same cell repeatedly. The existing
cell's defining owner updates its live local slot and persistent namespace once.
Operand cleanup precedes mutation. Ownerless and namespace-sensitive cases retain
their genuine lexical resolver. No new storage owner, call packet, execution route,
public option or memory policy is introduced. Reads already used deferred contexts.

## Correctness

Fresh nonincremental Release reference and candidate builds target actual .NET 11
and .NET 10 with zero warnings/errors. **672 internal / 2,587 public focused checks
pass per target**, covering executable functions, classes, scopes, closures and
comprehensions. [All original and final counts](focused-checks.json).
Eight independent CPython 3.13.2 goldens cover live owner reads and locals snapshots,
aliases, deletion/rebinding and detached owners, transitive shadowing, comprehension
walrus stores, class cells, class suites and default/exception effects. Existing
cancellation, terminal refusal, retained storage and cleanup tests remain enabled.

One original new golden exposed the unchanged frontend's LA3146 false positive
after `del value; write(7); print(value)`. Its failed TRX and original source/output
are preserved in preparation.tar.xz. The refined golden still checks nonlocal
rebinding and detached owners through an explicit reader/deleter. This is a separate
R222 correctness follow-up, not a removed Python behavior or a runtime failure.
[Original and refined goldens](semantic-golden-refinement.json).

## Complete-script observations

Three existing main-wrapped CSV/text/report workloads add an observable nonlocal
progress counter and final caller assertion. Inputs, stdout and complete output
files retain independently verified CPython goldens; source is identical across
the compared engines. Text uses ordinary whitespace split. These labelled counter
variants do not replace unchanged CPython rows or establish ordinary-script gains.

| Workload | Input | CLR allocation, sync / async | Quiet warm elapsed change | Wider process CPU change |
| --- | --- | ---: | ---: | ---: |
| CSV validation counter | 5,120 records | −41.41% / −41.40% | −17.41..−15.58% | −17.65..−14.38% |
| Text summary counter | 3,072 lines | −47.93% / −47.91% | −20.74..−18.68% | −21.57..−17.33% |
| JSON-to-Markdown counter | 3,072 records | −27.46% / −27.44% | −14.79..−11.93% | −19.31..−15.29% |
| Unchanged ordinary text control | 3,072 lines | Flat | −4.18..+1.04% | −4.44..+1.15% |

Desktop allocation uses the existing complete-job driver: 256 warmups/image,
five rotated rounds of 16 invocations, two identical reference images and one
candidate in isolated load contexts, shared JIT/CoreLib and ordinary GC/tiering.
Only core binaries differ; auxiliary identities are equal. Warm sync/async and
compilation are separate, with raw observations and summaries in their folders.
All outputs, stable shared-budget balances, final zero balance and cleanup pass.
Compilation allocation and three unchanged warm controls are flat.

Original desktop elapsed results remain diagnostic and include adversity: ordinary
text +3.79..3.88% sync / +1.48..5.89% async; captured CSV +0.17..0.97% sync;
JSON control +0.60..2.08% sync / +0.67..5.71% async. Counter-text sync spans
−8.23..+3.03%. Async CSV/report reference spreads reach 10.84%/14.69%; sync text
spread reaches 12.27%. Compile CSV control is +1.05..2.16%, and JSON compile
control spread reaches 14.21%. These samples are preserved, not replaced by the
quiet result. No neutrality, additive phase timing or general CPU claim follows.

## Quiet confirmation and boundaries

Four jobs finished in **145.631 seconds** on the existing quiet Azure F4as_v7,
Ubuntu 24.04.4, four AMD EPYC 9V45 vCPUs, 16 GiB/no swap, actual .NET 11 RC3.
Fixed worker `fec79afd`, normal optional adapter 0.4 and auxiliary files were
unchanged. Source-pair core hashes are `d5311220…` reference / `cb03fd47…` candidate.
Four independent processes per job, five-second warmups and seven rotated 400 ms
rounds; ordinary GC/tiering. Table ranges span all four process-median pairings,
not confidence intervals. [Exact summary](quiet/summary.json).

Reference/candidate process spread is 1.99%/0.23% CSV, 0.29%/2.31% text and
2.01%/1.32% report; max IQR 1.03%, 1.13% and 4.69%. Unchanged text spread
1.92%/3.46% leaves possible roughly +1.2% control regression debt unresolved.
The coherent context/name-walk removal and allocation reduction justify retention;
the supporting CPU evidence applies specifically to these counter workloads.

Warm clocks include mediated RAM-file I/O, full per-invocation validation and
prior-result cleanup. Compilation, fixture reset, separate verification, protocol,
startup and final cache teardown are excluded. Wider process CPU includes nearby
work and coarse ticks. Desktop clocks have their separately recorded boundary.
No pending-I/O, embedding-tail, RSS, CPython-ratio or final-producer claim is made.

Every identity, output, ownership, quiet-before/after, Closed, process-exit/join,
RAM cleanup and archive SHA check passes. Terminal service state is cached; no
workers or RAM fixtures remain. [Receipt](quiet/archive-receipt.json),
[original raw observations](quiet/observations.tgz). The 180-second owner bound
and unchanged 30-second stop/margin are retained.

Three remote compressed duplicates were removed only after comparison against
committed canonical nested archives, UID/path/link/size/SHA checks; protected
qualified images were unchanged. Final free space 262,021,120 B preserves the
240 MiB floor. Exact retirement and recovery proofs remain under quiet/ and in
preparation.tar.xz; no missing/adverse source or trace is discarded.

The preparation archive contains exact source/binaries, original failures and
goldens, focused TRXs/logs, recipes, desktop driver and quiet ownership records.
It preserves the prospective plan separately from current documentation. No
publication, backend promotion or completion of the performance objective follows.
