# Complete text-summary fresh compile/run — 2026-10-17 UTC

The existing complete file job now has a compile-to-result snapshot alongside
its historical warm measurements. CPython is the external baseline. The two
Lython roles are identical controls, not earlier/later implementations.

| Engine | Measured revision | Fresh compile/run median | IQR / median |
| --- | --- | ---: | ---: |
| Lython control 1 | 877f20e24870502082d7049f947aa5a97db713ab | 16.539 ms | 48.87% |
| Lython control 2 | Same runtime and worker files | 14.994 ms | 106.79% |
| CPython | 3.13.2, ordinary GIL and cyclic GC | 11.188 ms | 39.21% |

The identical Lython medians differ by 9.34%; spread is very large in all three
roles. This single local diagnostic is **inconclusive**. It establishes neither
a stable Lython/CPython ratio nor an optimization improvement. It cannot be
compared directly with the quiet-VM warm text snapshot [BF](../../../BENCHMARKS.md).
The text job does not exercise the preceding direct `str.replace` optimization.
No retuning, second timing capture, VM lane or ten-minute run was performed.

The unchanged [script](../../scripts/text-summary.py) lowercases and splits input
lines, counts words, writes a sorted report and prints the token count. Large
input: 1,024 blocks / 3,072 lines / 35,840 UTF-8 bytes; source: 353 UTF-8 bytes.
Output is `6144\n` and an exact four-line report. Independent literal-block
stdout/file goldens at 0, 16 and 1,024 blocks pass in both Lython roles and CPython
before sampling. All 35 micro responses preserve the large golden output/files.
The [frozen catalog](catalog.json) and [measurement](measurement.json) record the
source, input, output and binary identities.

Boundary: warm worker processes, newly compiled source and fresh invocation
state on every job. Timed work includes compilation, staged input/output reset,
execution, public capture/projection, complete stdout/file checks, disposal of
the per-job Lython compilation and previous result cleanup. Worker startup,
transport and full response hashing are outside. Compile-only and startup were
not separately measured. Compilation, execution and filesystem contributions
cannot be separated from these totals.

Both engines use the same logical fixture/reset/output work; their internals are
different. Lython keeps public containment, cooperative admission and explicit
result ownership. CPython has no equivalent in-process containment and uses
ordinary reference counting/cyclic GC. The fixture uses efficient existing host
file overrides. These practical differences are declared, not subtracted.

Machine: active Windows 11 Business development workstation, build 26200, x64;
Intel Core i7-14700KF, 20 cores / 28 logical processors. Release .NET SDK
10.0.300-preview.0.26177.108 / runtime 10.0.13; CPython 3.13.2, MSC v.1942 x64,
`-I -S -X utf8`, no debug/free-threaded mode. Workers keep normal GC/tiering;
no forced collections or runtime overrides. BenchmarkDotNet is not loaded.

Each Lython role has the same 50 immutable files, including the exact runtime
from the explicit Release package qualified at 877f20e2 / [CI39008651272](https://github.com/Lokad/Lython/actions/runs/39008651272). The
comparison adapter's own binary revision is 9321533b; its source did not change
between those commits. Loaded runtime revision, DLL SHA256 and package SHA512
are recorded separately, avoiding an implied adapter/runtime identity match.

One seven-round capture took 8.788 seconds under the existing 28-second process
bound plus one-second cleanup. Both 50-file manifests remain unchanged; all
seven correctness/timing workers and their fixture directories are gone.
The [raw capture](micro.json), [first-role goldens](baseline-verify.json),
[second-role goldens](candidate-verify.json) and [qualification summary](measurement.json)
preserve the evidence. No current-HEAD, embedding-latency or overall-gap claim.
