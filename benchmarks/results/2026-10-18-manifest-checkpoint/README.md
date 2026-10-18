# Complete file-manifest helper comparison, 2026-10-18 UTC

| Fresh compile + synchronous execution | Lython | CPython | Lython / CPython | Status |
| --- | ---: | ---: | ---: | --- |
| Two files / 4,107 input bytes; copy/checksum/manifest | 2.027–2.251 ms | 0.597 ms | 3.40–3.77× diagnostic | Inconclusive: Lython spreads 30.37% / 31.64%, control difference 11.02%; CPython spread 0.83% |

Two distinct Lython processes load identical immutable revision and binaries:
**3ea431fdc0cb45fc3d3dcb1678a6d0f605d782c8**. CPython **3.13.16** is the primary
external baseline. Ratios divide Lython elapsed time by CPython elapsed time.
The range contains two identical-control medians, not confidence bounds or
different Lython revisions. This one capture shows a gap, but the Lython spreads
and control divergence leave its magnitude uncertain. No speedup, regression or
individual-fix attribution is established. Historical warm snapshot AD has a
different boundary and must not be used to infer a trend.

The existing [574-byte helper script](source.py) sorts a directory, reads and
copies [4,096 binary bytes](input-a.bin) and [11 UTF-8 bytes](input-unicode.txt),
computes exact checksums through its annotated helper, and writes a complete
sorted [97-byte manifest](expected-manifest.json). It prints [2](expected-stdout.txt).
The catalog's independent constants reproduce both copied files, checksums,
Unicode filename and stdout; isolated CPython 3.13.2 also verifies the complete
byte maps before timing. Every timed response checks stdout and file digests.

## Boundary and fairness

Each job compiles source afresh and executes it once synchronously inside an
already-running worker. Both engines include a fresh invocation/namespace,
capture, file staging/reset, execution and complete output checks. Lython also
includes prior-result cleanup and disposal of each fresh compilation. Startup,
transport and response hashing are excluded. There is no identical-script cache
in this lane. Warm execution, compilation alone, process startup and genuinely
suspending host I/O are separate boundaries; none is inferred by subtraction.

Lython keeps public limits, cancellation, managed stack admission, cooperative
ownership and allocation preflights. The shared unbounded allowance funds
compilations/results, final results remain owned through response writing, and
verification compilations are disposed on worker exit. CPython keeps ordinary
reference counting/cyclic GC without equivalent in-process containment. Nothing
is subtracted. This does not qualify finite-budget streaming, denied admission,
embedding tail latency or physical memory/RSS bounds.

## Provenance and one bounded capture

Dedicated Azure Standard_F4as_v7: Ubuntu 24.04.4 LTS, Linux 6.17.0-1022-azure,
four AMD EPYC 9V45 vCPUs, 16 GiB RAM and no swap. Clean Release build:
.NET SDK 10.0.401/runtime 10.0.12. CPython 3.13.16: GCC 13.3.0, PGO/LTO,
ordinary GIL/cyclic GC, no debug/free-threaded/JIT mode, `-I -S -X utf8`.
[Inspection](inspection.json), [preparation](preparation.json),
[build log](build-candidate.log), [preflight](preflight.json) and
[sealed scope](capture-scope.json) record machine, source and binary identities.

One existing 15-second micro command completed in **7.649099 seconds**, with
one-second warmup per engine, two-second settling and seven rotating-order
batches calibrated to 25 ms. The owned service enforced 28 seconds work plus
one second cleanup. Only its supervisor disables tiering; worker launch removes
that override. All Lython worker overrides are null, ordinary GC/tiering remain
enabled, and BenchmarkDotNet is not loaded. The shared VM lease is held through
collection. Inspection load is zero; preflight load after the build is
0.30/0.10/0.03. The noisy first capture is retained without retries or profiles.

All **39 responses / 3,670 invocations** match source, fixture, stdout and complete
file digests. [Validation](validation.json) recomputes every median and IQR from
[raw batches](compile-run/micro.json), verifies identical Lython binaries and
reproduces the complete goldens. Sources remain clean/immutable and input hashes
unchanged. All three workers and fixture directories are gone, the process group
is empty and the lease is released. [Ownership](compile-run/ownership.json),
[terminal receipt](compile-run/terminal.json) and [final cleanup](final-cleanup.json)
retain evidence. [Raw archive](raw-evidence.tar.gz) SHA256:
`b114531966c5cecdb0f52dfd5f88ec776feb9137b8b7d3d15f0576066c5d72dd`.

This checkpoint is closed. The [previous leading comparison table](previous-comparison-table.md)
preserves historical and warm observations unchanged; their detailed evidence
remains linked. Runtime revision 3ea431fd has cached-green
[Windows/Ubuntu CI39147966089](https://github.com/Lokad/Lython/actions/runs/39147966089),
including Release package consumers. This evidence-only delivery changes no
runtime, test, benchmark implementation, worker, script or dependency.
