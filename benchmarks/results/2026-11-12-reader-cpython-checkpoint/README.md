# Retained reader/frontend CPython checkpoint

2026-11-12, exact retained8ec7bfab; all568 production files match the frozen
R271 source with explicit checkout newline normalization. Fixed fec79 worker,
normalGC/tiering, optional adapter0.4; stock CPython3.13.16, Azure F4as_v7 /
Ubuntu24.04.4 /.NET11RC3. Full source qualification is active43223095054;
these measurements do not qualify a release. Publication remains postponed.

Warm execution: two processes/engine,5swarmup(12s captured text),7rotated250msrounds,
four jobs in154.847seconds. Each runs identical source and fixtures, full stdout/
files, independent source/fixture/output/worker identities and balances. Quiet
before/after, joined users, unmount and independent archive SHA gates pass.

| Script | Lython | CPython | Lython / CPython | L / P control spread; maxIQR |
| --- | --- | --- | --- | --- |
| File search, 2,049lines/42,010B; stop1,025 | 0.246–0.248ms | 0.159–0.160ms | 1.54–1.56× | 0.78/0.36%;3.07% |
| Captured text,3,072lines/35,840B | 1.457–1.494ms | 0.889–0.910ms | 1.60–1.68× | 2.53/2.24%;3.56% |
| JSON-lines/helpers,4,096records/110,592B | 3.512–3.512ms | 4.082–4.101ms | 0.86–0.86× | 0.01/0.47%;2.69% |
| Report,3,072records/189,441B | 3.102–3.130ms | 2.540–2.594ms | 1.20–1.23× | 0.90/2.05%;3.37% |

Ratios above1 mean slower. Ranges span all four engine-process median pairings,
not confidence intervals. Warm clocks include mediated RAM I/O, complete output
validation and prior-result cleanup; exclude compilation/startup/reset/protocol/
finalcache cleanup. Wider processCPU includes additional worker/reset/protocol/GC.
Source-pair gains are not extrapolated into these external ratios. This collection
cannot isolate changes against older checkpoints. Compile/fresh follow separately
for native search and JSON; historical other phases remain explicitly labelled.

An initial duplicate-retirement preflight rejected stopped.utc: the prior archive
captured an earlier finish timestamp, then the owner updated it after archival.
No deletion or worker occurred. The changed timestamp was excluded, not treated as
an exact duplicate. Original failed script/error remain. Only unchanged terminal
bytes with canonical committed Git/SHA and fresh lease/path/UID/inode/link proof
were retired; both image families and OS240MiB/evidence12MiB floors remain.
B624 terminal state is cached; no remaining VM users/RAM fixtures.
