# Representative function-lookup-source CPython checkpoint

2026-11-08, R122/B481. Measured runtime `6f76f99d876ad7f3678ed4f4ecc416619c648642`,
frozen core `c6172fec`; exact production inventory and auxiliary hashes in
[identity](identity.json). Test-only fixture fix `d97ca8f2` leaves runtime source
unchanged. Combined Windows/Ubuntu full Release actual.NET11/.NET10, original
stress and installed-core consumers pass [cached delivery](../2026-11-08-function-lookup-lifetimes/delivery-ci.json).
Later R124 prototypes are separate; earlier revisions keep independent receipts.

Five unchanged maintained scripts, separately compilation, warm execution and
compile-to-result: 15 jobs / 311.685s. Isolated CPython 3.13.16 versus .NET 11 RC3,
dedicated Azure F4as_v7, Ubuntu 24.04.4, four EPYC 9V45 vCPUs, 16 GiB/no swap.
Normal GC/tiering, two independent Lython processes, five-second warmup per
role/job and seven rotated 150ms rounds. Frozen worker `7cf`, optional adapter
0.4, existing catalogs, full stdout/file goldens and protocol unchanged.

| Workload | Input | Lython | CPython | Lython / CPython | Status; control / max IQR |
| --- | --- | ---: | ---: | ---: | --- |
| CSV validation/helper calls | 5,120 records / 68,625 B | 5.227–5.247 ms | 4.467 ms | 1.17–1.17× | Diagnostic; 0.39% / 0.87% |
| CSV summary/helpers | 4,096 records / 57,361 B | 3.401–3.470 ms | 3.368 ms | 1.01–1.03× | Variable; 2.02% / 0.74% |
| Text summary/helpers | 3,072 lines / 35,840 B | 1.938–1.959 ms | 1.276 ms | 1.52–1.53× | Diagnostic; 1.09% / 1.33% |
| JSON-lines/helpers | 4,096 records / 110,592 B | 3.877–4.084 ms | 4.261 ms | 0.91–0.96× | Variable; 5.36% / 0.56% |
| JSON-to-Markdown report | 3,072 records / 189,441 B | 3.240–3.247 ms | 2.450 ms | 1.32–1.33× | Diagnostic; 0.23% / 2.35% |

Ratio = Lython elapsed / CPython elapsed; above one means slower. Ranges span two
independent Lython medians, not confidence intervals. Control is their absolute
difference; max IQR includes CPython. Variable observations remain inconclusive.
All compile/warm roles, process CPU and controls are in [summary](summary.json).

Warm includes invocation, mediated RAM-file I/O, complete validation and cleanup
of the previous result. Compilation includes construction/disposal under the
shared allowance; compile-to-result includes both. Startup, staging/reset,
protocol and final cache teardown are outside clocks. CPython retains imports;
Lython isolates execution imports. Pending I/O, RSS and embedding-tail impact
remain unmeasured. Historical configuration/search/manifest cases were not rerun.
No isolated causal gain, source-pair subtraction or new regex qualification.

All Ready/source/core/auxiliary/golden/balance/quiet/Closed/exit/join/unmount/
archive/local-verification gates pass. Finite 360s owner; persistent raw evidence
separate from owned RAM fixtures. No users or fixtures remain. Archive SHA256:
`6ca0ff4988817f2b057fd29c64429025e4ab2ddd76f0fba13c974c98df49f7ca`.

The optional historical exact-byte hardlink preparation failed with EPERM on a
root-owned old DLL. Original stderr/command and all conditions are preserved.
Launch independently passed actual payload + 240MiB OS floor + 12MiB headroom.
A subsequent read-only audit under a fresh idle lease verified all 17 original
aliases and hashes unchanged, with no temporary links. Future preparation must
preflight permitted canonical ownership/writable parents and journal before
mutation. No deletion, chmod workaround or measurement rerun.

[Raw observations](observations.tgz), [receipt](archive-receipt.json),
[sizing](sizing.json), [alias audit](space-post-audit.json),
[collection/source/failure diagnostics](collection-and-space-diagnostics.tgz),
[prior guide](prior-BENCHMARKS.md). Current source-pair gains and adverse controls
remain in the separate R119–R121 evidence; this checkpoint does not repay them.
