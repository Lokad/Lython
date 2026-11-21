# B1289 structurally shared abstract-state forks

The prototype replaces two full mutable map copies at each analysis fork
with shared immutable roots and independently mutable builders. Branches
still own their value facts, length bounds and resolution caches.
Invalidation can rewrite the active branch while visiting its snapshot,
which removes the rewrite list. The value map checks backing identity
when setting an entry. Recursive value joins remain explicit.

Reference `256c5e651` and prototype `393ef9913` ran on the quiet Azure
F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3 on November 21, 2026 UTC.
Each lane uses two independently warmed processes per source and seven
rotated batches per process. The ranges span all four pairs of process
medians. Compile elapsed includes compiled-owner disposal. Time to result
also includes one run, mediated RAM-backed I/O, result observation and
cleanup. Process CPU covers the enclosing request. Startup and final
teardown are outside these clocks.

| Workload | Phase | Elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | --- | ---: | ---: | ---: |
| Postponed nested factories | compile | +5.17% to +5.80% | +4.51% to +5.80% | 0.35% / 0.24% |
| Inherited dataclass configuration | compile | +6.32% to +8.62% | +6.43% to +9.31% | 1.28% / 0.87% |
| Generated 96-field validator | compile | +3.05% to +5.74% | +2.49% to +7.89% | 0.58% / 2.03% |
| Dense scalar control | compile | +111.60% to +117.47% | +109.93% to +117.53% | 0.12% / 2.65% |
| Generated 96-field validator | compile-run | +4.18% to +5.91% | +5.26% to +6.03% | 1.01% / 0.64% |

The local allocation diagnostic measures `2f9ad5677` against `40ba42fdc`
on Windows .NET 11. It counts current-thread allocations through complete
public compilation and compiled-owner disposal. Each invocation returns
the borrowed budget to baseline. This source pair differs from the quiet
pair because both local images include the later operand-forwarding
cancellation checkpoint. The diagnostic makes no elapsed claim.

| Script | Reference allocation | Prototype allocation | Change |
| --- | ---: | ---: | ---: |
| Postponed nested factories | 66,176 B | 65,008 B | −1.76% |
| Inherited dataclass configuration | 582,512 B | 571,168 B | −1.95% |
| Generated 96-field validator | 2,186,391 B | 2,184,086 B | −0.11% |
| Dense scalar control | 3,333,856 B | 1,490,736 B | −55.28% |

The synthetic scaling probe holds branch count at 48 while varying the
number of live scalar bindings. Allocation savings grow from 10.55%
at four bindings to 35.21% at 32 and 55.28% at 256. The corresponding
branchless cases allocate 1.58–5.99% less.

Keep private. Structural sharing removes the full map copies and saves substantial allocation in dense scalar forks. Representative compilation takes 3.05–8.62% longer, validator time to result rises 4.18–5.91%, and the dense scalar control compiles 111.60–117.47% slower. CPU also rises in all five lanes. The data rule out this map replacement. A next design must retain direct lookups and avoid full-state joins as well as copies.

Windows .NET 11 and Ubuntu .NET 10 each pass 17 white-box, 772 public
and 18 supplemental ownership checks. The first Ubuntu public run hit
a missing worker root marker in a benchmark supervisor test. The
unchanged rerun passed. Both complete frozen images match four CPython
goldens. All 140 timed batches and machine gates pass. Workers join,
RAM-backed fixtures are removed and the persistent archive is verified
under the lease.

[Timing summary](summary.json) · [Local allocations](local-allocations.json) ·
[Scaling controls](fork-scaling.json) · [Raw observations](observations.tgz) ·
[Source, checks and procedures](checks-and-procedures.tar.xz).
