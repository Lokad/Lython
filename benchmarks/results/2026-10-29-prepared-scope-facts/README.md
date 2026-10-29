# Prepared lexical scope facts

Candidate **cc988b9d288a0622f9849a0e1e04a3f692fe5305**, control
**1c5cff2a59d1defd94049e70940267a569cc717a**. One fixed collection on
**2026-10-29**, both .NET 11 RC3 (SDK/runtime 11.0.100-rc.3.26522.148), CPython
3.13.16 PGO/LTO, dedicated Azure Standard_F4as_v7 Ubuntu 24.04.4, four EPYC 9V45
vCPUs, 16 GiB RAM, no swap. Ordinary worker GC/tiering; before/after quiet gates
pass. Nine jobs take **70.33 seconds**, all complete stdout/file goldens pass.
**No elapsed result is performance-qualified. No timing retries.**

## Structural change and acceptance

Reuse prepared function/class/lambda scope facts across static analysis, lexical
super preparation, lowering and runtime class creation. Replace the separate
prepared class-cell booleans with the scope result already containing that fact.
The existing preparation pass runs after parser/name rewriting and before static
analysis; prepared syntax is not subsequently rewritten. Raw syntax subsystem
entries retain their needed collection behavior. No new scope-analysis framework,
cross-script cache, public option, execution backend or alternate class route.

Retained facts belong to the compilation owner and its conservative estimate.
They contain names/lexical metadata, never an execution or host. Existing lowered
and executable consumers remain; the rejected executable-class migration is not
reactivated. Managed stack admission, cancellation and ownership are unchanged.

The focused Release selection passes **374 internal + 3,429 public** checks on
Windows/Ubuntu and both .NET 11/.NET 10. Both-target builds have zero warnings or
errors. Windows net11 full ordinary also passes **3,882 internal + 15,060 public**
checks. This includes scope/directive rejection, Python diagnostics, classes,
closures, super, annotations, generic types, imports and complete scripts through
both APIs. Final delivery checks qualify their own recorded revision separately.

## Compilation, complete scripts and control

Medians in microseconds; IQR is interquartile range / median. Compilation rows
time fresh compilation only, with complete script verification outside the clock.
Compile/run rows retain the existing complete-job reset/output/cleanup boundary.
The loop row reuses code. Identical sources/inputs/goldens apply to every engine.

| Lane / workload | Control us | Candidate us | CPython us | Control / candidate IQR |
| --- | ---: | ---: | ---: | --- |
| Compile / CSV helpers | 206.8 | 197.4 | 54.6 | 10.0% / 5.2% |
| Compile / text helpers | 144.5 | 155.4 | 42.2 | 1.6% / 12.4% |
| Compile / JSON-lines helpers | 295.0 | 281.5 | 53.7 | 9.5% / 19.6% |
| Compile / small config edit | 147.2 | 127.0 | 35.0 | 27.1% / 8.4% |
| Compile / file manifest helpers | 322.4 | 411.9 | 59.1 | 23.8% / 3.1% |
| Compile / file search | 220.1 | 301.9 | 32.0 | 19.8% / 17.2% |
| Compile/run / CSV helpers | 18,660.1 | 17,577.6 | 3,721.0 | 165.0% / 220.1% |
| Compile/run / text helpers | 16,768.3 | 16,933.3 | 1,452.7 | 216.6% / 180.6% |
| Warm / integer loop control | 1,054.3 | 1,051.1 | 634.9 | 0.9% / 0.9% |

Several compile medians increase; complete-script rows are extremely noisy. Keep
them visible. They establish neither an overall throughput gain nor a qualified
regression. Separate reflected phase timing has a different warmup/reflection
boundary and does not reproduce the apparent file-search/manifest increases;
it cannot replace these rows. The config case has no allocation change despite
its lower median, another reason not to attribute medians mechanically to source.
Do not retime for a preferred result or claim a .NET/runtime speedup.

## Exact allocation diagnostic

Reuse the existing [phase diagnostic](../2026-10-29-foundation/diagnostics/PhaseDiagnostics.cs)
against each actual core DLL, with the same six frozen sources, normal compilation
preflight, 32 warmups and 64 observations per source. Public compilation counts
current-thread allocations; phase reflection includes its own overhead. These
are allocation counts, not physical retained heap or a certified memory envelope.

| Workload | Control public compile bytes | Candidate bytes | Reduction |
| --- | ---: | ---: | ---: |
| Small config edit | 308,672 | 308,672 | 0 |
| File search | 267,832 | 257,880 | 9,952 |
| CSV helpers | 461,656 | 452,808 | 8,848 |
| Text helpers | 372,808 | 361,944 | 10,864 |
| JSON-lines helpers | 431,216 | 422,560 | 8,656 |
| File manifest helpers | 518,920 | 510,072 | 8,848 |

Five sources allocate **1.7–3.7% less** during public compilation. Executable
construction also allocates 1,592–1,960 fewer bytes for those sources. All 768
recorded public compilation owners and 768 reflected-phase owners return to zero; admitted
compiled storage estimates are unchanged. No collection is forced.

Retain the consolidation for removing repeated analysis and duplicate flags with
demonstrably lower compilation allocation, **not** a claimed elapsed speedup.
This closes the bounded scope-fact slice. Together with explicit positional
activations' bounded CLR growth, it is the accepted M4 delivery; broader scalar
representation, a native-callback trampoline, executable classes and a JIT remain
separate hypotheses rather than automatic 0.11.0 requirements.

## Evidence and reproduction

[collect.py](collect.py) uses the existing micro CLI, catalogs, shared VM lease,
seven rotating 25 ms batches, normal worker tiering and the thirty-second owned
systemd boundary per job. The supervisor alone disables tiering. Six compile rows,
two complete scripts and one loop control are fixed prospectively; allocation
diagnostics follow timing. There is no new benchmark framework or production hook.

[raw-results.tar.gz](raw-results.tar.gz) preserves the unmodified collection,
machine gates, all nine full micro receipts/logs and both complete phase records.
The receipts carry source/core/toolchain/catalog identities and all observations.
The catalog itself is the existing frozen foundation catalog. The separate Linux
focused build/test logs are under `.git/agent-notes/prepared-scope-facts-20261029`.
