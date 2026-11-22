# B1343 Execute function-default staging as instructions

Measured `2aa1b17e2` replaces three staging descriptors with two
instructions. This eliminates descriptor allocations, constant-table
lookups and their operation dispatch. It keeps the same admitted
default storage, evaluation order and native/resumable cleanup.

Compared with `2d4fb517b` on November 22, 2026 UTC on the quiet Azure
F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3. Both 32-file images use the
same worker and optional PythonRe 0.4.0 adapter. Each revision has two
independent processes, twelve seconds of full-request warmup per
process and seven rotating batches. Changes span the four pairings of
process medians. Negative changes favor the candidate.

The warm boundary includes execution and disposal of the previous
result. Compilation includes code-owner disposal. Time to result adds
execution and result retirement. The fixtures use no guest filesystem
I/O. Process CPU also includes request handling. Startup and final
worker teardown are separate.

| Complete workload | Boundary | Elapsed change | Process CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Fully supplied helper | Warm | −2.13 to +0.76% | −1.46 to +0.88% | 1.84 / 1.09% |
| Eager factory | Warm | −3.23 to +0.31% | −2.40 to −0.87% | 2.03 / 1.60% |
| Mixed literal and dynamic defaults | Warm | −2.75 to +0.18% | −2.79 to +0.55% | 2.65 / 0.35% |
| Factory observing defaults | Warm | −4.65 to −1.60% | −5.02 to −1.91% | 1.36 / 1.81% |
| Postponed factory | Warm | −3.90 to +3.50% | −2.69 to +3.10% | 3.27 / 4.29% |
| Mixed literal and dynamic defaults | Compile | −0.30 to +1.05% | −0.77 to +2.34% | 0.11 / 1.25% |
| Mixed literal and dynamic defaults | Time to result | +2.32 to +5.26% | +1.37 to +5.04% | 1.99 / 0.87% |

The mixed factory takes longer to finish a new script under these
conditions. The default-observing warm script is consistently faster.
The helper, mixed warm and postponed observations span zero. The
targeted confirmation reverses worker order and adds a fully supplied
time-to-result control.

Local compilation allocates 304 fewer bytes for the mixed script and
424 fewer for each eager, postponed and default-observing script. The
fully supplied helper is unchanged. Warm allocation is effectively
flat. Accounted peaks match and allowances refund. The local elapsed
diagnostics vary between processes.

Windows .NET 11 and Ubuntu .NET 10 each passed 1,698 white-box and
4,062 public focused Release checks. Seven independent CPython goldens
match both local images, including all five quiet fixtures. All 196
timed batches verify. The collection completes in 491 seconds with
both quiet gates, joined workers and a SHA-verified persistent archive.

## Reversed-order confirmation and decision

Reused both complete images, the catalog, twelve-second warmup and
batch protocol. Both jobs measure time to result. Previous results and
code owners retire within the worker boundary. The mixed factory
runs first with the original worker order reversed.

| Complete workload | Elapsed change | Process CPU change | Elapsed spread, reference / candidate |
| --- | ---: | ---: | ---: |
| Mixed literal and dynamic defaults | −1.92 to +0.96% | −1.50 to +1.37% | 2.66 / 0.28% |
| Fully supplied helper | −4.51 to −0.01% | −4.00 to +0.54% | 3.83 / 0.85% |

The original adverse mixed result remains unexplained. The repeat
spans zero on both clocks and the fully supplied control moves.
Retain the removal of the three staging descriptors and the measured
compiler allocation benefit. Assess time to result again at the next
coherent checkpoint. All 56 confirmation batches pass in 144 seconds,
with both machine gates and joined workers. The archive verifies
under the lease after fixture cleanup.

[Timing](summary.json) · [Runtime allocation](allocation-summary.json) ·
[Compiler allocation](compile-allocation-summary.json) ·
[Raw observations](observations.tgz) ·
[Source and checks](checks-and-procedures.tar.xz) ·
[Confirmation timing](confirmation/summary.json) ·
[Confirmation observations](confirmation/observations.tgz) ·
[Confirmation procedures](confirmation/procedures.tar.xz).
