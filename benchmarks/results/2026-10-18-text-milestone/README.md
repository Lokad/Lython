# Complete text helper comparison, 2026-10-18 UTC

| Fresh compile + synchronous execution | Lython | CPython | Lython / CPython | Status |
| --- | ---: | ---: | ---: | --- |
| 3,072 lines / 35,840 input bytes | 7.354–7.451 ms | 1.527 ms | 4.818–4.881× | Diagnostic; Lython spreads 16.27% / 3.41%, CPython 1.96% |

Both Lython controls use the **same immutable revision and binaries**:
**4acd532f7848f8a3a4cd54ff5c02d75320190830**. Runtime, test, comparison and script
sources equal qualified runtime revision **73deb8dc**. The measured revision
contains documentation only beyond that baseline; the rejected ASCII casing
prototype is absent. CPython **3.13.16** is the external baseline. Ratios divide
Lython time by CPython time. Ranges are the two identical controls' medians, not
confidence intervals, improvements against earlier Lython or later HEAD results.
One short capture leaves the multiplier unqualified, especially with the 16.27%
spread. The gap in this run remains useful evidence, without attributing it to
casing, calls, compilation or any other individual mechanism.

The existing [script](source.py) reads the entire [input](input.txt), calls an
annotated lower/split helper, counts Unicode words through dictionary get/update,
sorts keys, writes the complete [report](expected-report.txt) and prints
[6144](expected-stdout.txt). It processes accented and supplementary text plus
blank lines. The catalog size 1,024 means 1,024 three-line blocks. Script size is
424 UTF-8 bytes; the report is 42 bytes. Independent host-side Python aggregation
reproduces both goldens. Every response also checks complete stdout/file hashes.

## Boundary and fairness

Each timed job compiles source afresh through the real compiler and runs it
synchronously inside an already-running host. There is no identical-script cache
in this lane. Both engines include fresh namespace/invocation state, capture,
input staging/output reset, execution and complete output-file checks. Lython
also includes prior-result cleanup and disposal of each fresh compilation.
Worker startup, protocol transport and response hashing are excluded. Separate
compilation, warm-only execution, startup and genuinely suspended I/O are not
measured, and cannot be derived by subtracting historical measurements.

Lython retains public limits, cancellation, managed stack admission, cooperative
ownership and allocation preflights. Its shared unbounded host allowance funds
compilations/results; the final result remains owned until response writing
finishes, and cached verification compilations are disposed on worker exit.
CPython keeps ordinary reference counting and cyclic GC without equivalent
in-process containment. Nothing is subtracted. This is not finite-budget
streaming, refused-admission, host-latency or exact physical-memory qualification.

## Provenance and bounded collection

Dedicated Azure Standard_F4as_v7: Ubuntu 24.04.4 LTS, Linux 6.17.0-1022-azure,
four AMD EPYC 9V45 vCPUs, 16 GiB RAM and no swap. The clean Release build uses
.NET SDK 10.0.401 / runtime 10.0.12. CPython uses GCC 13.3.0, PGO/LTO, ordinary
GIL/cyclic GC, no JIT/debug/free-threaded mode, launched with `-I -S -X utf8`.
The [raw receipt](compile-run/micro.json) records loaded identities and hashes;
[inspection](inspection.json), [preflight](preflight.json),
[preparation](preparation.json) and [build log](build-candidate.log) retain the
machine and build evidence. The controls are distinct processes loading identical
libraries, not two Lython revisions.

Exactly one existing 15-second micro command ran, with one-second warmup per
engine, two-second settling and seven rotating-order batches calibrated to
25 ms. The owned collection completed in **7.725 seconds**. Its systemd group
enforced 28 seconds of work plus at most one second cleanup. Only the supervisor
disables tiering; worker launch removes that override. All worker overrides are
null, ordinary GC/tiering remain enabled and BenchmarkDotNet is not loaded.
Inspection load was zero; preflight load after the build was 1.08/0.29/0.10.
The shared VM lease was held for collection. This is a short diagnostic, without
milestone noise qualification, a repeat capture, profile or ten-minute lane.

All **35 responses / 1,074 invocations** match source, fixture, stdout and file
digests. [Validation](validation.json) independently recomputes every median and
IQR from the raw batches, verifies identical binaries and reproduces goldens.
Sources remain clean and immutable, all input hashes match, all three workers
and fixture directories are gone, the service group is empty and the shared
lease is available. [Final cleanup](final-cleanup.json),
[ownership](compile-run/ownership.json) and launch/terminal receipts retain that
evidence. The [raw archive](raw-evidence.tar.gz) includes the unchanged catalog,
build/capture recipes and complete receipts; SHA256:
`2bda51e563d19a9bbb360156b4bc85002d380c0d7608d4d861b5056a8d260856`.

This bounded comparison is closed. The earlier Windows text snapshot BR remains
historical and inconclusive; its different machine/revision/script boundary
cannot establish a trend. Runtime baseline [CI39106588147](https://github.com/Lokad/Lython/actions/runs/39106588147)
and measured documentation revision [CI39107969578](https://github.com/Lokad/Lython/actions/runs/39107969578)
are cached green on Windows and Ubuntu, including their Release package checks.
