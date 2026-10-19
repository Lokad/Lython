# JSON scalar completion diagnostic

One bounded capture on 2026-10-19 compares immutable Lython `3d1ec461` with
`13375e82` and isolated CPython 3.13.2. The candidate removes always-async
number/constant forwarding; pending callbacks retain their existing managed
frame until completion. Container parsing and Python hooks are unchanged.

| Engine | Fresh compile and complete job | IQR / median | Status |
| --- | ---: | ---: | --- |
| Lython `3d1ec461` | 10.577 ms | 3.11% | Before; diagnostic |
| Lython `13375e82` | 10.697 ms | 10.45% | After; inconclusive change |
| CPython 3.13.2 | 14.624 ms | 2.53% | External context on this machine only |

The maintained `scripts.json-summary-helpers.large` job processes 4,096 records
in 110,592 UTF-8 input bytes, including ordinary helper calls, file iteration,
aggregation and output. Each invocation compiles afresh in an already-running
host. Timing includes disk staging/reset, complete work, exact output checks and
compilation/result cleanup. Startup and worker protocol are excluded. Compilation
alone and warm execution were not measured separately here.

The shared Windows workstation is LOKAD-0399, x64, Intel Family 6 Model 183,
28 logical processors, Windows build 26200, .NET 10.0.13, workstation GC. The
existing micro command used seven alternating rounds after warmup. The owned
process exited successfully in 9.298 seconds, including worker cleanup. Exact
stdout and output-file hashes matched before and after timing for all engines.

There is **no demonstrated speedup**: the candidate median is 1.13% higher and
its spread is 10.45%. The candidate/CPython quotient is 0.731, an unqualified
point estimate affected by this Windows file-host boundary. It is not comparable
with the quiet Linux VM snapshots and does not replace their representative
table. No allocation, embedding-latency or individual compilation-cost claim
follows. Do not retime this small removal as a new qualification campaign.

Evidence: [raw samples and identities](micro.json), [shared sources, fixtures and
independent goldens](catalog.json), [owned process receipt](process.receipt.json),
[frozen baseline hashes](before-identity.json), [scope and checksums](scope.json).
The existing `--compare micro` command was used with `--lane compile-run`;
no benchmark framework or workload was added.
