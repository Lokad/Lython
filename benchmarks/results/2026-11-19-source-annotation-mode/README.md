# B1145 annotation mode selected from source

Compared `802c7130e` and prototype `c37f96139` on the quiet Azure F4as_v7
with Ubuntu 24.04.4 and .NET 11 RC3 on November 19, 2026 UTC. The change
landed at `a050a9e19`. Both complete 32-file images verified against six
CPython 3.13 script goldens before staging.

Postponed annotations already carry their validated spelling from the
defining source. Using that fact removes the inherited execution flag,
the boolean producer and alternate instruction blocks. Each definition
keeps its Python evaluation order, including defaults and decorators.
The existing working dictionaries, type scopes and suspended operations
keep their ownership.

| Workload | Phase | Elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | --- | ---: | ---: | ---: |
| Annotated rules | Warm | −1.77% to −0.21% | −1.52% to +0.98% | 0.07 / 1.51% |
| Annotated rules | Compile-to-result | −8.42% to −2.52% | −7.94% to −3.88% | 3.19 / 3.15% |
| Annotated rules | Compilation | −4.28% to −1.95% | −5.38% to −2.53% | 0.75 / 1.67% |
| Read every annotation dictionary | Warm | −5.98% to −3.40% | −6.08% to −3.54% | 2.51 / 0.23% |
| Ordinary rules | Warm control | −1.10% to +1.96% | −3.35% to +1.46% | 0.20 / 2.89% |
| CSV invoice | Warm control | −0.83% to +0.42% | −0.58% to +0.58% | 0.82 / 0.44% |

Ranges cover all four pairings of process medians. Compile-to-result
and the read-every-time script improve, with supporting CPU. The
duplicate compile-to-result medians limit the precision of the gain.
Ordinary annotated warm CPU and the two unaffected controls span zero.

The two annotated scripts allocate 3.30% and 3.16% less during warm
execution and 2.25% and 2.10% less during compilation, respectively.
Accounted runtime peaks stay equal. Windows .NET 11 and Ubuntu .NET 10
each passed 320 public and 424 white-box checks. Existing coverage
includes mixed imported modes, postponed spelling, eager effects,
type parameters, generator suspension, admission and cleanup.

The collection took 324.97 seconds. All 120 timed batches passed, workers
joined and both quiet gates passed. The VM verified the archive under
the collection lease, and the guest fixtures were removed. Warm includes
mediated I/O, golden checks and previous-result cleanup. Compile-to-result
measures compilation, one execution and compiled-owner disposal together.
Startup, fixture reset and final teardown are outside guest elapsed.
The wider process CPU boundary includes reset and protocol handling.

[Measurements](summary.json) · [Raw observations](observations.tgz) ·
[Lease receipt](archive-receipt.json) · [Checks and procedures](checks-and-procedures.tar.xz).
