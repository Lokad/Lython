# Complete configuration execution attribution — R56/B414

2026-11-07. Source `7df9b465`, frozen core `11220cf7b773a38b`, unchanged
worker and normal optional 0.4 adapter. Quiet Azure F4as_v7 / Ubuntu 24.04.4 /
.NET 11 RC3. One maintained `scripts.config-edit.small` (181 B / 16 paths),
compiled once. Full warm execution includes mediated RAM-file I/O, stdout/file
goldens and prior-result cleanup. Compilation, startup, staging/reset and final
cached teardown are excluded. This is CPU attribution, not a source comparison
or normal-runtime elapsed measurement. No runtime prototype was introduced.

6.105 guest-second warmup, two-second settle, six-second wall CPU-clock/FP capture
at 199 Hz; 32 complete jobs per protocol request reduce transport overhead.
48,992 complete invocations, summed job clocks 4.1861 s; named guest-worker CPU
2.91 s, **551 guest samples / 33 unknown leaves**. All-thread records include
thread-pool CPU outside the guest clock. Do not add that CPU to timed execution.
PerfMap3 and five JIT listings are declared diagnostic overrides. Raw stacks,
maps, all threads and inlining/unknown limits remain archived.

| Phase | Inclusive samples | Interpretation |
| --- | ---: | --- |
| CreateBuiltinVariables | 17 | Fresh connected type/constructor/exception graph; not shown dominant |
| ExecutionContext constructor | 29 | Includes initialization; overlaps builtin creation |
| CreateSuccessfulResult | 1 | No evidence of a dominant projection phase |
| TextFileHandle.Open | 279 | Includes actual host/file work |
| ComparisonFileHost.OpenFileAsync | 269 | Includes contained path resolution and stream opening |
| ComparisonFileHost.Resolve | 152 | Overlaps opening; do not add these counts |
| Executable instruction prefix | 457 | Includes useful inlined guest work, not a removable dispatch share |

Kernel path lookup, locking and filesystem work appear prominently among leaves.
Sampling/inlining mean 17/551 is not a strict upper bound on builtin setup. It
nevertheless does not justify a new lazy namespace/materialization subsystem.
Existing source already shares ordinary immutable functions/signatures, reuses
workers and implements scalar sum accumulation. Object slots bind actual run-owned
types; constructor/exception descriptor caches need explicit identity, isolation,
concurrency and funding proof before sharing. No lazy-builtin redesign is selected
on this evidence; a materially simpler design/new evidence may justify revisiting.

The comparison host validates path components/kinds/symlinks inside Lython's timed
open. CPython uses ordinary builtin open; fixture staging/reset is excluded in both.
This is a real embedding-boundary asymmetry, not proof that the whole published
gap is interpreter dispatch or file I/O. There is no CPython phase capture or wall
I/O decomposition here. Preserve capability checks rather than changing the host
to improve reported timings.

Exact core/auxiliary/Ready identity, pre/post goldens, quiet endpoints,
Closed/exit-zero/join/empty-fixture checks pass. Evidence was archived and verified
under the lease, then independently verified locally; the owned fixture tmpfs was
unmounted/removed and every user joined. 120 s owner + 30 s stop + 30 s observation
margin. B414 is terminal; no further service query or unchanged qualification.

**Original B413 failed before any guest/profiler started:** a local helper closure
captured a loop filename as the collection root. Preserve its original source,
contract, journal and terminal archive in [failed-setup](failed-setup/).
B414 binds and validates the intended root; source, core, worker, input, sampling
sizes and acceptance gates are unchanged. This corrects setup, not runtime or
performance. Both archive hashes match their receipts.

[Raw observations](observations.tgz), [receipt](archive-receipt.json),
[identity](identity.json), [contract](contract.md),
[guest leaves](guest-cpu-leaves.json), [chains](guest-call-chains.json).
R55's full platform/package qualification remains separately completed; the
profile does not require another unchanged delivery. The whole-script performance
objective remains open.
