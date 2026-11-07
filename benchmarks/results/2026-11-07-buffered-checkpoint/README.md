# Retained-source CPython checkpoint — R67 / B421

2026-11-07. Source **824fc6bb**, frozen core **15c04e2a**. The 561-file production
inventory matches the source; informational SourceLink is precommit5dd131bf.
R65/R66 focused actual .NET11/10 checks pass. Full combined Windows/Ubuntu,
original Stress and installed-package CI42418518578 is pending; this measurement
does not substitute for it. Publication remains postponed.

**23 jobs / 478.32 seconds**, CPython3.13.16 versus current Lython with two
independent identical-build processes. Seven maintained scripts, each separated
into compilation, warm execution and compile-to-result; two additional explicit
numeric-input warm variants. No source-pair speedup is inferred across historical
collections. CPython is the external baseline; source-pair improvements remain
separate. The [current guide](../../../docs/BENCHMARKS.md) gives all three tables;
the [previous source55e510ff guide](prior-BENCHMARKS.md) remains historical.

Azure Standard_F4as_v7, Ubuntu24.04.4, four EPYC9V45 vCPUs,16GiB/no swap,
.NET11RC3 and CPython GCC13.3 PGO/LTO, ordinary GIL/GC/no JIT/debug,-I -S -X utf8.
Normal CLR GC/tiering; five-second warmup per role/job, seven rotated150ms rounds,
quiet endpoints. Same unchanged worker, normal optional0.4 adapter and goldens.
Not a new producer/adapter-performance qualification.

Ratios are Lython/CPython elapsed. Ranges span the two Lython process medians,
not confidence intervals. Control spread and all-role IQRs remain explicit,
including CPython variation. Diagnostic requires control<2% and maxIQR<3%; these
are descriptive measurement labels, not design acceptance thresholds. Original
raw data remain unchanged. Coarse10ms process CPU spans external protocol work
and is not a precise in-job CPU attribution. Variable/adverse results stay visible.

Warm includes public invocation, mediated RAM-file I/O, complete stdout/file
validation and prior-result cleanup. Compile includes construction/disposal;
compile-to-result adds compilation. Startup, fixture staging/reset, protocol
transport and final cached teardown are excluded. CPython retains imports while
Lython isolates execution imports. Lython opens include host capability and real
path-component validation; CPython uses ordinary open. No wall-I/O decomposition,
startup, genuinely pending-I/O, RSS or embedding-tail measurement is claimed.

All source/actual Ready/auxiliary/golden/quiet/Closed/exit/join gates pass. All
workers finish and guest fixtures disappear. Persistent records/workers are
separate from the collection-owned128MiB tmpfs. Cleanup joins users and unmounts
before archive verification under the global lease; the archive is independently
verified locally.600s owner+30s stop+30s observation margin; no collection restart.
Exact redundant B415 extracted reference storage was reclaimed only after local/
remote archive/member verification; immutable historical evidence is preserved.

[Summary](summary.json), [raw archive](observations.tgz), [archive receipt](archive-receipt.json),
[terminal status](terminal-status.json), [source/image identity](identity.json),
[contract](contract.md), [collector](collect.py), [focused checks](focused.json).
The identity's pending-CI status describes launch time, not a later qualification.

Combined R65/R66 source824fc6bb is **FULLY QUALIFIED**, CI42418518578:
Windows/Ubuntu Release actual.NET11/10, original Stress at both targets and
installed core-package consumers all pass. [Cached delivery receipt](../2026-11-07-buffered-lines/delivery-ci.json).
Never query the terminal run again. Launch-time pending statements in immutable
identities/plans remain historical. This does not qualify later runtime source.
