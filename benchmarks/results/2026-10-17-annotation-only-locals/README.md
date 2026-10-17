# Complete CSV job with an annotation-only local — 2026-10-17 UTC

| Engine | Fresh compile/run median | IQR / median | Status |
| --- | ---: | ---: | --- |
| Lython, guard-only candidate | 19.745 ms | 48.9% | Inconclusive |
| CPython 3.13.2 | 17.014 ms | 13.5% | No qualified ratio |

The local diagnostic establishes no qualified Lython/CPython ratio or speedup.
Earlier Lython `3f799746` measured 21.677 ms with 38.0% spread in the same capture;
its nominal 8.9% difference from the candidate is inconclusive. No allocation,
embedding-latency or current-HEAD performance claim follows. Stop this capture.

The [complete script](workload.py) parses 4,096 CSV records / 57,361 UTF-8 input bytes,
aggregates totals, writes the complete sorted report and prints `3072`. It is the
existing CSV helper job with a declaration-only `result:int` and an assigned local
return; original scripts, inputs and goldens are preserved. Source: 632 UTF-8 bytes.
All 36 verification/warmup/calibration/measurement responses match independent
stdout and full file goldens, including accented/supplementary/quoted-newline
input. The [catalog](catalog.json) also retains 0/16-block fixtures; this capture
selects only the 1,024-block case. Those other cases were not run here.

Baseline: `3f7997466adf3e07385642c3f1cd88bba0ed854f` from its explicit Release
package. Candidate: exactly that source plus the [one-line deletion](guard.patch.txt),
built Release before commit. The candidate measurement uses the normal Release
build DLL, not an asserted NuGet release artifact. The guard forced nongenerator
functions with annotation-only locals into lowered interpretation; removing it
shares existing target-read lowering and scope facts. No parser, binder, memory
mechanism, runtime configuration or dependency changed.

Each frozen worker has 50 files. Only `Lokad.Lython.dll` differs; the existing
comparison adapter and dependencies are byte-identical. [Measurement identities](measurement.json)
record both full manifests, candidate production sources, the baseline package
SHA512, loaded libraries, process receipt and cleanup. Source/build options are
Release; .NET SDK 10.0.300-preview.0.26177.108 / runtime 10.0.13; normal GC/tiering.
No simultaneous local tests/builds, forced collections or runtime overrides.
All recorded workers and file-fixture directories are gone; all 100 frozen files
still match their hashes. BenchmarkDotNet is not loaded.

Boundary: warm processes, fresh compilation and invocation state per job.
Timed work includes compilation, file staging/reset, execution, public capture,
full stdout/file validation, previous-result cleanup and compilation disposal.
Startup and transport are outside. Compile-only and warm execution were not
sampled separately. Both engines use the same logical file fixtures; Lython
retains public containment/cooperative ownership, CPython ordinary reference
counting/cyclic GC. The comparison host provides efficient file overrides.

Machine: active Windows 11 development workstation, build 26200 x64; Intel
Core i7-14700KF, 20 cores / 28 logical processors. CPython 3.13.2, MSC 1942 x64,
`-I -S -X utf8`, ordinary GIL/GC. Seven rounds in one 11.228-second owned capture,
with the existing 28-second process bound / one-second cleanup. There is no retuning,
repeat, long lane or quiet-VM qualification. [Raw capture](micro.json) preserves
all attempts. [BENCHMARKS.md](../../../BENCHMARKS.md) is the authoritative account.
