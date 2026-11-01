# Shared execution-state consolidation (B44)

Measured 2026-11-01, source **44e29e05 + source.patch + new-source** in the raw
archive; baseline **5d16ead3**, runtime statements **9e99f640**. Dedicated Ubuntu
24.04.4 Azure F4as_v7, four EPYC 9V45 vCPUs / 16 GiB, .NET 11 RC3. This is a
fixed development comparison against earlier Lython, not a new external baseline
or a qualified release latency multiplier.

## Deletion and behavior

Move the actual host operations, current exception, cancellation token and nesting
fields into the existing execution state. Delete ExecutionServices,
ExecutionGuards, ExecutionLimits and their construction, forwarding properties
and back-references. Lexical contexts, frames, closures and owned storage stay
separate. Current token propagation, refusal-before-cancellation ordering,
managed stack admission and awaited cleanup remain. No public API change or
new policy, pool, cache or accounting infrastructure.

[Native-offset attribution](../2026-11-01-main-loop-attribution/README.md)
selected this change. [Pre-code criteria](pre-code-plan.md) distinguish source
simplification from a measured improvement and freeze the controls before code.
Two obsolete tests' repeated checkpoints are reduced to the checks needed for
their independent ownership/cancellation assertions; their assertions remain.

## Fixed comparison

One **56.719-second** collection: six ordered before/candidate/identical-before
warm processes, then the existing compilation-only control. Each execution case
has four warmups and seven batches of 128 complete jobs. Ordinary GC/tiering;
no profiler, forced collection, retry, retiming or selected samples.

| Per complete warm job | Before | Candidate | Identical before |
| --- | ---: | ---: | ---: |
| CSV process CPU, ms | 7.78270 | 6.65207 | 7.78366 |
| CSV elapsed, ms | 8.51873 | 8.23589 | 8.26452 |
| CSV allocated bytes | 9,542,280 | 9,541,965.94 | 9,542,215.44 |
| Text process CPU, ms | 6.35520 | 6.25246 | 6.35502 |
| Text elapsed, ms | 6.78649 | 6.88198 | 6.93877 |
| Text allocated bytes | 5,518,687.19 | 5,518,637.88 | 5,518,788.69 |

CSV CPU is **14.53% / 14.54% lower**, text **1.62% / 1.61% lower** than the
two controls. CPU baseline drift is 0.0124% CSV / 0.0029% text; allocation drift
is below 0.002%. Every CPU batch exceeds 100 ms. The predeclared >=5% CPU
improvement on one script, <=5% CPU / 2% allocation regression on the other,
and control-drift bounds pass. Allocation is essentially unchanged.

**Elapsed improvement is smaller and uncertain:** CSV -3.32% / -0.35%; text
+1.41% / -0.82%. Do not describe the CPU result as a 14.5% latency improvement.
Process CPU includes GC/JIT and other process threads; the comparison does not
isolate a causal fraction of instruction-check cost or establish repeatability.

Compilation alone, an unaffected-phase control, is **95.175 / 94.699 us** for
baseline/candidate, IQR 5.6% / 3.3%; CPython 3.13.16 **54.618 us**, IQR 0.4%.
This does not establish a compilation improvement or update the maintained
[CPython comparison](../../../docs/BENCHMARKS.md). Startup is not measured.

## Boundaries and evidence

Warm public synchronous Run includes fixture reset, actual mediated file I/O,
complete stdout/file checks and result disposal. Compilation/startup are excluded.
The shared unbounded allowance exercises ownership; finite refusal is validated
separately. Results end each job, compiled owners end at process cleanup and their
budget returns to zero. All goldens, quiet gates and cleanup pass; all children
join, service inactive/dead/success, VM lease available.

Fresh nonincremental core/test/probe/worker copies agree before freezing.
Auxiliary host, adapter and backend binaries are byte-identical between roles;
only core DLL/PDB changes. Candidate core SHA256
`170166c60465465730040072bd4d847c1661091d9a7b39c1e34ff23a213d2251`;
baseline `4349680a91d753abf54888ccada9be6d0d97cc325b990b7de55fa3982d9f9c94`.
[Identities](candidate-identity.json), [summary](summary.json) and
[raw results](raw-results.tar.gz), SHA256
`f88060446c5c95434d9c6fe729f58dab9ef9f65b28b1eebd9db7065d8e89623e`, preserve
all samples, source patches/new files, catalog, controls and driver sources.
Collector/analyzer adaptations accompany them; no new benchmark framework.

Source validation: zero-warning Release build of both targets; all 3,956 native
and 15,474 public ordinary checks pass on each actual .NET 11 RC3 / .NET 10.0.8,
plus the focused regressions. Remaining delivery outcomes are recorded in
PLAN.md; these local results do not imply platform/stress/package completion.
