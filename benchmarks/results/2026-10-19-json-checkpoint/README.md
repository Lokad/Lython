# Complete JSON helper comparison, 2026-10-19 UTC

| Fresh compile + synchronous execution | Lython | CPython | Lython / CPython | Status |
| --- | ---: | ---: | ---: | --- |
| 4,096 JSON records / 110,592 input bytes; complete helper/aggregation/report job | 9.261–9.541 ms | 4.461 ms | 2.076–2.139× diagnostic | Lython spreads 18.70% / 6.52%; CPython 2.48%; unqualified |

Two separate Lython processes load the **same immutable source and binaries**:
**a22c11b884f523f91fab4fd4852c33f7f58d6460**. CPython **3.13.16** is the primary
external baseline. Ratios divide Lython time by CPython time. The range contains
medians of identical controls, not confidence bounds or old/new Lython revisions.
The controls differ by 3.03%; one Lython IQR/median is 18.70%. This capture shows
a gap for this job, with its precise magnitude uncertain. It establishes no trend
from historical AU/CC, individual-fix gain or attribution to compilation, JSON,
helper calls, initialization or host I/O.

The existing [607-byte helper script](source.py) reads [input.jsonl](input.jsonl),
skips 1,024 blank lines, parses 4,096 records, calls an annotated ordinary helper,
filters negative amounts and aggregates by key. It writes the exact sorted
[25-byte report](expected-report.json) and prints [3072](expected-stdout.txt).
Independent input parsing and aggregation reproduce the complete report/stdout.
The unchanged script/input/output hashes reuse the earlier isolated CPython
3.13.2 golden; the VM's
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

Execution-time control uses the host CancellationToken; fuel and host-call quotas
are removed. Lython retains ordinary output/projection limits, managed stack
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
The owned command completed in **8.659841 seconds**. Its systemd process group
enforces 28 seconds work plus at most one second cleanup. Only the supervisor
disables tiering; workers remove that override. Lython worker overrides are null,
normal GC/tiering remain enabled and BenchmarkDotNet is not loaded. The VM lease
was held during collection. Inspection load was near zero; post-build preflight
load was 0.42/0.10/0.03. This is a short diagnostic, with no milestone noise
qualification, repeated capture, profile or ten-minute lane.

All **35 responses / 424 invocations** match source, fixture, stdout and complete
file digests. [Validation](validation.json) recomputes every median and IQR from
[raw batches](compile-run/micro.json), verifies identical binaries and reproduces
complete goldens. Input hashes match and source checkouts are clean/immutable.
All three workers and fixture directories are gone, the service group is empty
and the shared lease is available. [Ownership](compile-run/ownership.json),
[terminal receipt](compile-run/terminal.json) and [final cleanup](final-cleanup.json)
retain evidence. The [raw archive](raw-evidence.tar.gz), including catalog,
recipes and receipts, has SHA256:
`529b2c25d33c57989eb1cef731f8cfaacebf5581533d38653def2c918b28ec71`.

This comparison is closed. The preceding [6584ddd7 snapshot](../2026-10-18-json-checkpoint/README.md),
historical AU and CC remain visible with their own
revisions, boundaries and uncertainty. Runtime a22c11b8 has cached green
[Windows/Ubuntu CI39273874116](https://github.com/Lokad/Lython/actions/runs/39273874116),
including Release consumers. This evidence/docs delivery changes no runtime,
test, worker, benchmark implementation or workload script.
