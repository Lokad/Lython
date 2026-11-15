# Representative CPython delivery checkpoint

2026-11-15 UTC, committed **43bce13a / frozen9e890d18**, stock **CPython3.13.16**.
[Full Windows/Ubuntu11/10, original stress and installed-consumer qualification passes](../2026-11-15-completed-arithmetic/delivery/README.md). Original capture metadata retains its at-capture status.
All586 production inputs match the frozen source with explicit newline normalization;
independent rebuilt binary identity is not inferred.

| Representative workload / phase | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| CSV invoice / warm Run | 4,096 records / 127,031 B | 6.710–6.749 ms | 5.983–6.077 ms | 1.10–1.13× | Diagnostic; spreads 0.57/1.55%; IQR ≤1.10% |
| CSV invoice / compilation only | 4,096 records / 127,031 B | 0.101–0.106 ms | 0.094–0.094 ms | 1.08–1.13× | Diagnostic; spreads 4.52/0.49%; IQR ≤1.74% |
| CSV invoice / compile and execute once | 4,096 records / 127,031 B | 6.826–7.004 ms | 6.215–6.263 ms | 1.09–1.13× | Diagnostic; spreads 2.54/0.76%; IQR ≤2.10% |
| CSV fulfillment / warm Run | 4,096 records / 126,821 B | 7.568–7.594 ms | 6.783–6.794 ms | 1.11–1.12× | Diagnostic; spreads 0.34/0.17%; IQR ≤1.70% |
| Text / captured helper and counter / warm Run | 3,072 lines / 35,840 B | 1.211–1.248 ms | 0.888–0.891 ms | 1.36–1.41× | Diagnostic; spreads 3.05/0.31%; IQR ≤1.24% |
| JSON-lines / aggregation helpers / warm Run | 4,096 records / 110,592 B | 3.416–3.484 ms | 4.053–4.070 ms | 0.84–0.86× | Diagnostic; spreads 1.98/0.42%; IQR ≤1.31% |
| File search / defensive helper / warm Run | 2,049 lines / 42,010 B; stop after 1,025 | 0.286–0.303 ms | 0.157–0.158 ms | 1.81–1.93× | Diagnostic; spreads 5.90/1.10%; IQR ≤3.18% |
| Configuration / six-helper manifest / compile and execute once | 833 B inputs / 1,877 B source | 0.498–0.498 ms | 0.470–0.475 ms | 1.05–1.06× | Diagnostic; spreads 0.08/1.02%; IQR ≤1.40% |

Eight jobs finish in **321.442s** on the quiet Azure F4as_v7 Ubuntu24.04.4 VM,
four AMD EPYC9V45 vCPUs,16GiB/no swap,.NET11RC3 workstation GC/normal tiering.
Two independent processes/engine, five rotated0.5s batches; full-request warmup
5s ordinary/compile,12s captured text,8s fresh. All medians and adverse controls
remain. Ratios are every process-median pairing, not confidence intervals.
Compilation Lython spread is4.52%, fresh invoice2.54%, text3.05%, search5.90%:
do not assert exact portable gaps or cross-collection gains.

Warm includes synchronous Run, mediated RAM I/O, full stdout/file goldens and
prior-result cleanup; excludes compilation/startup, reset/protocol and final
cache-owner teardown. Compilation-only includes compiled-owner disposal; fresh
adds compilation and disposal in one operation. Independently warmed phase
medians are not additive. Wider CPU also includes reset/protocol. Lython's host
supplies efficient synchronous stream/range operations and shared-budget/capability
checks; CPython uses native buffered files without equivalent containment.
No startup, pending-I/O, RSS/heap, contention or embedding-tail claim.

**Original owner is terminal FAILED:** the initial shell stops before starting
workers because generated shell line endings are CRLF. A separately identified
owner corrects only LF and completes every sampling/golden/quiet/budget/join gate.
After fixture teardown its archive step refuses the unchanged252MiB disk floor.
No timing is rerun or dropped. Persistent observations are subsequently verified
byte-for-byte under a fresh lease and independently SHA-verified locally; all
workers are joined and RAM fixtures are gone. Preserve both failures, the original
floor condition and archive-recovery qualification separately from timing validity.
[Disk headroom is restored](recovery-headroom.json) by retiring only three exact committed archived working duplicates, with full hash/path/UID/link proof and a fresh idle lease. Original failed owners remain failed; floors are unchanged. No claim that the
original archive-owner gate passed; the recovery disposition is recorded above.

[All medians/controls](summary.json),[exact identity](identity.json),
[original observations](observations.tgz),[recovered custody](recovered-archive-receipt.json),
[original failed owner](failed-archive-journal.txt),[procedure](procedure.tar.xz),
[preceding guide with older rows](prior-BENCHMARKS.md).
