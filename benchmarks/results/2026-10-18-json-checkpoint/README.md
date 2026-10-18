# Complete JSON helper comparison, 2026-10-18 UTC

| Fresh compile + synchronous execution | Lython | CPython | Lython / CPython | Status |
| --- | ---: | ---: | ---: | --- |
| 4,096 JSON records / 110,592 input bytes; complete helper/aggregation/report job | 9.466–9.607 ms | 4.441 ms | 2.132–2.163× diagnostic | Lython spreads 13.11% / 3.52%; CPython 0.36%; unqualified |

Two separate Lython processes load the **same immutable source and binaries**:
**6584ddd7fb87ede96b8e017f2b3835c78b539a1f**. CPython **3.13.16** is the primary
external baseline. Ratios divide Lython time by CPython time. The range contains
medians of identical controls, not confidence bounds or old/new Lython revisions.
The controls differ by 1.49%; one Lython IQR/median is 13.11%. This capture shows
a gap for this job, with its precise magnitude uncertain. It establishes no trend
from historical AU/CC, individual-fix gain or attribution to compilation, JSON,
helper calls, initialization or host I/O.

The existing [607-byte helper script](source.py) reads [input.jsonl](input.jsonl),
skips 1,024 blank lines, parses 4,096 records, calls an annotated ordinary helper,
filters negative amounts and aggregates by key. It writes the exact sorted
[25-byte report](expected-report.json) and prints [3072](expected-stdout.txt).
Independent input parsing and aggregation reproduce the complete report/stdout.
An isolated local CPython 3.13.2 golden was verified before collection; the VM's
CPython 3.13.16 and both Lython controls then check all complete output digests.

## Boundary and fairness

Every timed job compiles source afresh and executes it once synchronously in an
already-running host. There is no identical-script cache in this lane. Both
engines include fresh invocation state/capture, input staging and output reset,
file reads, parsing, helper/filter/aggregation, file writing and complete byte
checks. Lython includes prior-result cleanup and disposal of fresh compilation.
Worker startup, transport and response hashing are excluded. Warm execution,
compilation alone, startup and genuinely suspending I/O are separate boundaries;
none can be inferred by subtracting these medians.

Lython retains ordinary execution/projection limits, cancellation, managed stack
admission, allocation preflights and cooperative ownership. The comparison uses
one shared unbounded host allowance. Final results remain funded through response
writing; cached verification compilations are disposed on worker exit. CPython
keeps ordinary reference counting/cyclic GC without equivalent in-process
containment. Nothing is subtracted. This does not qualify finite-budget streaming,
refused admission, physical heap/RSS bounds or embedding tail latency.

## Provenance and bounded collection

Dedicated Azure VM: Ubuntu 24.04.4 LTS, Linux 6.17.0-1022-azure, four AMD EPYC
9V45 vCPUs, 16 GiB RAM, no swap. Clean Release build: .NET SDK 10.0.401/runtime
10.0.12. CPython 3.13.16 uses GCC 13.3.0, PGO/LTO, ordinary GIL/GC and
`-I -S -X utf8`, with no debug/free-threaded/JIT mode.
[Inspection](inspection.json), [preparation](preparation.json),
[build log](build-candidate.log) and [preflight](preflight.json) retain identities
and machine/build evidence. The [sealed scope](capture-scope.json) pins source,
input, canonical LF stdout and full output hashes before collection.

Exactly one existing 15-second micro command ran, with one-second warmup per
engine, two-second settling and seven rotating-order batches calibrated to 25 ms.
The owned command completed in **8.659068 seconds**. Its systemd process group
enforces 28 seconds work plus at most one second cleanup. Only the supervisor
disables tiering; workers remove that override. Lython worker overrides are null,
normal GC/tiering remain enabled and BenchmarkDotNet is not loaded. The VM lease
was held during collection. Inspection load was near zero; post-build preflight
load was 0.12/0.04/0.01. This is a short diagnostic, with no milestone noise
qualification, repeated capture, profile or ten-minute lane.

All **35 responses / 432 invocations** match source, fixture, stdout and complete
file digests. [Validation](validation.json) recomputes every median and IQR from
[raw batches](compile-run/micro.json), verifies identical binaries and reproduces
complete goldens. Input hashes match and source checkouts are clean/immutable.
All three workers and fixture directories are gone, the service group is empty
and the shared lease is available. [Ownership](compile-run/ownership.json),
[terminal receipt](compile-run/terminal.json) and [final cleanup](final-cleanup.json)
retain evidence. The [raw archive](raw-evidence.tar.gz), including catalog,
recipes and receipts, has SHA256:
`ac827ea50a4be3268dfa6624eac598c8552cb43a0a7a9135c16e9eb53793885c`.

This comparison is closed. Historical AU and CC remain visible with their own
revisions, boundaries and uncertainty. Runtime 6584ddd7 has cached green
[Windows/Ubuntu CI39130911660](https://github.com/Lokad/Lython/actions/runs/39130911660),
including Release consumers. This evidence/docs delivery changes no runtime,
test, worker, benchmark implementation or workload script.
