# Representative CPython checkpoint — 2026-11-11

Retained `8f8628a4`, frozen refined R245 core with precommit metadata, versus stock
CPython 3.13.16. All production source hashes match. The fixed `fec79` worker,
optional adapter 0.4, protocol, six complete scripts and input fixtures are
unchanged. Full exact-source platform/stress/installed-consumer qualification
remains separate and pending; 5,092 focused actual .NET 11/10 checks pass.

Eighteen jobs finish in **486.404 seconds** on the existing Azure F4as_v7 quiet
VM: Ubuntu 24.04.4, four AMD EPYC 9V45 vCPUs, 16 GiB/no swap, .NET 11 RC3.
Five-second warmups per role/job, seven rotated 150 ms rounds and ordinary
GC/tiering are unchanged. Each engine has two independent processes.

Warm Lython/CPython ratios are captured text **1.77–1.84×**, ordinary text
**1.06–1.11×**, CSV **1.04–1.05×**, JSON-lines **0.84–0.87×**, report
**1.28–1.35×** and keyword-default search **1.54–1.57×**. Captured/ordinary text
and report are variable. Search compile-to-result is **1.45–1.60×**, with roughly
4.7% controls on both engines. All 18 times, ratios, controls and IQRs belong in
[BENCHMARKS](../../../docs/BENCHMARKS.md), the current authoritative guide.

Warm and compile-to-result include mediated RAM-file I/O, complete output/file
validation and prior-result cleanup. Compilation is separate and includes code
construction/disposal. Startup, fixture staging/reset, protocol and final cache
teardown are excluded. External process CPU includes reset/protocol. Independently
measured medians need not add up. Ratios cover all four process-median pairings,
not confidence intervals.

Every loaded core/source/fixture/output identity, budget balance, quiet gate,
joined worker, RAM cleanup and archive check passes. The persistent archive is
verified under the owned lease and independently after retrieval. B590 exits 0
and is terminal/cached. A duplicate initial-R245 archive was retired only after
exact committed canonical Git bytes and fresh path/UID/inode/all-alias proofs;
current core roles remain intact and all original evidence survives.

The [prior guide](prior-BENCHMARKS.md) preserves replaced `6904093e` rows and all
older evidence links. Separate checkpoints do not attribute improvements to one
change. Report overlaps the earlier ratio range; this establishes neither a
regression nor neutrality. The separate asynchronous source-pair report CPU debt
remains unresolved: synchronous external rows do not settle it. Allocation gains
do not establish CPU gains, and source-pair changes are not multiplied into new
external ratios. Missing/adverse evidence remains explicit.

[Summary](summary.json), [source identity](source-identity.json),
[worker identity](identity.json), [raw observations](observations.tgz),
[receipt](archive-receipt.json) and [archive hashes](archive-identity.json) preserve
the full conditions. The archive contains its exact measured `current-core.dll`.
This checkpoint does not qualify a newer regex producer, pending-I/O latency,
RSS/heap peaks, embedding tail latency or large-file throughput. Publication stays
postponed; the remaining performance roadmap continues.

First full delivery attempt `43081976930` is terminal **failed**: Ubuntu actual
.NET 11/10, package consumers and original stress pass; Windows .NET 11 passes,
but a five-second async file-callback test times out on .NET 10. Windows package/
stress steps are skipped. The [receipt](delivery-first-attempt.json) and
[original job log](windows-net10-callback-failure.log.gz) preserve that failure.
The same32focused checks pass before correction on local .NET 10. The fixture
moves to the existing nonparallel RuntimeStress collection, retaining its actual
suspension/cancellation/cleanup assertions and unchanged five-second deadlines;
32checks pass on each target. Scheduling interference remains a hypothesis.
Corrected full delivery is pending; no runtime change or performance remeasurement
is implied by this test-scheduling correction.

Subsequent corrected delivery `43087005596`, source `458be840`, is **GREEN** on
Windows/Ubuntu, actual .NET 11/10, original stress and installed Release consumers.
Its production source is exactly retained `8f8628a4`; the correction changes test
scheduling, keeping five-second deadlines and every assertion. The
[terminal receipt](delivery-ci-status.json) is cached. Original failure and
collection-time status remain preserved. This does not qualify later prototypes
or resolve the report CPU debt.
