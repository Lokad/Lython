# Combined retained-source CPython checkpoint — R77 / B440

2026-11-08. Measured source38f53c33/core903eb346 (informational SourceLinkc384956f),
full recorded production inventory. All26 jobs complete in539.658580s: eight
complete maintained scripts, compilation/warm/compile-to-result independently,
plus two explicitly labeled numeric-string warm inputs. CPython3.13.16 is the
external baseline. Two identical-build Lython processes expose control variation;
ratios are Lython elapsed/CPython elapsed, ranges are process medians, not confidence
intervals. Max IQR includes reference variation; labels are descriptive only.

Windows/Ubuntu actual.NET11/10 Release, original Stress and installed core consumers
pass CI42439532114, [cached separately](../2026-11-08-inline-utf8/delivery-ci.json).
The identity's pending status describes launch time. Later R78 prototypes are not
measured or qualified here. No gain inferred against another historical collection,
universal CPU claim, debt repayment, new producer qualification or publication.

Azure F4as_v7/Ubuntu24.04.4/four EPYC9V45 vCPUs/16GiB/no swap, .NET11RC3,
CPython GCC13.3 PGO/LTO/GIL/GC/noJIT/debug/-I -S -X utf8. Five-second warmup per
role/job, seven rotated150ms rounds, normal CLR GC/tiering, quiet endpoints.
Warm includes invocation, mediated RAM I/O, full stdout/file validation and
prior-result cleanup; compile includes construction/disposal; compile-to-result
adds both. Startup/staging/reset/protocol/final cached teardown excluded.
CPython caches imports; Lython isolates per-run imports and checks host paths.
No pending-I/O, wall-I/O decomposition, RSS or embedding-tail measurement.

All source/core/auxiliary/actualReady/runtime/golden/quiet/Closed/exit/join/fixture
gates pass. Persistent evidence is separate from owned128MiB RAM fixtures. Every
worker joined and fixtures/mount released before archive verification under the
lease and independently locally.570s owner+30s stop+30s observation margin.

Original B437/B439 staging-floor failures collected no samples/workers. B438's
worker-role typo failed before samples: all users joined; its still-mounted empty
owned RAM was explicitly released, original failure archived/locally verified.
B440 corrects the label and hardlinks unchanged metadata to avoid duplicate
staging while keeping240MiB OS floor+7MiB evidence reserve. Failed conditions and
retired archive-verified redundant baseline links stay in the evidence; no timing
or containment assertion was relaxed.

[Current guide](../../../docs/BENCHMARKS.md), [summary](summary.json),
[raw observations](observations.tgz), [archive receipt](archive-receipt.json),
[original setup failures/corrected collector](setup-failures.tar.xz),
[previous guide](prior-BENCHMARKS.md). Raw archive includes all Ready identities,
catalog/goldens, production inventory, source patch, core, Python worker and
boundaries. The unchanged .NET worker and auxiliary binaries are identified by
Ready/manifest hashes and preserved in [R76's archive](../2026-11-08-inline-utf8/observations.tar.xz).
