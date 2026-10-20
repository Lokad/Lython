# Native text predicate cancellation

2026-10-20 UTC. Baseline `22d93ca1` (qualified runtime `51b53922`), followed by
frozen per-rune and final chunked working-tree variants. Final qualified runtime
is `f0cd695fd883915f0989a6fb88844c35f3499d57`. Loaded assembly hashes, common driver hash,
UTC times, OS and .NET details are preserved in the raw captures.

[CI 39360240398](https://github.com/Lokad/Lython/actions/runs/39360240398) is
terminal green for this exact runtime: Windows/Ubuntu Release, original Stress
and both explicit Release package consumers pass. All eight required final steps
were inspected. [Cached proof](delivery.json) is authoritative forever; never
query the run again. Four status queries were at least 180 seconds apart.
The reproduction project also builds successfully. All timings below remain
unqualified. A following documentation/evidence-only commit preserves qualified
runtime/test/build/CI inputs. No publication.

The two shared case/all-rune loops lacked cancellation polls, and their ten
runtime method delegates discarded context/span. Eleven red controls demonstrate
the gap: a predicate requests cancellation inside its active scan, and native
delegates fail to observe a supplied cancelled context without ambient state.
The final change forwards those existing arguments and checks the existing guard
between small UTF-8 byte chunks. Complete scalars can cross a chunk boundary.
There is no per-rune counter, new allowance, policy, option or dispatch layer.

| Method/API | Earlier uninterrupted | Final uninterrupted | Earlier request-to-cleanup | Final request-to-cleanup |
| --- | ---: | ---: | ---: | ---: |
| islower / Run | 160.185 ms | 162.274 ms | 154.655 ms | 5.562 ms |
| islower / RunAsync | 160.637 ms | 167.112 ms | 162.491 ms | 1.846 ms |
| isalpha / Run | 89.196 ms | 101.129 ms | 77.132 ms | 0.084 ms |
| isalpha / RunAsync | 87.911 ms | 99.076 ms | 83.474 ms | 0.149 ms |

These are single **unqualified observations**, with a 32 MiB ASCII subject,
an independent dedicated host caller and Lython's normal startup workers.
They establish no throughput ratio, latency
guarantee, whole-job improvement or CPython comparison. Final uninterrupted
observations are nominally 1.3–13.4% higher; the remaining cost stays visible.
The first per-rune prototype was 33–43% higher, prompting chunked checks. Preserve
that rejected variant's evidence rather than dropping its cost from the account.
No repeated timing, full lane, GC forcing or benchmark framework was introduced.

The boundary begins at the existing host stdout callback for `print('ready')`,
then runs one predicate, final output/failure processing and awaited cleanup.
Startup, compilation and input normalization precede the signal. Cancellation
is requested after a nominal 10 ms delay; actual scheduling delays vary and are
recorded. All eight cancelled observations across the earlier/final pair were
pending with their input funded when cancellation was requested. They returned
Cancelled without publishing the predicate result. Public timing supports the
integration result; the deterministic internal callback control proves a request
inside an active loop. Entry/exit checks alone are insufficient evidence.

The same driver and source run both execution APIs. Each script has one small
warm control per API, one uninterrupted observation and one cancelled observation.
Tiered compilation is disabled. Each process reserves one explicit 256 MiB shared
allowance; every run returns to its compiled baseline and compilation disposal
returns the root to zero. CLR physical collection is independent. No local
build/test overlaps the captures. All three owned processes exit successfully in
1.50–1.96 seconds including startup, preparation, executions and cleanup.

Evidence: [earlier](before.json), [per-rune prototype](per-rune-prototype.json),
[final chunked](chunked.json), [owned process receipts](processes.json),
[Release checks/source identities](checks.json), and
[18 independent CPython 3.13.2 Unicode goldens](cpython-goldens.json).
Public goldens cover direct/stored methods under both APIs after delayed host I/O,
including empty input, early exits, Unicode categories and split UTF-8 boundaries.
The existing representative/shared-budget/provider controls also pass.

Reproduction source is the [one-off driver](Program.cs) and
[project](PredicateDiagnostic.csproj). Supply `-p:CoreDirectory` with an
immutable core DLL plus Lokad.Parsing and the existing MockLythonHost source;
reuse the driver bytes with each core snapshot, set DOTNET_TieredCompilation=0,
and run with a revision label and output JSON path. Use the existing owned
28-second work/1-second cleanup harness and await all users. This diagnostic is
Lython-specific; CPython remains the external baseline in
[BENCHMARKS.md](../../../BENCHMARKS.md). Stop this investigation at these loops.
