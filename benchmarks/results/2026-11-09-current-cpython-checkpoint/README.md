# Representative CPython checkpoint — R169

2026-11-09. Five unchanged complete CSV/text/JSON/search/config scripts, separately
compiled, warm-executed and compiled-to-result. Fifteen jobs finish 410.313 seconds;
all identities, goldens, funding, quiet, Closed, joined-worker, RAM teardown and
archive checks pass. No VM workers or fixtures remain. Results are diagnostic,
including adverse/variable controls; no individual optimization gain is inferred.

Production source matches 43350e4f/220732b7, using the freshly nonincremental R167
precommit freeze, not a postcommit rebuild. Core cc596abc; unchanged worker fec79afd
and optional adapter 0.4. Reference: stock CPython 3.13.16, actual .NET 11 RC3,
F4as_v7/Ubuntu 24.04.4, four EPYC 9V45 vCPUs, 16 GiB/no swap, ordinary GC/tiering.
Five seconds warmup and seven rotated 150 ms rounds per role/job, two independent
processes per engine. Ratios span all four median pairings, not confidence intervals.

Mediated RAM-file I/O, complete output validation and prior-result cleanup are
included; startup, fixture staging/reset, protocol and final cache teardown are
excluded. Compilation includes construction/disposal. CPython retains imports;
Lython isolates execution imports and performs host capability/path checks.
External process CPU has a wider boundary. No pending-I/O or embedding-tail claim.

The updated [performance guide](../../../docs/BENCHMARKS.md) presents all phases.
Text complete 1.54–1.58× remains variable. JSON warm CPython controls differ 10.43%;
retain that uncertainty. Current CSV complete 1.09–1.11× and config 1.11–1.12×.
These observations do not prove changes against older nonpaired checkpoints.

Offline analysis initially lacked its local catalog path. The SHA-verified catalog
was copied from the already retrieved archive; only analysis reran, with no timed
data changes or collection restart. Original correction is retained. Finite owned
collection archived evidence before ending; receipt SHA verified locally. Fresh
space proof removed only the exact tracked redundant B516 compressed copy when
required; original raw/receipts/cores and original storage floors remain.

[Summary](summary.json), [source proof](core-source.json), [identities](identity.json),
[raw observations](observations.tgz), [archive receipt](archive-receipt.json),
[terminal status](terminal-status.json), [analysis correction](analysis-correction.json),
[replaced guide](prior-BENCHMARKS.md), [preparation and exact space proof](preparation.tar.xz).
Performance evidence is separate from full platform/package qualification.
