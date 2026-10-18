# Complete configuration edit comparison, 2026-10-18 UTC

| Fresh compile + synchronous execution | Lython | CPython | Lython / CPython | Status |
| --- | ---: | ---: | ---: | --- |
| 16 paths / 181 input bytes; complete read/edit/write job | 2.584–2.769 ms | 1.399 ms | 1.847–1.980× diagnostic | Lython spreads 16.63% / 9.79%; CPython 8.93%; unqualified |

Two distinct Lython processes load the **same immutable revision and binaries**:
**348998dc05d9a562c7b14691accb4a566824ebbb**. CPython **3.13.16** is the primary
external reference. Ratios divide Lython time by CPython time. The range contains
the medians of the two identical controls, not confidence bounds or different
Lython revisions. The controls differ by 6.69%; one Lython spread is 16.63%.
This single capture shows a gap on this job, with its precise magnitude uncertain.
It does not establish a speedup against historical configuration snapshot AF or
attribute time to compilation, initialization, JSON, host I/O or any individual fix.

The existing [357-byte script](source.py) reads [config.json](input-config.json),
sets enabled, increments retries, appends one generated path and writes the
complete sorted UTF-8 [192-byte configuration](expected-config.json). Accented
and supplementary text and an untouched nested object are preserved. It prints
[17](expected-stdout.txt). Independent host-side JSON parsing/editing/serialization
reproduces the entire file and stdout, with source/input/output hashes checked
before collection. Every timed response checks complete stdout and file digests.

## Boundary and fairness

Each timed job compiles source afresh and executes it once synchronously inside an
already-running worker. There is no identical-script cache in this lane. Both
engines include a fresh invocation/namespace, capture, input staging/output reset,
execution and complete output checks. Lython also includes prior-result cleanup
and disposal of each fresh compilation. Worker startup, transport and response
hashing are excluded. Warm execution, compilation alone, startup and genuinely
suspending host I/O are separate boundaries; none can be inferred by subtraction.

Lython keeps public limits, cancellation, managed stack admission, cooperative
ownership and allocation preflights. Its shared unbounded host allowance funds
compilations and results; final results remain owned through response writing,
and cached verification compilations are disposed on worker exit. CPython keeps
ordinary reference counting and cyclic GC without equivalent in-process
containment. Nothing is subtracted. This does not qualify finite-budget streaming,
refused admission, host tail latency or physical memory/RSS bounds.

## Provenance and bounded collection

Dedicated Azure Standard_F4as_v7: Ubuntu 24.04.4 LTS, Linux 6.17.0-1022-azure,
four AMD EPYC 9V45 vCPUs, 16 GiB RAM and no swap. The clean Release build uses
.NET SDK 10.0.401/runtime 10.0.12. CPython 3.13.16 uses GCC 13.3.0, PGO/LTO,
ordinary GIL/cyclic GC, no debug/free-threaded/JIT mode and `-I -S -X utf8`.
[Inspection](inspection.json), [preparation](preparation.json),
[build log](build-candidate.log) and [preflight](preflight.json) retain identities,
machine and build evidence. The [sealed capture scope](capture-scope.json) pins
the selected case and single collection; Windows CRLF capture metadata is
distinguished from the canonical LF stdout golden before collection.

Exactly one existing 15-second micro command ran, with one-second warmup per
engine, two-second settling and seven rotating-order batches calibrated to 25 ms.
The owned collection completed in **8.074056 seconds**. Its systemd process group
enforced 28 seconds work plus at most one second cleanup. Only the supervisor
disables tiering; worker launch removes that override. All Lython worker overrides
are null, ordinary GC/tiering remain enabled and BenchmarkDotNet is not loaded.
The VM lease was held for collection. Inspection load was zero; preflight load
after the build was 0.12/0.06/0.02. This is a short diagnostic, not milestone noise
qualification, a repeated capture, profile or ten-minute lane.

All **39 responses / 2,456 invocations** match source, fixture, stdout and file
digests. [Validation](validation.json) independently recomputes every median and
IQR from the [raw batches](compile-run/micro.json), verifies identical binaries and
reproduces complete goldens. Source checkouts are clean and immutable, input
hashes match, all three workers and fixture directories are gone, the service
group is empty and the shared lease is available. [Ownership](compile-run/ownership.json),
[terminal receipt](compile-run/terminal.json) and [final cleanup](final-cleanup.json)
retain the evidence. The [raw archive](raw-evidence.tar.gz) includes unchanged
catalog, build/capture recipes and receipts; SHA256:
`ab3e93ae2c85271c654c8a77cac946e6d666f4b2bb811718e93b93bce99e4269`.

This comparison is closed. The historical AF row remains visible with its own
revision and uncertainty. Runtime revision 348998dc has cached green
[Windows/Ubuntu CI39124478080](https://github.com/Lokad/Lython/actions/runs/39124478080),
including Release package consumers. This evidence/documentation delivery changes
no runtime, test, benchmark implementation, worker or script source.
